using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

// Real JWT pipeline, services, repositories and PostgreSQL: shifts, cash checkout, idempotency and races.
public sealed class PosCheckoutPostgresHttpTests
{
    private const decimal Price = 10_000_000m;
    // What the customer pays: catalog prices exclude VAT and the API runs with the production 10% (M3 pricing).
    private const decimal Total = 11_000_000m;
    private const string Password = "HttpEmployee@123";

    [PostgresFact]
    public async Task CashSale_FullFlow_UpdatesOrderDeviceStockWarrantyAndShiftThenCloses()
    {
        // Arrange
        await using var world = await PosWorld.CreateAsync(deviceCount: 2);
        var shift = await world.OpenShiftAsync(world.SalesA, 2_000_000m);
        var imeiId = world.DeviceIds[0];

        // Act
        using var response = await world.SalesA.Client.PostAsJsonAsync("/api/pos/checkout", Checkout(world, shift.Id, imeiId, 11_500_000m));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = (await response.Content.ReadFromJsonAsync<OrderDto>())!;
        Assert.Equal("Completed", order.Status);
        Assert.Equal(Price, order.Subtotal);
        Assert.Equal(1_000_000m, order.TaxAmount);
        Assert.Equal(Total, order.TotalAmount);
        Assert.Equal(500_000m, order.ChangeGiven);
        Assert.Equal(shift.Id, order.PosSessionId);
        Assert.Equal(world.SalesA.UserId, order.CashierUserId);
        await using (var db = world.Database.CreateContext())
        {
            var serial = await db.SerialImeis.AsNoTracking().SingleAsync(s => s.Id == imeiId);
            Assert.Equal(SerialImeiStatus.Sold, serial.Status);
            Assert.NotNull(serial.WarrantyStartAt);
            Assert.NotNull(serial.WarrantyEndAt);
            var stock = await db.InventoryStocks.AsNoTracking().SingleAsync(s => s.ProductId == world.ProductId);
            Assert.Equal(1, stock.Quantity);
            Assert.Equal(0, stock.ReservedQuantity);
            var item = Assert.Single(order.Items);
            var movement = await db.InventoryMovements.AsNoTracking().SingleAsync(m => m.OrderItemId == item.Id);
            Assert.Equal(InventoryMovementType.Sale, movement.MovementType);
            Assert.Equal(world.SalesA.UserId, movement.PerformedByUserId);
            var saved = await db.PosSessions.AsNoTracking().SingleAsync(s => s.Id == shift.Id);
            Assert.Equal(Total, saved.CashSalesTotal);
            var dbOrder = await db.Orders.AsNoTracking().Include(o => o.Items).SingleAsync(o => o.Id == order.Id);
            Assert.Equal(11_000_000m, dbOrder.Items.Single().UnitCost);
        }

        // Act: close with exactly the expected cash
        using var close = await world.SalesA.Client.PostAsJsonAsync(
            $"/api/pos/shifts/{shift.Id}/close", new CloseShiftRequest { ClosingBalance = 13_000_000m });

        // Assert
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        var closed = (await close.Content.ReadFromJsonAsync<PosShiftDto>())!;
        Assert.Equal("Closed", closed.Status);
        Assert.Equal(13_000_000m, closed.ExpectedClosingBalance);
        Assert.Equal(0m, closed.Discrepancy);

        // Act: selling into the closed shift
        using var late = await world.SalesA.Client.PostAsJsonAsync("/api/pos/checkout", Checkout(world, shift.Id, world.DeviceIds[1], Total));

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        await using var db2 = world.Database.CreateContext();
        Assert.Equal(1, await world.TestOrders(db2).CountAsync());
        Assert.Equal(SerialImeiStatus.InStock, (await db2.SerialImeis.AsNoTracking().SingleAsync(s => s.Id == world.DeviceIds[1])).Status);
    }

    [PostgresFact]
    public async Task Checkout_SameKeyTwice_ReturnsSameOrderAndAppliesItOnce()
    {
        // Arrange
        await using var world = await PosWorld.CreateAsync(deviceCount: 1);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var request = Checkout(world, shift.Id, world.DeviceIds[0], Total);

        // Act
        using var first = await world.SalesA.Client.PostAsJsonAsync("/api/pos/checkout", request);
        using var second = await world.SalesA.Client.PostAsJsonAsync("/api/pos/checkout", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal((await first.Content.ReadFromJsonAsync<OrderDto>())!.Id, (await second.Content.ReadFromJsonAsync<OrderDto>())!.Id);
        await world.AssertSoldOnceAsync(shift.Id, expectedOrders: 1);
    }

    [PostgresFact]
    public async Task Checkout_SameKeyAtTheSameTime_AppliesItOnce()
    {
        // Arrange
        await using var world = await PosWorld.CreateAsync(deviceCount: 1);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var request = Checkout(world, shift.Id, world.DeviceIds[0], Total);
        var gate = new TaskCompletionSource();

        // Act
        var calls = Enumerable.Range(0, 2).Select(async _ =>
        {
            await gate.Task;
            using var response = await world.SalesA.Client.PostAsJsonAsync("/api/pos/checkout", request);
            return (response.StatusCode, Order: await response.Content.ReadFromJsonAsync<OrderDto>());
        }).ToArray();
        gate.SetResult();
        var results = await Task.WhenAll(calls);

        // Assert
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Created], results.Select(r => r.StatusCode).Order().ToArray());
        Assert.Equal(results[0].Order!.Id, results[1].Order!.Id);
        await world.AssertSoldOnceAsync(shift.Id, expectedOrders: 1);
    }

    [PostgresFact]
    public async Task Checkout_TwoCashiersRaceForTheLastDevice_ExactlyOneSucceeds()
    {
        // Arrange
        await using var world = await PosWorld.CreateAsync(deviceCount: 1);
        var shiftA = await world.OpenShiftAsync(world.SalesA, 0m);
        var shiftB = await world.OpenShiftAsync(world.SalesB, 0m);
        var gate = new TaskCompletionSource();

        // Act
        async Task<HttpStatusCode> SellAsync(PosWorld.Seller seller, PosShiftDto shift)
        {
            await gate.Task;
            using var response = await seller.Client.PostAsJsonAsync("/api/pos/checkout", Checkout(world, shift.Id, world.DeviceIds[0], Total));
            return response.StatusCode;
        }

        var tasks = new[] { SellAsync(world.SalesA, shiftA), SellAsync(world.SalesB, shiftB) };
        gate.SetResult();
        var statuses = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], statuses.Order().ToArray());
        await using var db = world.Database.CreateContext();
        Assert.Equal(1, await world.TestOrders(db).CountAsync());
        Assert.Equal(1, await world.TestSales(db).CountAsync());
        Assert.Equal(Total, await world.TestShifts(db).SumAsync(s => s.CashSalesTotal));
        var stock = await db.InventoryStocks.AsNoTracking().SingleAsync(s => s.ProductId == world.ProductId);
        Assert.Equal(0, stock.Quantity);
        Assert.Equal(0, stock.ReservedQuantity);
    }

    [PostgresFact]
    public async Task Checkout_WhileTheShiftIsClosing_SalesAreEitherCountedOrRejected()
    {
        // Arrange
        await using var world = await PosWorld.CreateAsync(deviceCount: 1);
        var shift = await world.OpenShiftAsync(world.SalesA, 1_000_000m);
        var gate = new TaskCompletionSource();

        // Act
        var sale = Task.Run(async () =>
        {
            await gate.Task;
            using var response = await world.SalesA.Client.PostAsJsonAsync("/api/pos/checkout", Checkout(world, shift.Id, world.DeviceIds[0], Total));
            return response.StatusCode;
        });
        var close = Task.Run(async () =>
        {
            await gate.Task;
            using var response = await world.Manager.PostAsJsonAsync(
                $"/api/pos/shifts/{shift.Id}/close", new CloseShiftRequest { ClosingBalance = 1_000_000m, Note = "race" });
            return response.StatusCode;
        });
        gate.SetResult();
        var saleStatus = await sale;
        var closeStatus = await close;

        // Assert
        Assert.Equal(HttpStatusCode.OK, closeStatus);
        Assert.Contains(saleStatus, new[] { HttpStatusCode.Created, HttpStatusCode.Conflict });
        await using var db = world.Database.CreateContext();
        var saved = await db.PosSessions.AsNoTracking().SingleAsync(s => s.Id == shift.Id);
        var orders = await db.Orders.AsNoTracking().Where(o => o.PosSessionId == shift.Id).ToListAsync();
        Assert.Equal(saleStatus == HttpStatusCode.Created ? 1 : 0, orders.Count);
        Assert.Equal(orders.Sum(o => o.TotalAmount), saved.CashSalesTotal);
        Assert.Equal(1_000_000m + saved.CashSalesTotal, saved.ExpectedClosingBalance);
        Assert.Equal(world.ManagerUserId, saved.ClosedByUserId);
    }

    [PostgresFact]
    public async Task Checkout_OneLineUnavailable_RollsBackEveryLine()
    {
        // Arrange
        await using var world = await PosWorld.CreateAsync(deviceCount: 3);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        await using (var db = world.Database.CreateContext())
            await db.SerialImeis.Where(s => s.Id == world.DeviceIds[2]).ExecuteUpdateAsync(
                s => s.SetProperty(x => x.Status, SerialImeiStatus.Defective));
        var request = Checkout(world, shift.Id, world.DeviceIds[0], 100_000_000m);
        request.Items = world.DeviceIds.Select(id => new CreateOrderItemRequest { ProductId = world.ProductId, SerialImeiId = id }).ToList();

        // Act
        using var response = await world.SalesA.Client.PostAsJsonAsync("/api/pos/checkout", request);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var check = world.Database.CreateContext();
        Assert.Equal(0, await world.TestOrders(check).CountAsync());
        Assert.Equal(0, await world.TestSales(check).CountAsync());
        Assert.Equal(2, await check.SerialImeis.CountAsync(s => s.ProductId == world.ProductId && s.Status == SerialImeiStatus.InStock));
        Assert.Equal(0m, (await check.PosSessions.AsNoTracking().SingleAsync(s => s.Id == shift.Id)).CashSalesTotal);
        Assert.Equal(0, (await check.InventoryStocks.AsNoTracking().SingleAsync(s => s.ProductId == world.ProductId)).ReservedQuantity);
    }

    [PostgresFact]
    public async Task Shifts_Authorization_OwnershipAndRoleRules()
    {
        // Arrange
        await using var world = await PosWorld.CreateAsync(deviceCount: 1);
        var shift = await world.OpenShiftAsync(world.SalesA, 100m);
        using var anonymous = world.Api.Factory.NewClient();

        // Act and Assert: second open shift and ownership
        using var again = await world.SalesA.Client.PostAsJsonAsync("/api/pos/shifts", new OpenShiftRequest { OpeningBalance = 1m });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.SalesB.Client.GetAsync($"/api/pos/shifts/{shift.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.SalesB.Client.PostAsJsonAsync(
            $"/api/pos/shifts/{shift.Id}/close", new CloseShiftRequest { ClosingBalance = 100m })).StatusCode);
        var current = await world.SalesA.Client.GetFromJsonAsync<PosShiftDto>("/api/pos/shifts/current");
        Assert.Equal(shift.Id, current!.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await world.SalesB.Client.GetAsync("/api/pos/shifts/current")).StatusCode);

        // Act and Assert: lists by role
        var own = await world.SalesB.Client.GetFromJsonAsync<PagedResultDto<PosShiftDto>>("/api/pos/shifts");
        Assert.Empty(own!.Items);
        var all = await world.Manager.GetFromJsonAsync<PagedResultDto<PosShiftDto>>("/api/pos/shifts");
        Assert.Contains(all!.Items, s => s.Id == shift.Id);

        // Act and Assert: roles that must not sell or open
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Manager.PostAsJsonAsync("/api/pos/checkout", Checkout(world, shift.Id, world.DeviceIds[0], Total))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Manager.PostAsJsonAsync("/api/pos/shifts", new OpenShiftRequest { OpeningBalance = 0m })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Warehouse.GetAsync("/api/pos/shifts")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/pos/shifts/current")).StatusCode);

        // Act and Assert: discrepancy needs a note, Manager may force close
        using var noNote = await world.Manager.PostAsJsonAsync($"/api/pos/shifts/{shift.Id}/close", new CloseShiftRequest { ClosingBalance = 50m });
        Assert.Equal(HttpStatusCode.BadRequest, noNote.StatusCode);
        using var forced = await world.Manager.PostAsJsonAsync($"/api/pos/shifts/{shift.Id}/close", new CloseShiftRequest { ClosingBalance = 50m, Note = "Manager count" });
        Assert.Equal(HttpStatusCode.OK, forced.StatusCode);
        var closed = (await forced.Content.ReadFromJsonAsync<PosShiftDto>())!;
        Assert.Equal(-50m, closed.Discrepancy);
        Assert.Equal(world.ManagerUserId, closed.ClosedByUserId);
        using var twice = await world.Manager.PostAsJsonAsync($"/api/pos/shifts/{shift.Id}/close", new CloseShiftRequest { ClosingBalance = 50m, Note = "again" });
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
    }

    [PostgresFact]
    public async Task PaidPosOrder_CannotBeCancelledThroughTheOrdersApi()
    {
        // Arrange
        await using var world = await PosWorld.CreateAsync(deviceCount: 1);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        using var sale = await world.SalesA.Client.PostAsJsonAsync("/api/pos/checkout", Checkout(world, shift.Id, world.DeviceIds[0], Total));
        var order = (await sale.Content.ReadFromJsonAsync<OrderDto>())!;

        // Act
        using var cancel = await world.SalesA.Client.PatchAsJsonAsync(
            $"/api/orders/{order.Id}/status", new UpdateOrderStatusRequest { TargetStatus = "Cancelled" });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        var detail = await world.SalesA.Client.GetFromJsonAsync<OrderDto>($"/api/orders/{order.Id}");
        Assert.Equal("Completed", detail!.Status);
        Assert.Equal(order.CashTendered, detail.CashTendered);
    }

    private static PosCheckoutRequest Checkout(PosWorld world, Guid shiftId, Guid deviceId, decimal tendered) => new()
    {
        PosSessionId = shiftId,
        CheckoutKey = Guid.NewGuid(),
        CashTendered = tendered,
        Items = [new CreateOrderItemRequest { ProductId = world.ProductId, SerialImeiId = deviceId }]
    };

    private sealed class PosWorld : IAsyncDisposable
    {
        public sealed record Seller(HttpClient Client, Guid UserId);

        private readonly IdentityPostgresApi _api;
        private readonly List<HttpClient> _clients = [];

        public IdentityPostgresApi Api => _api;
        public PostgresDatabase Database => _api.Database;
        public Seller SalesA { get; private set; } = null!;
        public Seller SalesB { get; private set; } = null!;
        public HttpClient Manager { get; private set; } = null!;
        public Guid ManagerUserId { get; private set; }
        public HttpClient Warehouse { get; private set; } = null!;
        public Guid ProductId { get; private set; }
        public List<Guid> DeviceIds { get; } = [];

        private PosWorld(IdentityPostgresApi api) => _api = api;

        public static async Task<PosWorld> CreateAsync(int deviceCount)
        {
            var world = new PosWorld(await IdentityPostgresApi.CreateAsync());
            try
            {
                await world.SetUpAsync(deviceCount);
                return world;
            }
            catch
            {
                await world.DisposeAsync();
                throw;
            }
        }

        private HttpClient NewClient()
        {
            var client = _api.Factory.NewClient();
            _clients.Add(client);
            return client;
        }

        private async Task SetUpAsync(int deviceCount)
        {
            var admin = NewClient();
            await IdentityPostgresApi.LoginAsync(admin);
            var a = await IdentityPostgresApi.CreateUserAsync(admin, "SalesStaff");
            var b = await IdentityPostgresApi.CreateUserAsync(admin, "SalesStaff");
            SalesA = new Seller(NewClient(), a.Id);
            SalesB = new Seller(NewClient(), b.Id);
            await IdentityPostgresApi.LoginAsync(SalesA.Client, a.Username, Password);
            await IdentityPostgresApi.LoginAsync(SalesB.Client, b.Username, Password);
            Manager = NewClient();
            ManagerUserId = (await IdentityPostgresApi.LoginAsync(Manager, "manager1", "Manager@123")).User.Id;
            Warehouse = NewClient();
            await IdentityPostgresApi.LoginAsync(Warehouse, "warehouse1", "Warehouse@123");

            var key = Guid.NewGuid().ToString("N")[..12];
            using var categoryResponse = await admin.PostAsJsonAsync("/api/categories", new CreateCategoryRequest { Name = "POS " + key, Slug = "pos-" + key, IsActive = true });
            categoryResponse.EnsureSuccessStatusCode();
            var category = (await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>())!;
            using var productResponse = await admin.PostAsJsonAsync("/api/products", new CreateProductRequest
            {
                CategoryId = category.Id, Name = "POS phone", Sku = "POS-" + key, Brand = "Test",
                BasePrice = Price, CostPrice = 5_000_000, IsSerialTracked = true
            });
            productResponse.EnsureSuccessStatusCode();
            ProductId = (await productResponse.Content.ReadFromJsonAsync<ProductDto>())!.Id;
            (await admin.PatchAsJsonAsync($"/api/products/{ProductId}/status", new UpdateProductStatusRequest { IsActive = true })).EnsureSuccessStatusCode();
            var imeis = Enumerable.Range(0, deviceCount).Select(i => "35" + Random.Shared.NextInt64(1_000_000_000L, 9_999_999_999L) + i.ToString("D3")).ToList();
            using var import = await admin.PostAsJsonAsync("/api/inventory/serials/batch-import", new BatchImportSerialImeiRequest
            {
                ProductId = ProductId,
                DefaultUnitCost = 11_000_000m,
                DefaultWarrantyMonths = 12,
                Reason = "POS fixture",
                Items = imeis.Select(i => new SerialImeiImportItemRequest { Imei = i }).ToList()
            });
            Assert.Equal(HttpStatusCode.Created, import.StatusCode);
            await using var db = Database.CreateContext();
            DeviceIds.AddRange(await db.SerialImeis.AsNoTracking()
                .Where(s => s.ProductId == ProductId).OrderBy(s => s.Imei).Select(s => s.Id).ToListAsync());
            Assert.Equal(deviceCount, DeviceIds.Count);
        }

        public async Task<PosShiftDto> OpenShiftAsync(Seller seller, decimal opening)
        {
            using var response = await seller.Client.PostAsJsonAsync("/api/pos/shifts", new OpenShiftRequest { OpeningBalance = opening });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<PosShiftDto>())!;
        }

        // The seeded demo data also contains orders and movements; count only what these test cashiers did.
        public IQueryable<TechStore.Api.Orders.Entities.Order> TestOrders(TechStore.Api.Common.Data.AppDbContext db) =>
            db.Orders.Where(o => o.CashierUserId == SalesA.UserId || o.CashierUserId == SalesB.UserId);

        public IQueryable<TechStore.Api.Inventory.Entities.InventoryMovement> TestSales(TechStore.Api.Common.Data.AppDbContext db) =>
            db.InventoryMovements.Where(m => m.MovementType == InventoryMovementType.Sale
                && (m.PerformedByUserId == SalesA.UserId || m.PerformedByUserId == SalesB.UserId));

        public IQueryable<TechStore.Api.Sales.Entities.PosSession> TestShifts(TechStore.Api.Common.Data.AppDbContext db) =>
            db.PosSessions.Where(s => s.CashierUserId == SalesA.UserId || s.CashierUserId == SalesB.UserId);

        public async Task AssertSoldOnceAsync(Guid shiftId, int expectedOrders)
        {
            await using var db = Database.CreateContext();
            Assert.Equal(expectedOrders, await TestOrders(db).CountAsync());
            Assert.Equal(expectedOrders, await TestSales(db).CountAsync());
            Assert.Equal(Total * expectedOrders, (await db.PosSessions.AsNoTracking().SingleAsync(s => s.Id == shiftId)).CashSalesTotal);
            Assert.Equal(0, (await db.InventoryStocks.AsNoTracking().SingleAsync(s => s.ProductId == ProductId)).ReservedQuantity);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var client in _clients) client.Dispose();
            await _api.DisposeAsync();
        }
    }
}
