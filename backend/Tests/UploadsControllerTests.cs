using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Moq;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// HTTP-level tests for POST /api/uploads/event-image (EIM-002): role gate
/// (Organizador + Admin), CSRF, rate limit, MIME ∈ {jpeg,png,webp}, ≤ 5 MB and
/// 200 { imageUrl } — plus EIM-006 (the removed POST /api/events/{id}/image
/// route returns 404). The real R2 transport is replaced by a recording mock
/// (live R2 probes are deploy-time; TLS is proven by R2StorageClientTests).
/// </summary>
[Collection("EnvConfigTests")]
public class UploadsControllerTests
{
    private static MultipartFormDataContent ValidJpegPart()
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(new byte[] { 1, 2, 3 });
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(file, "image", "photo.jpg");
        return content;
    }

    [Fact]
    public async Task UploadEventImage_OrganizerValidJpeg_Returns200WithImageUrl()
    {
        using var factory = new UploadsApiFactory();
        var organizerId = factory.SeedOrganizer();
        var cookie = await factory.LoginAndGetCookieAsync(organizerId);
        using var client = factory.CreateClientWithCookie(cookie);
        client.DefaultRequestHeaders.Add("X-CSRF-PROTECT", "1");

        using var content = ValidJpegPart();
        var response = await client.PostAsync("/api/uploads/event-image", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ImageUploadResponse>();
        Assert.NotNull(body);
        Assert.StartsWith("https://test.r2.dev/events/", body!.ImageUrl);
        Assert.EndsWith(".jpg", body.ImageUrl);

        // EIM-002: no event id is accepted in the request — the service path is
        // event-agnostic: one PUT to R2 with an events/{guid}.jpg key.
        factory.R2Mock.Verify(x => x.PutObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadEventImage_AdminValidPng_Returns200WithImageUrl()
    {
        using var factory = new UploadsApiFactory();
        var adminId = factory.SeedAdmin();
        var cookie = await factory.LoginAndGetCookieAsync(adminId);
        using var client = factory.CreateClientWithCookie(cookie);
        client.DefaultRequestHeaders.Add("X-CSRF-PROTECT", "1");

        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(new byte[] { 9, 9, 9 });
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "image", "graphic.png");

        var response = await client.PostAsync("/api/uploads/event-image", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ImageUploadResponse>();
        Assert.NotNull(body);
        Assert.StartsWith("https://test.r2.dev/events/", body!.ImageUrl);
        Assert.EndsWith(".png", body.ImageUrl);
    }

    [Fact]
    public async Task UploadEventImage_Unauthenticated_Returns401()
    {
        using var factory = new UploadsApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-CSRF-PROTECT", "1");

        using var content = ValidJpegPart();
        var response = await client.PostAsync("/api/uploads/event-image", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        factory.R2Mock.Verify(x => x.PutObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadEventImage_StaffRole_Returns403()
    {
        using var factory = new UploadsApiFactory();
        var staffId = factory.SeedStaff();
        var cookie = await factory.LoginAndGetCookieAsync(staffId);
        using var client = factory.CreateClientWithCookie(cookie);
        client.DefaultRequestHeaders.Add("X-CSRF-PROTECT", "1");

        using var content = ValidJpegPart();
        var response = await client.PostAsync("/api/uploads/event-image", content);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        factory.R2Mock.Verify(x => x.PutObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadEventImage_MissingCsrfHeader_Returns400()
    {
        using var factory = new UploadsApiFactory();
        var organizerId = factory.SeedOrganizer();
        var cookie = await factory.LoginAndGetCookieAsync(organizerId);
        using var client = factory.CreateClientWithCookie(cookie);
        // No X-CSRF-PROTECT header on purpose

        using var content = ValidJpegPart();
        var response = await client.PostAsync("/api/uploads/event-image", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        factory.R2Mock.Verify(x => x.PutObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadEventImage_InvalidMimeVariantJpg_Returns400_NoObjectCreated()
    {
        using var factory = new UploadsApiFactory();
        var organizerId = factory.SeedOrganizer();
        var cookie = await factory.LoginAndGetCookieAsync(organizerId);
        using var client = factory.CreateClientWithCookie(cookie);
        client.DefaultRequestHeaders.Add("X-CSRF-PROTECT", "1");

        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(new byte[] { 1, 2, 3 });
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpg"); // variant of image/jpeg — rejected
        content.Add(file, "image", "photo.jpg");

        var response = await client.PostAsync("/api/uploads/event-image", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        factory.R2Mock.Verify(x => x.PutObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadEventImage_Over5Mb_Returns400_NoObjectCreated()
    {
        using var factory = new UploadsApiFactory();
        var organizerId = factory.SeedOrganizer();
        var cookie = await factory.LoginAndGetCookieAsync(organizerId);
        using var client = factory.CreateClientWithCookie(cookie);
        client.DefaultRequestHeaders.Add("X-CSRF-PROTECT", "1");

        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(new byte[6 * 1024 * 1024]); // 6 MB > 5 MB limit
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(file, "image", "large.jpg");

        var response = await client.PostAsync("/api/uploads/event-image", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        factory.R2Mock.Verify(x => x.PutObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadEventImage_MissingImagePart_Returns400()
    {
        using var factory = new UploadsApiFactory();
        var organizerId = factory.SeedOrganizer();
        var cookie = await factory.LoginAndGetCookieAsync(organizerId);
        using var client = factory.CreateClientWithCookie(cookie);
        client.DefaultRequestHeaders.Add("X-CSRF-PROTECT", "1");

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("no image part here"), "other-field");

        var response = await client.PostAsync("/api/uploads/event-image", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        factory.R2Mock.Verify(x => x.PutObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadEventImage_RateLimit_Returns429On11thCall()
    {
        using var factory = new UploadsApiFactory();
        var organizerId = factory.SeedOrganizer();
        var cookie = await factory.LoginAndGetCookieAsync(organizerId);
        using var client = factory.CreateClientWithCookie(cookie);
        client.DefaultRequestHeaders.Add("X-CSRF-PROTECT", "1");

        // 10 permitted per fixed 1-minute window (policy EventImageUpload)…
        for (var i = 0; i < 10; i++)
        {
            using var content = ValidJpegPart();
            var response = await client.PostAsync("/api/uploads/event-image", content);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // …the 11th is rejected 429 (JD-C2: per-client partition, never global).
        using var eleventh = ValidJpegPart();
        var rejected = await client.PostAsync("/api/uploads/event-image", eleventh);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }
}

/// <summary>Shape of the 200 response: { "imageUrl": "…" }.</summary>
public class ImageUploadResponse
{
    public string ImageUrl { get; set; } = string.Empty;
}

/// <summary>
/// WAF host for the upload endpoint tests: in-memory database, frozen clock,
/// real auth via /api/auth/login, and the R2 transport replaced by a recording
/// mock (no real Cloudflare calls). Mutates process-global env vars → serialized
/// via the EnvConfigTests collection.
/// </summary>
public class UploadsApiFactory : WebApplicationFactory<Program>
{
    public FakeTimeProvider Clock { get; } = new();

    /// <summary>Recording mock shared with the tests to assert R2 call behavior.</summary>
    public Mock<IR2StorageClient> R2Mock { get; } = new();

    private readonly string _dbName = Guid.NewGuid().ToString("N");
    private readonly Dictionary<string, string?> _originalValues = new();
    private readonly Dictionary<Guid, (string Email, string Password)> _users = new();

    public UploadsApiFactory()
    {
        Clock.SetUtcNow(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var backendRoot = Path.GetFullPath(
            Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!,
                "..", "..", ".."));
        builder.UseContentRoot(backendRoot);
        builder.UseEnvironment("Development");

        SetConfigEnvVar("Resend__ApiKey", "test-resend-api-key");
        SetConfigEnvVar("Resend__FromEmail", "tickets@example.com");
        SetConfigEnvVar("CloudflareR2__AccessKey", "test-r2-access-key");
        SetConfigEnvVar("CloudflareR2__SecretKey", "test-r2-secret-key");
        SetConfigEnvVar("CloudflareR2__ServiceUrl", "https://test-account.r2.cloudflarestorage.com");
        SetConfigEnvVar("CloudflareR2__BucketName", "test-bucket");
        SetConfigEnvVar("CloudflareR2__PublicUrl", "https://test.r2.dev");
        SetConfigEnvVar("Jwt__SecretKey", "ThisIsAVerySecureSecretKeyForTestingPurposesOnly123456789");

        builder.ConfigureServices(services =>
        {
            // Background services try to connect to the real database; remove them.
            var hostedServices = services.Where(d => d.ServiceType == typeof(IHostedService)).ToList();
            foreach (var hosted in hostedServices)
            {
                services.Remove(hosted);
            }

            // Replace the Npgsql DbContext with an isolated InMemory database.
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options
                .UseInMemoryDatabase(_dbName)
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)));

            // Replace the real R2 transport with the recording mock (EIM-002's
            // "no object is uploaded" assertions depend on it).
            services.RemoveAll<IR2StorageClient>();
            services.AddSingleton<IR2StorageClient>(R2Mock.Object);

            // Replace TimeProvider.System with the frozen fake clock (ADR-3).
            var tpDescriptor = services.Single(d => d.ServiceType == typeof(TimeProvider));
            services.Remove(tpDescriptor);
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    private void SetConfigEnvVar(string name, string value)
    {
        _originalValues[name] = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    protected override void Dispose(bool disposing)
    {
        foreach (var (name, original) in _originalValues)
        {
            Environment.SetEnvironmentVariable(name, original);
        }

        base.Dispose(disposing);
    }

    public Guid SeedOrganizer() => SeedUser(UserRole.Organizador);

    public Guid SeedStaff() => SeedUser(UserRole.Staff);

    public Guid SeedAdmin() => SeedUser(UserRole.Admin);

    private Guid SeedUser(UserRole role)
    {
        var email = $"seed-{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@test.com";
        using var scope = Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var result = authService.CreateUserAsync($"Seed {role}", email, "password123", role).GetAwaiter().GetResult();
        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to seed {role} user: {result.Error}");
        }

        _users[result.UserId] = (email, "password123");
        return result.UserId;
    }

    public Guid SeedEvent(string name, DateTime date, Guid organizerId)
    {
        var id = Guid.NewGuid();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Events.Add(new Event
        {
            Id = id,
            Name = name,
            Description = "Description",
            Date = date,
            Location = "Venue",
            OrganizerId = organizerId,
            CreatedAt = date,
            UpdatedAt = date,
            Status = EventStatus.Approved
        });
        db.SaveChanges();
        return id;
    }

    public async Task<string> LoginAndGetCookieAsync(Guid userId)
    {
        var (email, password) = _users[userId];
        using var loginClient = CreateClient();
        var response = await loginClient.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();

        var setCookieHeaders = response.Headers.GetValues("Set-Cookie");
        var tokenCookie = setCookieHeaders.First(c => c.StartsWith("token=", StringComparison.OrdinalIgnoreCase));
        return tokenCookie.Split(';')[0]; // "token=value" — added manually as the Cookie header
    }

    public HttpClient CreateClientWithCookie(string cookie)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        return client;
    }
}