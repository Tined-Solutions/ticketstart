using Microsoft.EntityFrameworkCore;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Helpers;
using TicketeraOnline.Api.Services;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Authorization;
using TicketeraOnline.Api.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Console;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Threading.RateLimiting;
using Amazon.S3;
using Amazon.Runtime;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Configure structured logging using built-in Microsoft.Extensions.Logging.
// Message templates with named placeholders produce structured fields for stdout consumers.
// All stdout output is piped through LogRedactor before writing.
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
builder.Logging.AddConsole(options => options.FormatterName = "redacted");
builder.Logging.AddConsoleFormatter<RedactingConsoleFormatter, SimpleConsoleFormatterOptions>();

// Add services to the container.
// Register application services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IMetricsService, MetricsService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

// Configure Mercado Pago
builder.Services.Configure<MercadoPagoOptions>(builder.Configuration.GetSection(MercadoPagoOptions.SectionName));
builder.Services.AddHttpClient<IMercadoPagoClient, MercadoPagoClient>(client =>
{
    client.BaseAddress = new Uri("https://api.mercadopago.com/");
});

// Configure Resend email
builder.Services.Configure<ResendOptions>(builder.Configuration.GetSection(ResendOptions.SectionName));
builder.Services.AddHttpClient<IResendClient, ResendClient>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Configure Cloudflare Turnstile
builder.Services.Configure<TurnstileOptions>(builder.Configuration.GetSection(TurnstileOptions.SectionName));
builder.Services.AddHttpClient<ITurnstileService, TurnstileService>();

// Configure reservation HMAC token for guest checkout IDOR protection
builder.Services.Configure<ReservationTokenOptions>(builder.Configuration.GetSection(ReservationTokenOptions.SectionName));

var resendSettings = builder.Configuration.GetSection("Resend");
var resendApiKey = GetRequiredValue(resendSettings, "ApiKey");
var resendFromEmail = GetRequiredValue(resendSettings, "FromEmail");

// Register background services
builder.Services.AddHostedService<ReservationExpirationService>();

// Configure Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), npgsqlOptions =>
    {
        // Enable connection resiliency for transient failures
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null);
        
        // Set command timeout
        npgsqlOptions.CommandTimeout(30);
    });
    
    // Enable detailed errors in development
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is not configured");
if (secretKey.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase) || secretKey.Length < 32)
    throw new InvalidOperationException("JWT SecretKey is not configured or is a placeholder. Provide a key with at least 32 characters that does not start with 'YOUR_'.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };

    // Extract JWT from httpOnly cookie (instead of Authorization header)
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            context.Token = context.Request.Cookies["token"];
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    // Policy for event ownership - requires user to be the event owner or an Admin
    options.AddPolicy("EventOwnership", policy =>
        policy.Requirements.Add(new EventOwnershipRequirement()));
    
    // Policy for Organizador role
    options.AddPolicy("RequireOrganizadorRole", policy =>
        policy.RequireRole("Organizador", "Admin"));
    
    // Policy for Staff role
    options.AddPolicy("RequireStaffRole", policy =>
        policy.RequireRole("Staff", "Admin"));
    
    // Policy for Admin role only
    options.AddPolicy("RequireAdminRole", policy =>
        policy.RequireRole("Admin"));
});

// Register authorization handlers
builder.Services.AddSingleton<IAuthorizationHandler, EventOwnershipHandler>();
builder.Services.AddHttpContextAccessor();

// Register global exception handler and problem details
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Configure Cloudflare R2 (S3-compatible storage)
var r2Settings = builder.Configuration.GetSection("CloudflareR2");
var r2AccessKey = GetRequiredValue(r2Settings, "AccessKey");
var r2SecretKey = GetRequiredValue(r2Settings, "SecretKey");
var r2ServiceUrl = GetRequiredValue(r2Settings, "ServiceUrl");

builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var credentials = new BasicAWSCredentials(r2AccessKey, r2SecretKey);
    var config = new AmazonS3Config
    {
        ServiceURL = r2ServiceUrl,
        ForcePathStyle = true
    };
    return new AmazonS3Client(credentials, config);
});

// Add controllers
builder.Services.AddControllers();

// Configure rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("Resend", config =>
    {
        config.PermitLimit = 3;
        config.Window = TimeSpan.FromHours(1);
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 0;
    });

    options.AddSlidingWindowLimiter("Login", config =>
    {
        config.PermitLimit = 10;
        config.Window = TimeSpan.FromMinutes(1);
        config.SegmentsPerWindow = 4;
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("Reservations", config =>
    {
        config.PermitLimit = 5;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 0;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TicketeraOnline API",
        Version = "v1",
        Description = "Online ticketing platform with event management, reservations, Mercado Pago payments, QR code validation, and organizer/admin dashboards."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste your JWT token here. Swagger UI adds the \"Bearer \" prefix automatically."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Include XML comments from controllers for endpoint descriptions
    var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Rate limiter must be after CORS/HTTPS but before auth/endpoints
app.UseRateLimiter();

// CSRF header protection must run before authentication
app.UseMiddleware<CsrfHeaderMiddleware>();

// Global exception handler must be early in the pipeline to catch errors from auth and endpoints.
app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Shared helper that centralizes the repeated "required configuration value" validation
// and produces a consistent exception message containing the missing key.
static string GetRequiredValue(IConfigurationSection section, string key)
{
    var value = section[key] ?? throw new InvalidOperationException($"{section.Path}:{key} is not configured");
    if (string.IsNullOrWhiteSpace(value))
        throw new InvalidOperationException($"{section.Path}:{key} is not configured");
    return value;
}

// Make Program class accessible for integration tests
public partial class Program { }
