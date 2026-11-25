using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using TechStore.Api.Analytics;
using TechStore.Api.Analytics.Services;
using TechStore.Shared.DTOs;

namespace TechStore.UnitTests;

public sealed class AnalyticsControllerTests
{
    [Fact]
    public async Task HttpPipeline_EnforcesAuthenticationAndDashboardRoles()
    {
        await using var app = await CreateAppAsync(new());
        using var client = app.GetTestClient();

        var anonymous = await client.GetAsync("/api/analytics/dashboard");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var invalid = new HttpRequestMessage(HttpMethod.Get, "/api/analytics/dashboard");
        invalid.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid");
        var invalidToken = await client.SendAsync(invalid);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, invalidToken.StatusCode);

        foreach (var role in new[] { "Admin", "Manager", "SalesStaff", "WarehouseStaff" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/analytics/dashboard");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", role);
            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (role is "Admin" or "Manager")
            {
                Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
                Assert.Contains("last7Days", body, StringComparison.Ordinal);
            }
            else
            {
                Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
                Assert.DoesNotContain("dailyRevenue", body, StringComparison.Ordinal);
            }
        }

        using var badPeriod = new HttpRequestMessage(HttpMethod.Get, "/api/analytics/dashboard?period=week");
        badPeriod.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "Admin");
        var badRequest = await client.SendAsync(badPeriod);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public void Controller_RequiresAdminOrManagerRole()
    {
        var authorization = Assert.Single(typeof(AnalyticsController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));
        Assert.Equal("Admin,Manager", ((AuthorizeAttribute)authorization).Roles);
    }

    [Theory]
    [InlineData("", "last7Days")]
    [InlineData("?period=today", "today")]
    [InlineData("?period=last7Days", "last7Days")]
    [InlineData("?period=thisMonth", "thisMonth")]
    [InlineData("?period=custom&from=2026-10-01&to=2026-10-31", "custom", "2026-10-01", "2026-10-31")]
    [InlineData("?period=custom&from=2026-10-18&to=2026-10-18", "custom", "2026-10-18", "2026-10-18")]
    public async Task GetDashboard_WhenQueryValid_PassesTypedPeriodToService(
        string queryString, string period, string? from = null, string? to = null)
    {
        var service = new FakeAnalyticsService();
        await using var app = await CreateAppAsync(service);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "Manager");

        using var action = await client.GetAsync("/api/analytics/dashboard" + queryString);

        action.EnsureSuccessStatusCode();
        Assert.NotNull(service.Query);
        Assert.Equal(period, service.Query.Period);
        if (from is null || to is null)
            Assert.Null(service.Query.Range);
        else
            Assert.Equal((DateOnly.Parse(from), DateOnly.Parse(to)), service.Query.Range);
    }

    [Theory]
    [InlineData("?period=week", "period")]
    [InlineData("?period=custom&to=2026-10-18", "from")]
    [InlineData("?period=custom&from=2026-10-18", "to")]
    [InlineData("?period=custom&from=&to=2026-10-18", "from")]
    [InlineData("?period=custom&from=2026-10-18&to=2026-10-18&to=2026-10-19", "to")]
    [InlineData("?period=custom&from=2026-10-18&from=2026-10-18&to=2026-10-18", "from")]
    [InlineData("?period=custom&from=18-10-2026&to=2026-10-18", "from")]
    [InlineData("?period=custom&from=2026-10-18&to=2026-10-32", "to")]
    [InlineData("?period=custom&from=2026-10-18&to=2026-10-17", "to")]
    [InlineData("?period=custom&from=2026-10-01&to=2026-11-01", "to")]
    [InlineData("?period=today&from=", "period")]
    [InlineData("?to=2026-10-18", "period")]
    public async Task GetDashboard_WhenQueryInvalid_ReturnsValidationProblemWithoutCallingService(string query, string field)
    {
        var service = new FakeAnalyticsService();
        await using var app = await CreateAppAsync(service);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "Admin");

        using var action = await client.GetAsync("/api/analytics/dashboard" + query);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, action.StatusCode);
        var problem = await action.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(400, problem?.Status);
        Assert.Contains(field, problem!.Errors.Keys);
        Assert.Null(service.Query);
    }

    [Fact]
    public async Task GetDashboard_WhenSnapshotRejectsRange_ReturnsFieldValidationProblem()
    {
        var service = new FakeAnalyticsService { Error = DashboardAnalyticsResult.Invalid("to", "End date is in the future.") };
        await using var app = await CreateAppAsync(service);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "Manager");

        using var action = await client.GetAsync("/api/analytics/dashboard?period=custom&from=2090-01-01&to=2090-01-02");

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, action.StatusCode);
        Assert.Contains("to", (await action.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.Keys);
    }

    private static async Task<WebApplication> CreateAppAsync(FakeAnalyticsService service)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(AnalyticsController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, StubAuthHandler>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAnalyticsService>(service);
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    private sealed class FakeAnalyticsService : IAnalyticsService
    {
        public DashboardAnalyticsQuery? Query { get; private set; }
        public DashboardAnalyticsResult? Error { get; init; }

        public Task<FinancialReportResult> GetFinancialReportAsync(
            FinancialReportQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DashboardAnalyticsResult> GetDashboardAsync(DashboardAnalyticsQuery query, CancellationToken cancellationToken = default)
        {
            Query = query;
            return Task.FromResult(Error ?? DashboardAnalyticsResult.Success(new AdminDashboardDto(
                query.Period, query.Range?.From ?? DateOnly.MinValue, query.Range?.To ?? DateOnly.MinValue,
                "UTC+07:00", DateTime.UnixEpoch, new(0m, 0, 0, 0, null, null), [], [], [])));
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
            if (role is not ("Admin" or "Manager" or "SalesStaff" or "WarehouseStaff"))
                return Task.FromResult(AuthenticateResult.Fail("Invalid test token."));

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "test-user"), new Claim(ClaimTypes.Role, role)], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
