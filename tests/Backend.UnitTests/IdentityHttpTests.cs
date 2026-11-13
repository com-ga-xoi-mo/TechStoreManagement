using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TechStore.Api.Catalog.Repositories;
using TechStore.Api.Customers.Repositories;
using TechStore.Api.Identity.Configurations;
using TechStore.Api.Identity.Repositories;
using TechStore.Api.Identity.Services;
using TechStore.Api.Inventory.Services;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Orders.Repositories;
using TechStore.Api.Orders.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

public sealed class IdentityHttpTests
{
    [Theory]
    [InlineData("GET", "/api/orders")]
    [InlineData("GET", "/api/orders/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/orders")]
    [InlineData("PATCH", "/api/orders/00000000-0000-0000-0000-000000000001/status")]
    [InlineData("GET", "/api/test-auth-fallback")]
    [InlineData("GET", "/api/products")]
    public async Task ProtectedEndpoint_NoToken_ReturnsSafe401(string method, string route)
    {
        await using var factory = new IdentityApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), route) { Content = JsonContent.Create(new { }) });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.Single().Scheme);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(401, problem!.Status);
        Assert.DoesNotContain("stack", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("missing-sub")]
    [InlineData("invalid-sub")]
    [InlineData("empty-sub")]
    [InlineData("unknown-sub")]
    [InlineData("conflicting-sub")]
    [InlineData("missing-version")]
    [InlineData("old-version")]
    [InlineData("expired")]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    public async Task ProtectedEndpoint_InvalidIdentity_Returns401(string kind)
    {
        await using var factory = new IdentityApiFactory();
        using var client = factory.CreateClient();
        var user = factory.Admin;
        var jwt = factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var claims = new List<Claim> { new("role", "Admin") };
        if (kind != "missing-sub") claims.Add(new("sub", kind switch
        {
            "invalid-sub" => "invalid", "empty-sub" => Guid.Empty.ToString(), "unknown-sub" => Guid.NewGuid().ToString(), _ => user.Id.ToString()
        }));
        if (kind == "conflicting-sub") claims.Add(new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        if (kind != "missing-version") claims.Add(new("token_version", kind == "old-version" ? "999" : "0"));
        var token = new JwtSecurityToken(kind == "issuer" ? "wrong" : jwt.Issuer, kind == "audience" ? "wrong" : jwt.Audience,
            claims, DateTime.UtcNow.AddHours(-2), kind == "expired" ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(kind == "signature" ? new string('x', 64) : jwt.SecretKey)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/orders")).StatusCode);
    }

    [Fact]
    public async Task PublicExceptions_WithoutAccessToken_RemainReachable()
    {
        await using var factory = new IdentityApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/settings/store")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = "unknown", Password = "wrong" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.LoginAsync(client)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/test-auth-fallback")).StatusCode);
    }

    [Fact]
    public async Task RoleMatrix_WarehouseAndSales_CannotReachRestrictedActions()
    {
        await using var factory = new IdentityApiFactory();
        using var client = factory.CreateClient();
        foreach (var role in new[] { RoleType.WarehouseStaff, RoleType.SalesStaff, RoleType.Manager })
        {
            var user = IdentityMigrationPostgresTests.NewAdmin();
            user.Username = role.ToString();
            user.UserRoles.Single().Role.Name = role;
            factory.Users.Add(user);
            await factory.LoginAsync(client, user.Username);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/analytics/dashboard")).StatusCode);
            if (role == RoleType.WarehouseStaff)
            {
                Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/orders")).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/products/" + Guid.NewGuid(), new { })).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/users", new CreateUserRequest())).StatusCode);
            }
            if (role == RoleType.Manager)
                Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest())).StatusCode);
        }
    }

    [Fact]
    public async Task Accounts_AdminFlow_RevokesOldTokensAndPreservesLastAdmin()
    {
        await using var factory = new IdentityApiFactory();
        using var admin = factory.CreateClient();
        await factory.LoginAsync(admin);
        var create = await admin.PostAsJsonAsync("/api/users", new CreateUserRequest
        {
            Username = "employee", FullName = "Nhân viên", Email = "employee@test.vn", Password = "Employee@123", Role = "Manager"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotNull(create.Headers.Location);
        var account = (await create.Content.ReadFromJsonAsync<UserAccountDto>())!;
        Assert.DoesNotContain("password", await create.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        using var employee = factory.CreateClient();
        await factory.LoginAsync(employee, "employee", "Employee@123");
        var old = employee.DefaultRequestHeaders.Authorization;
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/users/{account.Id}", new UpdateUserRequest { FullName = "Tên mới", Email = "changed@test.vn" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/users/{account.Id}/role", new UpdateUserRoleRequest { Role = "SalesStaff" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await employee.GetAsync("/api/auth/me")).StatusCode);
        await factory.LoginAsync(employee, "employee", "Employee@123");
        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/users/{account.Id}/status", new UpdateUserStatusRequest { IsActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await employee.GetAsync("/api/auth/me")).StatusCode);
        await admin.PatchAsJsonAsync($"/api/users/{account.Id}/status", new UpdateUserStatusRequest { IsActive = true });
        employee.DefaultRequestHeaders.Authorization = old;
        Assert.Equal(HttpStatusCode.Unauthorized, (await employee.GetAsync("/api/auth/me")).StatusCode);
        await factory.LoginAsync(employee, "employee", "Employee@123");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/users/{account.Id}/reset-password", new ResetUserPasswordRequest { Password = "NewPassword@123" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await employee.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.LoginAsync(employee, "employee", "Employee@123")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.LoginAsync(employee, "employee", "NewPassword@123")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PatchAsJsonAsync($"/api/users/{factory.Admin.Id}/status", new UpdateUserStatusRequest { IsActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/users/{factory.Admin.Id}/role", new UpdateUserRoleRequest { Role = "Manager" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await employee.PostAsJsonAsync("/api/auth/logout", new LogoutRequest())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await employee.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Orders_SalesScopeAndAudit_AreEnforcedThroughHttp()
    {
        await using var factory = new IdentityApiFactory();
        using var client = factory.CreateClient();
        var sales = IdentityMigrationPostgresTests.NewAdmin();
        sales.Username = "sales";
        sales.UserRoles.Single().Role.Name = RoleType.SalesStaff;
        factory.Users.Add(sales);
        var own = new Order { OrderCode = "OWN", CashierUserId = sales.Id, Status = OrderStatus.Pending };
        var foreign = new Order { OrderCode = "OTHER", CashierUserId = factory.Admin.Id, Status = OrderStatus.Pending };
        factory.Orders.Orders[own.Id] = own;
        factory.Orders.Orders[foreign.Id] = foreign;
        await factory.LoginAsync(client, "sales");
        var page = await client.GetFromJsonAsync<PagedResultDto<OrderSummaryDto>>("/api/orders");
        Assert.Equal(1, page!.TotalCount);
        Assert.Equal(own.Id, Assert.Single(page.Items).Id);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/orders/{foreign.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsJsonAsync($"/api/orders/{foreign.Id}/status", new UpdateOrderStatusRequest { TargetStatus = "Cancelled" })).StatusCode);
        Assert.Equal(OrderStatus.Pending, foreign.Status);
        var product = new TechStore.Api.Catalog.Entities.Product { Name = "Item", Sku = "ITEM", BasePrice = 100, IsActive = true, IsSerialTracked = false };
        factory.Products.Products[product.Id] = product;
        var response = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { Items = [new() { ProductId = product.Id, Quantity = 1 }] });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = factory.Orders.AddedOrders.Single();
        Assert.Equal(sales.Id, created.CashierUserId);
        foreach (var item in created.Items) item.Product = product;
        var complete = await client.PatchAsJsonAsync($"/api/orders/{created.Id}/status", new UpdateOrderStatusRequest { TargetStatus = "Completed" });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        Assert.Equal(sales.Id, factory.Fulfillment.LastPerformedBy);
    }
}

public sealed class IdentityApiFactory : WebApplicationFactory<Program>
{
    public FakeUserRepository Users => Services.GetRequiredService<FakeUserRepository>();
    public TechStore.Api.Identity.Entities.User Admin => Users.Users.Values.Single(u => u.Username == "admin");
    public FakeOrderRepository Orders { get; } = new();
    public FakeProductRepository Products { get; } = new();
    public FakeInventoryFulfillmentService Fulfillment { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.AddTestIdentities();
            services.AddControllers().AddApplicationPart(typeof(UnannotatedIdentityProbeController).Assembly);
            services.RemoveAll<IStoreSettingRepository>();
            services.AddSingleton<IStoreSettingRepository, FakeStoreSettingRepository>();
            services.RemoveAll<IOrderRepository>();
            services.AddSingleton<IOrderRepository>(Orders);
            services.RemoveAll<IProductRepository>();
            services.AddSingleton<IProductRepository>(Products);
            services.RemoveAll<ICustomerRepository>();
            services.AddSingleton<ICustomerRepository, FakeCustomerRepository>();
            services.RemoveAll<IInventoryFulfillmentService>();
            services.AddSingleton<IInventoryFulfillmentService>(Fulfillment);
            services.AddSingleton<IStartupFilter>(new IdentityStartupFilter());
        });
    }

    public async Task<HttpResponseMessage> LoginAsync(HttpClient client, string username = "admin", string password = "Admin@123")
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = username, Password = password });
        if (response.IsSuccessStatusCode)
        {
            var pair = (await response.Content.ReadFromJsonAsync<LoginResponseDto>())!;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pair.AccessToken);
        }
        return response;
    }

    private sealed class IdentityStartupFilter : IStartupFilter
    {
        public Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> Configure(Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> next) => app =>
        {
            app.ApplicationServices.GetRequiredService<FakeUserRepository>().Add(IdentityMigrationPostgresTests.NewAdmin());
            next(app);
        };
    }
}

[ApiController]
[Route("api/test-auth-fallback")]
public sealed class UnannotatedIdentityProbeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { authenticated = true });
}
