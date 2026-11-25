using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;
using Xunit.Abstractions;

namespace TechStore.UnitTests;

public sealed class ApiAuthorizationCoverageTests(ITestOutputHelper output)
{
    private const string All = "Admin,Manager,SalesStaff,WarehouseStaff";
    private const string CatalogWrite = "Admin,Manager";
    private const string StockWrite = "Admin,WarehouseStaff";
    private const string OrderRead = "Admin,Manager,SalesStaff";
    private const string OrderWrite = "Admin,SalesStaff";
    private const string CustomerServe = "Admin,Manager,SalesStaff";
    private const string CustomerManage = "Admin,Manager";
    private const string PurchaseRead = "Admin,Manager,WarehouseStaff";

    // Independent expectations from docs/API_PERMISSIONS.md, not generated from controller attributes.
    private static readonly EndpointCase[] Baseline =
    [
        new("POST", "api/auth/login", null), new("POST", "api/auth/refresh", null),
        new("GET", "api/health", null), new("GET", "api/settings/store", null),
        new("GET", "api/auth/me", All), new("POST", "api/auth/logout", All),
        new("PUT", "api/settings/store", CatalogWrite),
        new("GET", "api/products", All), new("GET", "api/products/{id:guid}", All),
        new("GET", "api/products/spec-keys", All), new("GET", "api/products/brands", All),
        new("GET", "api/products/spec-definitions", All), new("POST", "api/products", CatalogWrite),
        new("PUT", "api/products/{id:guid}", CatalogWrite), new("PATCH", "api/products/{id:guid}/status", CatalogWrite),
        new("DELETE", "api/products/{id:guid}", CatalogWrite),
        new("POST", "api/products/{productid:guid}/images", CatalogWrite),
        new("DELETE", "api/products/{productid:guid}/images/{imageid:guid}", CatalogWrite),
        new("PUT", "api/products/{productid:guid}/images/layout", CatalogWrite),
        new("GET", "api/categories/tree", All), new("POST", "api/categories", "Admin"),
        new("PUT", "api/categories/{id:guid}", "Admin"), new("DELETE", "api/categories/{id:guid}", "Admin"),
        new("GET", "api/inventory/stocks", All), new("GET", "api/inventory/stocks/{stockid:guid}/detail", All),
        new("GET", "api/inventory/serials", All), new("POST", "api/inventory/stocks/initialize", StockWrite),
        new("POST", "api/inventory/stocks/{stockid:guid}/adjust", StockWrite),
        new("DELETE", "api/inventory/stocks/{stockid:guid}", StockWrite),
        new("POST", "api/inventory/serials/batch-import", StockWrite),
        new("GET", "api/inventory/suppliers", PurchaseRead), new("GET", "api/inventory/suppliers/{id:guid}", PurchaseRead),
        new("POST", "api/inventory/suppliers", StockWrite), new("PUT", "api/inventory/suppliers/{id:guid}", StockWrite),
        new("PATCH", "api/inventory/suppliers/{id:guid}/status", StockWrite),
        new("GET", "api/inventory/purchase-receipts", PurchaseRead),
        new("GET", "api/inventory/purchase-receipts/{id:guid}", PurchaseRead),
        new("POST", "api/inventory/purchase-receipts", StockWrite),
        new("POST", "api/inventory/purchase-receipts/{id:guid}/deliveries", StockWrite),
        new("POST", "api/inventory/purchase-receipts/{id:guid}/cancel-remainder", StockWrite),
        new("POST", "api/inventory/serials/{id:guid}/defective", StockWrite),
        new("POST", "api/inventory/serials/{id:guid}/restore", StockWrite),
        new("GET", "api/orders", OrderRead), new("GET", "api/orders/{id:guid}", OrderRead),
        new("POST", "api/orders", OrderWrite), new("PATCH", "api/orders/{id:guid}/status", OrderWrite),
        new("GET", "api/warranties/lookup", All),
        new("GET", "api/warranties", All),
        new("GET", "api/warranties/{caseid:guid}", All),
        new("POST", "api/warranties/{caseid:guid}/receive", "Admin,Manager,SalesStaff"),
        new("POST", "api/warranties/{caseid:guid}/return", "Admin,Manager,SalesStaff"),
        new("POST", "api/warranties/{caseid:guid}/inspect", StockWrite),
        new("POST", "api/warranties/{caseid:guid}/repaired", StockWrite),
        new("POST", "api/warranties/{caseid:guid}/replace", "Admin,Manager"),
        new("POST", "api/warranties/{caseid:guid}/reject-replacement", "Admin,Manager"),
        new("GET", "api/returns/order/{orderid:guid}", OrderRead), new("GET", "api/returns/{id:guid}", OrderRead),
        new("POST", "api/returns", OrderRead), new("POST", "api/returns/{id:guid}/settle", "Admin,Manager"),
        new("POST", "api/returns/{id:guid}/reconcile", "Admin,Manager"), new("POST", "api/returns/{id:guid}/exchange", "Admin,Manager"),
        new("GET", "api/inventory/returns", StockWrite), new("POST", "api/inventory/returns/{returnlineid:guid}/inspections", StockWrite),
        new("GET", "api/customers", CustomerServe), new("GET", "api/customers/{id:guid}", CustomerServe),
        new("GET", "api/customers/{id:guid}/loyalty-summary", CustomerServe),
        new("GET", "api/vouchers", CustomerManage), new("GET", "api/vouchers/{id:guid}", CustomerManage),
        new("POST", "api/vouchers", CustomerManage), new("PUT", "api/vouchers/{id:guid}", CustomerManage),
        new("PATCH", "api/vouchers/{id:guid}/activate", CustomerManage),
        new("PATCH", "api/vouchers/{id:guid}/deactivate", CustomerManage),
        new("GET", "api/customers/{id:guid}/loyalty-points", CustomerManage),
        new("GET", "api/customers/{id:guid}/orders", CustomerManage),
        new("GET", "api/customers/{id:guid}/purchased-items", CustomerManage),
        new("POST", "api/customers", CustomerServe), new("PUT", "api/customers/{id:guid}", CustomerManage),
        new("PATCH", "api/customers/{id:guid}/deactivate", CustomerManage),
        new("PATCH", "api/customers/{id:guid}/activate", CustomerManage),
        new("GET", "api/analytics/dashboard", "Admin,Manager"),
        new("GET", "api/analytics/financial-report", "Admin,Manager"),
        new("GET", "api/users", "Admin"), new("GET", "api/users/{id:guid}", "Admin"),
        new("POST", "api/users", "Admin"), new("PUT", "api/users/{id:guid}", "Admin"),
        new("PATCH", "api/users/{id:guid}/status", "Admin"), new("PUT", "api/users/{id:guid}/role", "Admin"),
        new("POST", "api/users/{id:guid}/reset-password", "Admin"),
        new("POST", "api/pos/shifts", OrderRead), new("GET", "api/pos/shifts/current", OrderRead),
        new("GET", "api/pos/shifts", OrderRead), new("GET", "api/pos/shifts/{id:guid}", OrderRead),
        new("POST", "api/pos/quote", OrderWrite),
        new("POST", "api/pos/vietqr/{id:guid}/cancel", OrderWrite), new("POST", "api/pos/vietqr/{id:guid}/expire", OrderWrite),
        new("POST", "api/pos/shifts/{id:guid}/close", OrderRead), new("POST", "api/pos/checkout", OrderWrite)
    ];

    [Fact]
    public async Task RegisteredEndpoints_EffectivePolicies_MatchIndependentSprintOneBaseline()
    {
        // This check inspects routing/policies only; it never opens a database connection.
        await using var factory = new IdentityPostgresApiFactory("Host=localhost;Database=unused_policy_test;Username=unused");
        using var client = factory.NewClient();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<ControllerActionDescriptor>() is not null)
            .ToDictionary(e => Key(Assert.Single(e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods), e.RoutePattern.RawText!), e => e);
        Assert.Equal(Baseline.Select(b => b.Key).Order(), endpoints.Keys.Order());
        var provider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var authorization = factory.Services.GetRequiredService<IAuthorizationService>();
        Assert.NotNull(factory.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value.FallbackPolicy);
        foreach (var expected in Baseline)
        {
            var endpoint = endpoints[expected.Key];
            var anonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            Assert.True(anonymous == (expected.Roles is null), $"Public declaration mismatch: {expected.Key}");
            if (anonymous) continue;
            var policy = await AuthorizationPolicy.CombineAsync(provider,
                endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(), endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>());
            Assert.NotNull(policy);
            var unauthenticated = await authorization.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), null, policy);
            Assert.False(unauthenticated.Succeeded);
            foreach (var role in Enum.GetValues<RoleType>())
            {
                var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role.ToString())], "Bearer"));
                var allowed = (await authorization.AuthorizeAsync(principal, null, policy)).Succeeded;
                Assert.True(allowed == expected.Allows(role), $"Policy mismatch: {expected.Key}, role={role}, allowed={allowed}");
            }
        }
        output.WriteLine($"Exact endpoint inventory: {Baseline.Length}; public: {Baseline.Count(b => b.Roles is null)}; role policies checked for all four roles.");
    }

    [PostgresFact]
    public async Task RegisteredEndpoints_AnonymousAndDisallowedRoles_ReturnSafe401And403()
    {
        await using var api = await IdentityPostgresApi.CreateAsync();
        using var client = api.Factory.NewClient();
        var challenges = 0;
        var forbidden = 0;
        foreach (var endpoint in Baseline.Where(b => b.Roles is not null))
        {
            await CheckAsync(endpoint, HttpStatusCode.Unauthorized);
            challenges++;
        }
        foreach (var (username, password, role) in new[]
        {
            ("admin", "Admin@123", RoleType.Admin), ("manager1", "Manager@123", RoleType.Manager),
            ("cashier1", "Cashier@123", RoleType.SalesStaff), ("warehouse1", "Warehouse@123", RoleType.WarehouseStaff)
        })
        {
            await IdentityPostgresApi.LoginAsync(client, username, password);
            foreach (var endpoint in Baseline.Where(b => b.Roles is not null && !b.Allows(role)))
            {
                await CheckAsync(endpoint, HttpStatusCode.Forbidden);
                forbidden++;
            }
            var profile = await client.GetFromJsonAsync<UserProfileDto>("/api/auth/me");
            Assert.Equal(role, profile!.Role);
            var products = await client.GetFromJsonAsync<PagedResultDto<ProductSummaryDto>>("/api/products");
            Assert.NotEmpty(products!.Items);
            var stocks = await client.GetFromJsonAsync<PagedResultDto<InventoryStockDto>>("/api/inventory/stocks");
            Assert.NotEmpty(stocks!.Items);
            if (role != RoleType.WarehouseStaff)
                Assert.NotNull(await client.GetFromJsonAsync<PagedResultDto<OrderSummaryDto>>("/api/orders"));
        }

        client.DefaultRequestHeaders.Authorization = null;
        using var health = await client.GetAsync("/api/health");
        using var store = await client.GetAsync("/api/settings/store");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal(HttpStatusCode.OK, store.StatusCode);
        await IdentityPostgresApi.AssertNoSecretsAsync(health);
        await IdentityPostgresApi.AssertNoSecretsAsync(store);
        using var blankLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest());
        using var blankRefresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest());
        Assert.Equal(HttpStatusCode.BadRequest, blankLogin.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, blankRefresh.StatusCode);
        output.WriteLine($"HTTP exact assertions: {challenges} missing-token 401; {forbidden} disallowed-role 403; four-role positive reads; four public endpoints evaluated.");

        async Task CheckAsync(EndpointCase endpoint, HttpStatusCode expected)
        {
            var route = "/" + Regex.Replace(endpoint.Route, @"\{[^}]+\}", "00000000-0000-0000-0000-000000000001");
            using var request = new HttpRequestMessage(new HttpMethod(endpoint.Method), route)
            {
                // Consumes participates in endpoint selection before action authorization.
                Content = endpoint.Method == "POST" && endpoint.Route.EndsWith("/images", StringComparison.Ordinal)
                    ? new MultipartFormDataContent() : JsonContent.Create(new { })
            };
            var response = await client.SendAsync(request);
            Assert.True(response.StatusCode == expected, $"{endpoint.Key}: expected {(int)expected}, actual {(int)response.StatusCode}");
            await IdentityPostgresApi.AssertSafeFailureAsync(response, expected);
        }
    }

    private static string Key(string method, string route) => method.ToUpperInvariant() + " /" + route.Trim('/').ToLowerInvariant();
    private sealed record EndpointCase(string Method, string Route, string? Roles)
    {
        public string Key => ApiAuthorizationCoverageTests.Key(Method, Route);
        public bool Allows(RoleType role) => Roles is null || Roles.Split(',').Contains(role.ToString(), StringComparer.Ordinal);
    }
}
