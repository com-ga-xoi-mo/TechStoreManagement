using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using TechStore.Api.Analytics;
using TechStore.Api.Analytics.Services;
using TechStore.Shared.DTOs;

namespace TechStore.UnitTests;

public sealed class AnalyticsControllerTests
{
    [Fact]
    public async Task HttpPipeline_EnforcesAuthenticationAndAdminRole()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(AnalyticsController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, StubAuthHandler>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAnalyticsService, FakeAnalyticsService>();
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var anonymous = await client.GetAsync("/api/analytics/dashboard");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var invalid = new HttpRequestMessage(HttpMethod.Get, "/api/analytics/dashboard");
        invalid.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid");
        var invalidToken = await client.SendAsync(invalid);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, invalidToken.StatusCode);

        using var manager = new HttpRequestMessage(HttpMethod.Get, "/api/analytics/dashboard");
        manager.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "Manager");
        var forbidden = await client.SendAsync(manager);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var admin = new HttpRequestMessage(HttpMethod.Get, "/api/analytics/dashboard");
        admin.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "Admin");
        var accepted = await client.SendAsync(admin);
        Assert.Equal(System.Net.HttpStatusCode.OK, accepted.StatusCode);
        var body = await accepted.Content.ReadAsStringAsync();
        Assert.Contains("last7Days", body, StringComparison.Ordinal);

        using var badPeriod = new HttpRequestMessage(HttpMethod.Get, "/api/analytics/dashboard?period=week");
        badPeriod.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "Admin");
        var badRequest = await client.SendAsync(badPeriod);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public void Controller_RequiresAdminRole()
    {
        var authorization = Assert.Single(typeof(AnalyticsController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));
        Assert.Equal("Admin", ((AuthorizeAttribute)authorization).Roles);
    }

    [Fact]
    public async Task GetDashboard_WhenPeriodOmitted_UsesLastSevenDays()
    {
        var service = new FakeAnalyticsService();
        var controller = new AnalyticsController(service);

        var action = await controller.GetDashboard();

        Assert.IsType<OkObjectResult>(action.Result);
        Assert.Equal("last7Days", service.Period);
    }

    [Fact]
    public async Task GetDashboard_WhenPeriodInvalid_ReturnsValidationProblemWithoutCallingService()
    {
        var service = new FakeAnalyticsService();
        var controller = new AnalyticsController(service);

        var action = await controller.GetDashboard("week");

        var result = Assert.IsAssignableFrom<ObjectResult>(action.Result);
        Assert.Equal(400, Assert.IsAssignableFrom<ProblemDetails>(result.Value).Status);
        Assert.Null(service.Period);
    }

    private sealed class FakeAnalyticsService : IAnalyticsService
    {
        public string? Period { get; private set; }

        public Task<AdminDashboardDto> GetDashboardAsync(string period, CancellationToken cancellationToken = default)
        {
            Period = period;
            return Task.FromResult(new AdminDashboardDto(period, DateOnly.MinValue, DateOnly.MinValue,
                "UTC+07:00", DateTime.UnixEpoch, new(0m, 0, 0, 0, null, null), [], [], []));
        }
    }

    private sealed class StubAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var authorization = Request.Headers.Authorization.ToString();
            if (string.IsNullOrEmpty(authorization)) return Task.FromResult(AuthenticateResult.NoResult());
            if (!authorization.StartsWith("Bearer ", StringComparison.Ordinal))
                return Task.FromResult(AuthenticateResult.Fail("Invalid authorization header."));

            var role = authorization[7..];
            if (role is not ("Admin" or "Manager"))
                return Task.FromResult(AuthenticateResult.Fail("Invalid test token."));

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "test-user"), new Claim(ClaimTypes.Role, role)], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
