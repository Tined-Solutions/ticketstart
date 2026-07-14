using Microsoft.EntityFrameworkCore;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Helpers;
using TicketeraOnline.Api.Services;
using TicketeraOnline.Api.Authorization;
using TicketeraOnline.Api.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Console;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Amazon.S3;
using Amazon.Runtime;

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
builder.Services.AddHttpClient<IMercadoPagoClient, MercadoPagoClient>();

// Configure Resend email
builder.Services.Configure<ResendOptions>(builder.Configuration.GetSection(ResendOptions.SectionName));
builder.Services.AddHttpClient<IResendClient, ResendClient>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Configure reservation HMAC token for guest checkout IDOR protection
builder.Services.Configure<ReservationTokenOptions>(builder.Configuration.GetSection(ReservationTokenOptions.SectionName));

var resendSettings = builder.Configuration.GetSection("Resend");
var resendApiKey = resendSettings["ApiKey"] ?? throw new InvalidOperationException("Resend ApiKey is not configured");
if (string.IsNullOrWhiteSpace(resendApiKey))
    throw new InvalidOperationException("Resend ApiKey is not configured");
var resendFromEmail = resendSettings["FromEmail"] ?? throw new InvalidOperationException("Resend FromEmail is not configured");
if (string.IsNullOrWhiteSpace(resendFromEmail))
    throw new InvalidOperationException("Resend FromEmail is not configured");

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
var r2AccessKey = r2Settings["AccessKey"] ?? throw new InvalidOperationException("R2 AccessKey is not configured");
var r2SecretKey = r2Settings["SecretKey"] ?? throw new InvalidOperationException("R2 SecretKey is not configured");
var r2ServiceUrl = r2Settings["ServiceUrl"] ?? throw new InvalidOperationException("R2 ServiceUrl is not configured");

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

// Global exception handler must be early in the pipeline to catch errors from auth and endpoints.
app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

// Make Program class accessible for integration tests
public partial class Program { }
