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
using Amazon;
using Amazon.S3;
using Amazon.Runtime;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net.Security;
using System.Security.Authentication;

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
builder.Services.AddScoped<IAdminPurchaseService, AdminPurchaseService>();

// Configure Mercado Pago
builder.Services.Configure<MercadoPagoOptions>(builder.Configuration.GetSection(MercadoPagoOptions.SectionName));
builder.Services.AddHttpClient<IMercadoPagoClient, MercadoPagoClient>(client =>
{
    client.BaseAddress = new Uri("https://api.mercadopago.com/");
});

// Configure transactional email. Staging sends via Brevo (API v3): senders
// are verified in the Brevo dashboard by code, so no domain is required.
// ResendClient remains in the codebase as the alternative once a domain is
// verified — swap the registration below and the "Brevo" config section.
builder.Services.Configure<BrevoOptions>(builder.Configuration.GetSection(BrevoOptions.SectionName));
builder.Services.AddHttpClient<IResendClient, BrevoClient>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IEventNotificationQueue, EventNotificationQueue>();
builder.Services.AddScoped<IRetryableEmailSender, RetryableEmailSender>();

// Configure Cloudflare Turnstile
builder.Services.Configure<TurnstileOptions>(builder.Configuration.GetSection(TurnstileOptions.SectionName));
builder.Services.AddHttpClient<ITurnstileService, TurnstileService>();

// Configure reservation HMAC token for guest checkout IDOR protection
builder.Services.Configure<ReservationTokenOptions>(builder.Configuration.GetSection(ReservationTokenOptions.SectionName));

// Configure HideExpiredEvents feature flag (EHE-009, ADR-4). The section is
// REQUIRED at startup — a missing key fails fast so the flag can never be
// silently absent. Within the section, Enabled defaults to true.
var hideExpiredSection = builder.Configuration.GetSection(HideExpiredEventsOptions.SectionName);
if (!hideExpiredSection.Exists())
    throw new InvalidOperationException("HideExpiredEvents configuration section is required");
builder.Services.Configure<HideExpiredEventsOptions>(hideExpiredSection);

// Single shared clock (ADR-3): services read "now" exclusively through the
// injected TimeProvider so tests can freeze/advance time with FakeTimeProvider.
// TimeProvider.System.GetUtcNow() is DateTime.UtcNow semantically.
builder.Services.AddSingleton(TimeProvider.System);


// Validate the active provider (Brevo) fail-fast at startup.
// Brevo has no sandbox gate like Resend's @resend.dev: an unverified sender
// fails at send time with a clear API error instead.
var brevoSettings = builder.Configuration.GetSection("Brevo");
var brevoApiKey = GetRequiredValue(brevoSettings, "ApiKey");
var brevoFromEmail = GetRequiredValue(brevoSettings, "FromEmail");

// Register background services
builder.Services.AddHostedService<ReservationExpirationService>();
builder.Services.AddHostedService<EventNotificationDispatchService>();

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
    
    // Policy for the scan surface (staff scan event chooser + QR validation).
    // Organizador scans "as staff" per product decision (2026-08-31); the
    // explicit role list keeps the gate future-proof if a buyer role is added.
    options.AddPolicy("RequireScanAccessRole", policy =>
        policy.RequireRole("Staff", "Organizador", "Admin"));
    
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

// AWS SDK on Linux (Render) fails the TLS handshake with R2 using its default
// HttpWebRequest transport ("sslv3 alert handshake failure" via OpenSSL). Route
// the SDK through a modern HttpClient with explicit TLS 1.2+ instead — the same
// transport that already works for Turnstile/Brevo from the container.
AWSConfigs.HttpClientFactory = new SdkTlsHttpClientFactory();

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

// Trust forwarded headers from reverse proxies (ngrok in dev).
// Required so UseHttpsRedirection respects X-Forwarded-Proto: https
// and does NOT redirect ngrok-forwarded HTTPS requests back to HTTP → 502.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // X-Forwarded-Proto keeps UseHttpsRedirection from looping on HTTPS tunnels;
    // X-Forwarded-For lets the ForwardedHeaders middleware rewrite RemoteIpAddress to the
    // real client IP, which the rate limiters partition by (JD-C2) and audit logs rely on.
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    if (builder.Environment.IsDevelopment())
    {
        // Trust any proxy in development (ngrok IPs change).
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    }
});

// Configure rate limiting
builder.Services.AddRateLimiter(options =>
{
    // JD-C2: every limiter is partitioned per client (never global), so one client cannot
    // exhaust a shared bucket and lock out everyone else, and each client actually gets its
    // own limit. Anonymous abuse endpoints (Resend/Login) partition by client IP; Reservations
    // partitions by user id when authenticated, else by IP.
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("Resend", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromHours(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.AddPolicy("Login", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 4,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.AddPolicy("Reservations", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            RateLimitPartitioner.AuthenticatedOrIp(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
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

// Apply forwarded headers from reverse proxies (Render terminates TLS and forwards
// X-Forwarded-Proto/X-Forwarded-For). Must run before any middleware that reads the
// request scheme or RemoteIpAddress (UseHttpsRedirection, rate limiters, audit logs).
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// In development behind ngrok, HTTPS is handled at the edge by ngrok.
// Skip HTTP→HTTPS redirect to avoid 502 from ngrok forwarding.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

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

/// <summary>
/// AWS SDK HttpClient factory that forces TLS 1.2+ through a modern
/// SocketsHttpHandler. The SDK's default HttpWebRequest transport fails the
/// TLS handshake with Cloudflare R2 from Linux containers (Render) with
/// "sslv3 alert handshake failure".
/// </summary>
internal sealed class SdkTlsHttpClientFactory : Amazon.Runtime.HttpClientFactory
{
    public override HttpClient CreateHttpClient(IClientConfig clientConfig) =>
        new HttpClient(new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions
            {
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            },
        });
}
