using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using TechStore.Client.Models.Analytics;
using TechStore.Client.Repositories.Analytics;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Development;
using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Dashboard;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.IntegrationTests;

[Collection("Live API database")]
public sealed class FinancialReportApiIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly FinancialReportRange DemoRange = new(new(2026, 10, 15), new(2026, 10, 21));

    [OptInFact]
    public async Task LiveReport_ClientMatchesTheSameHttpSnapshotAndGuardsEachRole()
    {
        var options = new ApiClientOptions { BaseUrl = Environment.GetEnvironmentVariable(ApiClientOptions.BaseUrlEnvironmentVariable)! };
        var baseUri = options.Validate();
        var accounts = new[]
        {
            ("admin", "Admin@123", true), ("manager1", "Manager@123", true),
            ("cashier1", "Cashier@123", false), ("warehouse1", "Warehouse@123", false)
        };

        foreach (var (username, password, allowed) in accounts)
        {
            var session = new InMemorySessionStore();
            using var capture = new CaptureReportResponse();
            using var http = new HttpClient(new BearerTokenHandler(session) { InnerHandler = capture });
            using var loginResponse = await http.PostAsJsonAsync(new Uri(baseUri, "api/auth/login"),
                new LoginRequest { Username = username, Password = password, RememberMe = false });
            loginResponse.EnsureSuccessStatusCode();
            var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
            Assert.NotNull(login);
            Assert.True(session.TrySetSession(session.Current.Generation, login, rememberMe: false));

            var repository = new AnalyticsApiRepository(new ApiClient(http, options));
            var dashboard = new DashboardViewModel(repository, new DashboardPreviewOptions(false), session, repository, TimeProvider.System);
            try
            {
                await dashboard.ActivateAsync();
                var report = dashboard.FinancialReport;
                Assert.True(report.ShowOverview);
                Assert.Equal(allowed, report.IsAvailable);
                Assert.Null(capture.Payload);
                await report.OpenCommand.ExecuteAsync(null);
                if (!allowed)
                {
                    Assert.Null(capture.Payload);
                    Assert.True(report.ShowOverview);
                    var denied = await Assert.ThrowsAsync<ApiException>(() => repository.GetFinancialReportAsync(DemoRange));
                    Assert.Equal((int)HttpStatusCode.Forbidden, denied.StatusCode);
                    Assert.Equal(SessionStatus.Active, session.Current.Status);
                    continue;
                }

                Assert.True(dashboard.IsContent, dashboard.ErrorMessage);
                report.From = "15/10/2026";
                report.To = "21/10/2026";
                await report.ApplyCommand.ExecuteAsync(null);
                var snapshot = report.Snapshot;
                Assert.True(snapshot is not null, report.ErrorMessage);
                var wire = Assert.IsType<JsonObject>(capture.Payload);
                Assert.Equal(DemoRange, snapshot!.Range);
                Assert.Equal(DemoRange.From, wire["startDate"]!.Deserialize<DateOnly>());
                Assert.Equal(DemoRange.To, wire["endDate"]!.Deserialize<DateOnly>());
                Assert.Equal(wire["asOf"]!.Deserialize<DateTimeOffset>(), snapshot.AsOf);
                Assert.Equal(7, snapshot.Daily.Count);
                Assert.True(snapshot.Summary.SalesExcludingTax > 0, "Expected the seeded demo sales in October 2026.");
                Assert.NotEmpty(snapshot.TopProductsByRevenue);
                Assert.NotEmpty(snapshot.CashByShift!);
                AssertMatches(wire, "summary", snapshot.Summary);
                AssertMatches(wire, "daily", snapshot.Daily);
                AssertMatches(wire, "topProductsByQuantity", snapshot.TopProductsByQuantity);
                AssertMatches(wire, "topProductsByRevenue", snapshot.TopProductsByRevenue);
                AssertMatches(wire, "cashierPerformance", snapshot.CashierPerformance);
                AssertMatches(wire, "cashByShift", snapshot.CashByShift);
                Assert.Equal(snapshot.Daily.Count, report.Presentation!.Daily.Count);
                Assert.Contains("15/10/2026–21/10/2026", report.Presentation.Reference);
                Assert.Contains($"{snapshot.AsOf.ToOffset(TimeSpan.FromHours(7)):HH:mm} (UTC+07)", report.Presentation.Reference);
            }
            finally { dashboard.Deactivate(); }
        }
    }

    private static void AssertMatches<T>(JsonObject wire, string property, T value) =>
        Assert.True(JsonNode.DeepEquals(wire[property], JsonSerializer.SerializeToNode(value, JsonOptions)),
            $"Client value differs from the response in {property}.");

    private sealed class CaptureReportResponse() : DelegatingHandler(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
    {
        public JsonNode? Payload { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (request.RequestUri?.AbsolutePath == "/api/analytics/financial-report" && response.IsSuccessStatusCode)
                Payload = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return response;
        }
    }
}
