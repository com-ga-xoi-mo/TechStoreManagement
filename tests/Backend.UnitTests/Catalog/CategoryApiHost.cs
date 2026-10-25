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
using TechStore.Api.Catalog;
using TechStore.Api.Catalog.Services;
using TechStore.Api.Identity.Authentication;

namespace TechStore.UnitTests.Catalog;

public sealed class CategoryApiHost : IAsyncDisposable
{
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(64);
    private readonly string _issuer = "category-tests";
    private readonly string _audience = "category-tests-client";
    public WebApplication App { get; }
    public HttpClient Client { get; private set; } = null!;

    public CategoryApiHost(Action<IServiceCollection> registerServices, string? invalidSetting = null, string invalidValue = "")
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        var configuration = new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = _issuer, ["Jwt:Audience"] = _audience,
            ["Jwt:SigningKey"] = Convert.ToBase64String(_key)
        };
        if (invalidSetting is not null) configuration[invalidSetting] = invalidValue;
        builder.Configuration.AddInMemoryCollection(configuration);
        builder.Services.AddControllers().AddApplicationPart(typeof(CategoriesController).Assembly);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddStoreAuthentication(builder.Configuration);
        registerServices(builder.Services);
        App = builder.Build();
        App.UseSwagger();
        App.UseAuthentication();
        App.UseAuthorization();
        App.MapControllers();
    }

    public async Task StartAsync()
    {
        await App.StartAsync();
        Client = App.GetTestClient();
    }

    public string Token(string role = "Admin", string kind = "valid")
    {
        var key = kind == "signature" ? RandomNumberGenerator.GetBytes(64) : _key;
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: kind == "issuer" ? "wrong-issuer" : _issuer,
            audience: kind == "audience" ? "wrong-audience" : _audience,
            claims: [new Claim("sub", Guid.NewGuid().ToString()), new Claim("role", role)],
            notBefore: now.AddHours(-2),
            expires: kind == "expired" ? now.AddHours(-1) : now.AddMinutes(10),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(Convert.ToBase64String(key))), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        await App.DisposeAsync();
    }
}
