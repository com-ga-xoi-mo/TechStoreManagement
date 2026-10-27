using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TechStore.Api.Inventory.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests.Inventory;

public sealed class InventoryControllerTests
{
    [Fact]
    public async Task Inventory_Reads_Should_Require_Authentication_And_Allow_All_Inventory_Roles()
    {
        var inventory = new FakeInventoryService();
        var serials = new FakeSerialImeiService();
        await using var host = new InventoryApiHost(services =>
        {
            services.AddSingleton<IInventoryService>(inventory);
            services.AddSingleton<ISerialImeiService>(serials);
        });
        await host.StartAsync();

        (await host.Client.GetAsync("/api/inventory/stocks")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.Client.GetAsync("/api/inventory/serials")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        inventory.GetStocksCount.Should().Be(0);
        serials.GetSerialsCount.Should().Be(0);

        foreach (var role in new[] { "Admin", "Manager", "SalesStaff", "WarehouseStaff" })
        {
            host.Authenticate(host.Token(role));
            (await host.Client.GetAsync("/api/inventory/stocks")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await host.Client.GetAsync("/api/inventory/serials")).StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Inventory_Writes_Should_Enforce_Roles_And_Forward_Claimed_User_Id()
    {
        var inventory = new FakeInventoryService();
        var serials = new FakeSerialImeiService();
        await using var host = new InventoryApiHost(services =>
        {
            services.AddSingleton<IInventoryService>(inventory);
            services.AddSingleton<ISerialImeiService>(serials);
        });
        await host.StartAsync();
        var initRequest = new InitializeStockRequest { ProductId = Guid.NewGuid(), Quantity = 1 };
        var importRequest = new BatchImportSerialImeiRequest
        {
            ProductId = initRequest.ProductId,
            Items = [new SerialImeiImportItemRequest { SerialNumber = "SN-001" }]
        };

        (await host.Client.PostAsJsonAsync("/api/inventory/stocks/initialize", initRequest)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        foreach (var role in new[] { "Manager", "SalesStaff" })
        {
            host.Authenticate(host.Token(role));
            (await host.Client.PostAsJsonAsync("/api/inventory/stocks/initialize", initRequest)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await host.Client.PostAsJsonAsync("/api/inventory/serials/batch-import", importRequest)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        var expected = Guid.NewGuid();
        host.Authenticate(host.Token("WarehouseStaff", expected));
        (await host.Client.PostAsJsonAsync("/api/inventory/stocks/initialize", initRequest)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await host.Client.PostAsJsonAsync("/api/inventory/serials/batch-import", importRequest)).StatusCode.Should().Be(HttpStatusCode.Created);
        inventory.LastPerformedByUserId.Should().Be(expected);
        serials.LastPerformedByUserId.Should().Be(expected);
    }

    [Fact]
    public async Task Inventory_Writes_Should_Return_Unauthorized_For_Missing_Or_Malformed_Actor_Claim()
    {
        var inventory = new FakeInventoryService();
        var serials = new FakeSerialImeiService();
        await using var host = new InventoryApiHost(services =>
        {
            services.AddSingleton<IInventoryService>(inventory);
            services.AddSingleton<ISerialImeiService>(serials);
        });
        await host.StartAsync();
        var request = new InitializeStockRequest { ProductId = Guid.NewGuid(), Quantity = 1 };

        host.Authenticate(host.Token("Admin", includeSub: false));
        (await host.Client.PostAsJsonAsync("/api/inventory/stocks/initialize", request)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        host.Authenticate(host.Token("Admin", sub: "not-a-guid"));
        (await host.Client.PostAsJsonAsync("/api/inventory/stocks/initialize", request)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        inventory.InitializeCount.Should().Be(0);
    }

    [Fact]
    public async Task Inventory_DeleteStock_Should_AllowAdminAndWarehouseStaffOnly()
    {
        var inventory = new FakeInventoryService();
        await using var host = new InventoryApiHost(services =>
        {
            services.AddSingleton<IInventoryService>(inventory);
            services.AddSingleton<ISerialImeiService, FakeSerialImeiService>();
        });
        await host.StartAsync();
        var stockId = Guid.NewGuid();

        (await host.Client.DeleteAsync($"/api/inventory/stocks/{stockId}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        foreach (var role in new[] { "Manager", "SalesStaff" })
        {
            host.Authenticate(host.Token(role));
            (await host.Client.DeleteAsync($"/api/inventory/stocks/{stockId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        inventory.DeleteCount.Should().Be(0);

        foreach (var role in new[] { "Admin", "WarehouseStaff" })
        {
            host.Authenticate(host.Token(role));
            (await host.Client.DeleteAsync($"/api/inventory/stocks/{stockId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        inventory.DeleteCount.Should().Be(2);
    }
}

public sealed class FakeInventoryService : IInventoryService
{
    public int GetStocksCount { get; private set; }
    public int InitializeCount { get; private set; }
    public int DeleteCount { get; private set; }
    public Guid LastPerformedByUserId { get; private set; }

    public Task<InventoryStockSearchResult> GetStocksAsync(InventoryStockFilterRequest request, CancellationToken cancellationToken = default)
    {
        GetStocksCount++;
        return Task.FromResult(InventoryStockSearchResult.Success(new PagedResultDto<InventoryStockDto>
        {
            Items = [], Page = 1, PageSize = 20, TotalCount = 0, TotalPages = 0
        }));
    }

    public Task<InitializeStockResult> InitializeStockAsync(InitializeStockRequest request, Guid performedByUserId, CancellationToken cancellationToken = default)
    {
        InitializeCount++;
        LastPerformedByUserId = performedByUserId;
        return Task.FromResult(InitializeStockResult.Success(new InventoryStockDto { ProductId = request.ProductId ?? Guid.Empty }));
    }

    public Task<DeleteInventoryStockResult> DeleteStockAsync(Guid stockId, CancellationToken cancellationToken = default)
    {
        DeleteCount++;
        return Task.FromResult(DeleteInventoryStockResult.Success());
    }

    public Task<InventoryStockDetailResult> GetStockDetailAsync(Guid stockId, CancellationToken cancellationToken = default) =>
        Task.FromResult(InventoryStockDetailResult.NotFound());

    public Task<AdjustInventoryStockResult> AdjustStockAsync(Guid stockId, AdjustStockRequest request, Guid performedByUserId, CancellationToken cancellationToken = default)
    {
        LastPerformedByUserId = performedByUserId;
        return Task.FromResult(AdjustInventoryStockResult.NotFound());
    }
}

public sealed class FakeSerialImeiService : ISerialImeiService
{
    public int GetSerialsCount { get; private set; }
    public int BatchImportCount { get; private set; }
    public Guid LastPerformedByUserId { get; private set; }

    public Task<SerialImeiSearchResult> GetSerialsAsync(SerialImeiFilterRequest request, CancellationToken cancellationToken = default)
    {
        GetSerialsCount++;
        return Task.FromResult(SerialImeiSearchResult.Success(new PagedResultDto<SerialImeiDto>
        {
            Items = [], Page = 1, PageSize = 20, TotalCount = 0, TotalPages = 0
        }));
    }

    public Task<BatchImportSerialImeiResult> BatchImportAsync(BatchImportSerialImeiRequest request, Guid performedByUserId, CancellationToken cancellationToken = default)
    {
        BatchImportCount++;
        LastPerformedByUserId = performedByUserId;
        return Task.FromResult(BatchImportSerialImeiResult.Success(request.Items?.Count ?? 0));
    }
}
