using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using TechStore.Api.Identity.Configurations;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

public sealed class IdentityPostgresHttpTests
{
    [PostgresFact]
    public async Task Login_FourRolesAndInvalidIdentity_UsesProductionJwtAndPostgres()
    {
        await using var api = await IdentityPostgresApi.CreateAsync();
        using var client = api.Factory.NewClient();
        foreach (var (username, password, role) in new[]
        {
            ("admin", "Admin@123", RoleType.Admin), ("manager1", "Manager@123", RoleType.Manager),
            ("cashier1", "Cashier@123", RoleType.SalesStaff), ("warehouse1", "Warehouse@123", RoleType.WarehouseStaff)
        })
        {
            var login = await IdentityPostgresApi.LoginAsync(client, username, password);
            var profile = await client.GetFromJsonAsync<UserProfileDto>("/api/auth/me");
            Assert.Equal(login.User.Id, profile!.Id);
            Assert.Equal(role, profile.Role);
            Assert.True(profile.IsActive);
        }

        client.DefaultRequestHeaders.Authorization = null;
        await IdentityPostgresApi.AssertSafeFailureAsync(await client.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized);
        using var wrong = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = "admin", Password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(401, (await wrong.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>())!.Status);

        var admin = await IdentityPostgresApi.LoginAsync(client);
        var jwt = api.Factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        Assert.True(jwt.SecretKey == IdentityPostgresApiFactory.SigningKey, "Test signing settings must match production registration.");
        foreach (var kind in new[] { "valid", "expired", "signature", "missing-sub", "invalid-sub", "missing-version", "old-version" })
        {
            var claims = new List<Claim> { new("role", "Admin") };
            if (kind != "missing-sub") claims.Add(new("sub", kind == "invalid-sub" ? "invalid" : admin.User.Id.ToString()));
            if (kind != "missing-version") claims.Add(new("token_version", kind == "old-version" ? "999" : "0"));
            var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience, claims,
                DateTime.UtcNow.AddHours(-2), kind == "expired" ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddMinutes(5),
                new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(kind == "signature" ? new string('x', 64) : jwt.SecretKey)), SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
            if (kind == "valid") Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
            else await IdentityPostgresApi.AssertSafeFailureAsync(await client.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized, admin.AccessToken, admin.RefreshToken);
        }
    }

    [PostgresFact]
    public async Task RefreshAndLogout_MultipleSessions_RevokesOnlyCallerAfterCommit()
    {
        await using var api = await IdentityPostgresApi.CreateAsync();
        using var first = api.Factory.NewClient();
        using var second = api.Factory.NewClient();
        using var other = api.Factory.NewClient();
        var original = await IdentityPostgresApi.LoginAsync(first);
        var another = await IdentityPostgresApi.LoginAsync(second);
        var foreign = await IdentityPostgresApi.LoginAsync(other, "manager1", "Manager@123");
        using var rotate = await RefreshAsync(first, original.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, rotate.StatusCode);
        var replacement = (await rotate.Content.ReadFromJsonAsync<LoginResponseDto>())!;
        Assert.True(replacement.RefreshToken != original.RefreshToken, "Rotation must issue a different token.");
        await AssertRefreshRejectedAsync(first, original.RefreshToken);
        first.DefaultRequestHeaders.Authorization = new("Bearer", replacement.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await first.GetAsync("/api/auth/me")).StatusCode);
        using var rotateAgain = await RefreshAsync(first, replacement.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, rotateAgain.StatusCode);
        var latest = (await rotateAgain.Content.ReadFromJsonAsync<LoginResponseDto>())!;
        first.DefaultRequestHeaders.Authorization = new("Bearer", latest.AccessToken);
        using var logout = await first.PostAsJsonAsync("/api/auth/logout", new LogoutRequest { RefreshToken = foreign.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        await AssertRevokedAsync(first, latest);
        await AssertRevokedAsync(second, another);
        await AssertRefreshRejectedAsync(first, replacement.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync("/api/auth/me")).StatusCode);
        using var otherRefresh = await RefreshAsync(other, foreign.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, otherRefresh.StatusCode);
        await using var db = api.Database.CreateContext();
        Assert.True(await db.RefreshTokens.Where(t => t.UserId == original.User.Id).AllAsync(t => t.IsRevoked));
        Assert.True(await db.Users.Where(u => u.Id == original.User.Id).Select(u => u.TokenVersion).SingleAsync() > 0);
    }

    [PostgresFact]
    public async Task AccountLifecycle_MutationsRevokeSessions_NoOpsAndLastAdminRemainSafe()
    {
        await using var api = await IdentityPostgresApi.CreateAsync();
        using var admin = api.Factory.NewClient();
        using var employee = api.Factory.NewClient();
        var administrator = await IdentityPostgresApi.LoginAsync(admin);
        var account = await IdentityPostgresApi.CreateUserAsync(admin, "Manager");
        var pair = await IdentityPostgresApi.LoginAsync(employee, account.Username, "HttpEmployee@123");
        using var noOpStatus = await admin.PatchAsJsonAsync($"/api/users/{account.Id}/status", new UpdateUserStatusRequest { IsActive = true });
        Assert.Equal(HttpStatusCode.OK, noOpStatus.StatusCode);
        using var noOpRole = await admin.PutAsJsonAsync($"/api/users/{account.Id}/role", new UpdateUserRoleRequest { Role = "Manager" });
        Assert.Equal(HttpStatusCode.OK, noOpRole.StatusCode);
        using var update = await admin.PutAsJsonAsync($"/api/users/{account.Id}", new UpdateUserRequest { FullName = "Updated HTTP employee", Email = "updated-http@test.local" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        foreach (var response in new[] { noOpStatus, noOpRole, update }) await IdentityPostgresApi.AssertNoSecretsAsync(response);
        Assert.Equal(HttpStatusCode.OK, (await employee.GetAsync("/api/auth/me")).StatusCode);
        using (var refresh = await RefreshAsync(employee, pair.RefreshToken))
        {
            Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
            pair = (await refresh.Content.ReadFromJsonAsync<LoginResponseDto>())!;
        }
        await using (var db = api.Database.CreateContext())
            Assert.Equal(0, await db.Users.Where(u => u.Id == account.Id).Select(u => u.TokenVersion).SingleAsync());

        using var locked = await admin.PatchAsJsonAsync($"/api/users/{account.Id}/status", new UpdateUserStatusRequest { IsActive = false });
        Assert.Equal(HttpStatusCode.OK, locked.StatusCode);
        await AssertRevokedAsync(employee, pair);
        using var lockedLogin = await employee.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = account.Username, Password = "HttpEmployee@123" });
        Assert.Equal(HttpStatusCode.Unauthorized, lockedLogin.StatusCode);
        using var unlocked = await admin.PatchAsJsonAsync($"/api/users/{account.Id}/status", new UpdateUserStatusRequest { IsActive = true });
        Assert.Equal(HttpStatusCode.OK, unlocked.StatusCode);
        await AssertRevokedAsync(employee, pair);
        pair = await IdentityPostgresApi.LoginAsync(employee, account.Username, "HttpEmployee@123");
        using var role = await admin.PutAsJsonAsync($"/api/users/{account.Id}/role", new UpdateUserRoleRequest { Role = "SalesStaff" });
        Assert.Equal(HttpStatusCode.OK, role.StatusCode);
        await AssertRevokedAsync(employee, pair);
        pair = await IdentityPostgresApi.LoginAsync(employee, account.Username, "HttpEmployee@123");
        Assert.Equal(RoleType.SalesStaff, pair.User.Role);
        await IdentityPostgresApi.AssertSafeFailureAsync(await employee.GetAsync("/api/users"), HttpStatusCode.Forbidden);
        using var reset = await admin.PostAsJsonAsync($"/api/users/{account.Id}/reset-password", new ResetUserPasswordRequest { Password = "ChangedHttpPassword@123" });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        await AssertRevokedAsync(employee, pair);
        using var oldPassword = await employee.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = account.Username, Password = "HttpEmployee@123" });
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        await IdentityPostgresApi.LoginAsync(employee, account.Username, "ChangedHttpPassword@123");

        using var lastLock = await admin.PatchAsJsonAsync($"/api/users/{administrator.User.Id}/status", new UpdateUserStatusRequest { IsActive = false });
        using var lastRole = await admin.PutAsJsonAsync($"/api/users/{administrator.User.Id}/role", new UpdateUserRoleRequest { Role = "Manager" });
        Assert.Equal(HttpStatusCode.Conflict, lastLock.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, lastRole.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/auth/me")).StatusCode);
        foreach (var path in new[] { "/api/users", $"/api/users/{account.Id}" })
        {
            using var read = await admin.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            await IdentityPostgresApi.AssertNoSecretsAsync(read);
        }
    }

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string token) =>
        client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest { RefreshToken = token });

    private static async Task AssertRefreshRejectedAsync(HttpClient client, string token)
    {
        using var response = await RefreshAsync(client, token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(401, (await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>())!.Status);
        await IdentityPostgresApi.AssertNoSecretsAsync(response);
    }

    private static async Task AssertRevokedAsync(HttpClient client, LoginResponseDto pair)
    {
        client.DefaultRequestHeaders.Authorization = new("Bearer", pair.AccessToken);
        await IdentityPostgresApi.AssertSafeFailureAsync(await client.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized, pair.AccessToken, pair.RefreshToken);
        await AssertRefreshRejectedAsync(client, pair.RefreshToken);
    }

    [PostgresFact]
    public async Task Orders_TwoSalesAccounts_PersistTokenActorAndEnforceOwnership()
    {
        await using var api = await IdentityPostgresApi.CreateAsync();
        using var admin = api.Factory.NewClient();
        using var salesA = api.Factory.NewClient();
        using var salesB = api.Factory.NewClient();
        using var manager = api.Factory.NewClient();
        var administrator = await IdentityPostgresApi.LoginAsync(admin);
        var a = await IdentityPostgresApi.CreateUserAsync(admin, "SalesStaff");
        var b = await IdentityPostgresApi.CreateUserAsync(admin, "SalesStaff");
        await IdentityPostgresApi.LoginAsync(salesA, a.Username, "HttpEmployee@123");
        await IdentityPostgresApi.LoginAsync(salesB, b.Username, "HttpEmployee@123");
        await IdentityPostgresApi.LoginAsync(manager, "manager1", "Manager@123");
        var key = Guid.NewGuid().ToString("N")[..12];
        using var categoryResponse = await admin.PostAsJsonAsync("/api/categories", new CreateCategoryRequest { Name = "HTTP " + key, Slug = "http-" + key, IsActive = true });
        Assert.Equal(HttpStatusCode.Created, categoryResponse.StatusCode);
        var category = (await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>())!;
        using var productResponse = await admin.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            CategoryId = category.Id, Name = "HTTP fixture product", Sku = "HTTP-" + key, Brand = "Test",
            BasePrice = 100000, CostPrice = 50000, IsSerialTracked = false
        });
        Assert.Equal(HttpStatusCode.Created, productResponse.StatusCode);
        var product = (await productResponse.Content.ReadFromJsonAsync<ProductDto>())!;
        using var activate = await admin.PatchAsJsonAsync($"/api/products/{product.Id}/status", new UpdateProductStatusRequest { IsActive = true });
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode);
        using var stockResponse = await admin.PostAsJsonAsync("/api/inventory/stocks/initialize", new InitializeStockRequest
        {
            ProductId = product.Id, Quantity = 3, UnitCost = 50000, MinStockAlert = 0, Reason = "HTTP actor fixture"
        });
        Assert.Equal(HttpStatusCode.Created, stockResponse.StatusCode);
        var stock = (await stockResponse.Content.ReadFromJsonAsync<InventoryStockDto>())!;
        var own = await CreateAsync(salesA);
        var foreign = await CreateAsync(salesB);
        Assert.Equal(a.Id, own.CashierUserId);
        Assert.Equal(b.Id, foreign.CashierUserId);
        var page = await salesA.GetFromJsonAsync<PagedResultDto<OrderSummaryDto>>("/api/orders?pageSize=1");
        Assert.Equal(1, page!.TotalCount);
        Assert.Equal(1, page.TotalPages);
        Assert.Equal(own.Id, Assert.Single(page.Items).Id);
        Assert.Equal(HttpStatusCode.NotFound, (await salesA.GetAsync($"/api/orders/{foreign.Id}")).StatusCode);
        using var forbiddenTransition = await salesA.PatchAsJsonAsync($"/api/orders/{foreign.Id}/status", new UpdateOrderStatusRequest { TargetStatus = "Cancelled" });
        Assert.Equal(HttpStatusCode.NotFound, forbiddenTransition.StatusCode);
        await using (var db = api.Database.CreateContext())
            Assert.Equal(OrderStatus.Pending, await db.Orders.Where(o => o.Id == foreign.Id).Select(o => o.Status).SingleAsync());
        foreach (var reader in new[] { admin, manager })
        {
            foreach (var order in new[] { own, foreign })
                Assert.Equal(order.Id, (await reader.GetFromJsonAsync<OrderDto>($"/api/orders/{order.Id}"))!.Id);
            var all = await reader.GetFromJsonAsync<PagedResultDto<OrderSummaryDto>>("/api/orders?pageSize=100");
            Assert.Contains(all!.Items, o => o.Id == own.Id);
            Assert.Contains(all.Items, o => o.Id == foreign.Id);
        }
        using var completed = await salesA.PatchAsJsonAsync($"/api/orders/{own.Id}/status", new UpdateOrderStatusRequest { TargetStatus = "Completed" });
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        await IdentityPostgresApi.AssertNoSecretsAsync(completed);
        await using (var db = api.Database.CreateContext())
        {
            var saved = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == own.Id);
            Assert.Equal(a.Id, saved.CashierUserId);
            Assert.Equal(OrderStatus.Completed, saved.Status);
            var itemId = Assert.Single(own.Items).Id;
            var audit = await db.InventoryMovements.AsNoTracking().SingleAsync(m => m.OrderItemId == itemId);
            Assert.Equal(a.Id, audit.PerformedByUserId);
            Assert.Equal(InventoryMovementType.Sale, audit.MovementType);
            Assert.Equal(-1, audit.QuantityChange);
            Assert.Equal(2, (await db.InventoryStocks.AsNoTracking().SingleAsync(s => s.Id == stock.Id)).Quantity);
        }

        async Task<OrderDto> CreateAsync(HttpClient client)
        {
            // An untrusted extra actor field must never override the authenticated cashier.
            using var response = await client.PostAsJsonAsync("/api/orders", new
            {
                cashierUserId = administrator.User.Id, paymentMethod = "Cash",
                items = new[] { new { productId = product.Id, quantity = 1 } }
            });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<OrderDto>())!;
        }
    }

    [PostgresFact]
    public async Task Fixture_ExceptionDuringUse_DropsGeneratedDatabase()
    {
        string databaseName = "";
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var api = await IdentityPostgresApi.CreateAsync();
            databaseName = new NpgsqlConnectionStringBuilder(api.Database.ConnectionString).Database!;
            throw new InvalidOperationException("Deliberate failure to verify cleanup.");
        });
        var source = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TECHSTORE_TEST_CONNECTION"));
        Assert.StartsWith("techstore_category_test_", databaseName);
        Assert.NotEqual(source.Database, databaseName);
        source.Database = "postgres";
        await using var connection = new NpgsqlConnection(source.ConnectionString);
        await connection.OpenAsync();
        await using var query = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name)", connection);
        query.Parameters.AddWithValue("name", databaseName);
        Assert.False((bool)(await query.ExecuteScalarAsync())!);
    }
}
