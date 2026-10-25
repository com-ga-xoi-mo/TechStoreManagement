using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using TechStore.Api.Inventory;

namespace TechStore.UnitTests.Inventory;

public sealed class InventoryApiHost : IAsyncDisposable
{
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(64);
    private readonly string _issuer = "inventory-tests";
    private readonly string _audience = "inventory-tests-client";

    public WebApplication App { get; }
    public HttpClient Client { get; private set; } = null!;

    public InventoryApiHost(Action<IServiceCollection> registerServices, bool useDatabase = false)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = _issuer,
            ["Jwt:Audience"] = _audience,
            ["Jwt:SecretKey"] = Convert.ToBase64String(_key)
        });
        builder.Services.AddControllers().AddApplicationPart(typeof(InventoryController).Assembly);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddStoreJwtAuthentication(builder.Configuration);
        registerServices(builder.Services);
        App = builder.Build();
        App.UseAuthentication();
        App.UseAuthorization();
        App.MapControllers();
    }

    public async Task StartAsync()
    {
        await App.StartAsync();
        Client = App.GetTestClient();
    }

    public string Token(string role = "Admin", Guid? userId = null, bool includeSub = true, string? sub = null, string? nameIdentifier = null)
    {
        var claims = new List<Claim> { new("role", role) };
        if (includeSub)
        {
            claims.Add(new Claim("sub", sub ?? (userId ?? Guid.NewGuid()).ToString()));
        }

        if (nameIdentifier is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, nameIdentifier));
        }

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(10),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(Convert.ToBase64String(_key))),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public void Authenticate(string token) =>
        Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        await App.DisposeAsync();
    }
}
