using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TechStore.Api.Common.Data;
using TechStore.Api.Identity.Repositories;
using TechStore.Api.Identity.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

// Keeps production authentication and persistence; only configuration belongs to the test host.
public sealed class IdentityPostgresApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    public const string SigningKey = "Identity_HTTP_fixture_key_for_tests_only_at_least_64_characters_2026";
    private readonly string _storagePath = Path.Combine(Path.GetTempPath(), "techstore-identity-http-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        foreach (var (key, value) in new Dictionary<string, string>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString,
            ["Jwt:SecretKey"] = SigningKey,
            ["Jwt:Issuer"] = "TechStore.Identity.HttpTests",
            ["Jwt:Audience"] = "TechStore.Identity.HttpTests.Client",
            ["ObjectStorage:Provider"] = "Local",
            ["ObjectStorage:Local:RootPath"] = _storagePath
        }) builder.UseSetting(key, value);
    }

    public HttpClient NewClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
    });

    public override async ValueTask DisposeAsync()
    {
        try { await base.DisposeAsync(); }
        finally { if (Directory.Exists(_storagePath)) Directory.Delete(_storagePath, recursive: true); }
    }
}

public sealed class IdentityPostgresApi : IAsyncDisposable
{
    public PostgresDatabase Database { get; }
    public IdentityPostgresApiFactory Factory { get; }

    private IdentityPostgresApi(PostgresDatabase database)
    {
        Database = database;
        Factory = new IdentityPostgresApiFactory(database.ConnectionString);
    }

    public static async Task<IdentityPostgresApi> CreateAsync()
    {
        var api = new IdentityPostgresApi(await PostgresDatabase.CreateAsync(migrate: true));
        try
        {
            using var scope = api.Factory.Services.CreateScope();
            var services = scope.ServiceProvider;
            var db = services.GetRequiredService<AppDbContext>();
            Assert.True(db.Database.IsNpgsql());
            // Do not print connection strings if isolation fails.
            Assert.True(db.Database.GetConnectionString() == api.Database.ConnectionString, "Host must use its generated database.");
            Assert.IsType<UserRepository>(services.GetRequiredService<IUserRepository>());
            Assert.IsType<RefreshTokenRepository>(services.GetRequiredService<IRefreshTokenRepository>());
            Assert.IsType<UnitOfWork>(services.GetRequiredService<IUnitOfWork>());
            Assert.IsType<AuthService>(services.GetRequiredService<IAuthService>());
            Assert.IsType<UserManagementService>(services.GetRequiredService<IUserManagementService>());
            await services.GetRequiredService<IDataSeeder>().SeedAsync();
            return api;
        }
        catch
        {
            await api.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try { await Factory.DisposeAsync(); }
        finally { await Database.DisposeAsync(); }
    }

    public static async Task<LoginResponseDto> LoginAsync(HttpClient client, string username = "admin", string password = "Admin@123")
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = username, Password = password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = (await response.Content.ReadFromJsonAsync<LoginResponseDto>())!;
        Assert.NotNull(login.User);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(login.RefreshToken));
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        return login;
    }

    public static async Task<UserAccountDto> CreateUserAsync(HttpClient admin, string role)
    {
        using var response = await admin.PostAsJsonAsync("/api/users", new CreateUserRequest
        {
            Username = "http_" + Guid.NewGuid().ToString("N")[..12], FullName = "HTTP acceptance",
            Email = Guid.NewGuid().ToString("N") + "@test.local", Password = "HttpEmployee@123", Role = role
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        await AssertNoSecretsAsync(response);
        return (await response.Content.ReadFromJsonAsync<UserAccountDto>())!;
    }

    public static async Task AssertNoSecretsAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Check(json.RootElement);
        static void Check(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
                foreach (var property in element.EnumerateObject())
                {
                    var key = property.Name.ToLowerInvariant();
                    Assert.False(key.Contains("password") || key.Contains("token") || key.Contains("secret") || key.Contains("connectionstring"));
                    Check(property.Value);
                }
            else if (element.ValueKind == JsonValueKind.Array)
                foreach (var item in element.EnumerateArray()) Check(item);
        }
    }

    public static async Task AssertSafeFailureAsync(HttpResponseMessage response, HttpStatusCode expected, params string[] secrets)
    {
        using (response)
        {
            Assert.Equal(expected, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var body = await response.Content.ReadAsStringAsync();
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal((int)expected, problem!.Status);
            if (expected == HttpStatusCode.Unauthorized)
                Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
            foreach (var forbidden in new[] { "stackTrace", "passwordHash", "token_version", "connectionString", SigningKeyMarker }.Concat(secrets))
                Assert.True(!body.Contains(forbidden, StringComparison.OrdinalIgnoreCase), "Failure body must not expose sensitive values.");
        }
    }

    private const string SigningKeyMarker = IdentityPostgresApiFactory.SigningKey;
}
