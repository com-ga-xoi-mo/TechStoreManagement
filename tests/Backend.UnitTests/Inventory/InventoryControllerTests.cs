using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using TechStore.Api.Inventory.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
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
    public async Task GetSerials_Should_Hide_Cost_For_SalesStaff_And_Include_Cost_For_Warehouse_Manager_Admin()
    {
        var inventory = new FakeInventoryService();
        var serials = new FakeSerialImeiService();
        await using var host = new InventoryApiHost(services =>
        {
            services.AddSingleton<IInventoryService>(inventory);
            services.AddSingleton<ISerialImeiService>(serials);
        });
        await host.StartAsync();

        // SalesStaff receives null unit cost
        host.Authenticate(host.Token("SalesStaff"));
        var salesResp = await host.Client.GetAsync("/api/inventory/serials");
        salesResp.StatusCode.Should().Be(HttpStatusCode.OK);
        serials.LastIncludeCost.Should().BeFalse();
        var salesData = await salesResp.Content.ReadFromJsonAsync<PagedResultDto<SerialImeiDto>>();
        salesData!.Items[0].UnitCost.Should().BeNull();

        // WarehouseStaff, Manager, Admin receive real unit cost
        foreach (var role in new[] { "WarehouseStaff", "Manager", "Admin" })
        {
            host.Authenticate(host.Token(role));
            var resp = await host.Client.GetAsync("/api/inventory/serials");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            serials.LastIncludeCost.Should().BeTrue();
            var data = await resp.Content.ReadFromJsonAsync<PagedResultDto<SerialImeiDto>>();
            data!.Items[0].UnitCost.Should().Be(15_000_000m);
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

    [Fact]
    public async Task AdjustStock_WhenConflict_Returns409ProblemDetails()
    {
        var inventory = new FakeInventoryService
        {
            NextAdjustResult = AdjustInventoryStockResult.Conflict(
                "Hàng có Serial/IMEI: nhập máy qua phiếu nhập, bớt máy bằng Báo lỗi máy theo từng IMEI.")
        };

        await using var host = new InventoryApiHost(services =>
        {
            services.AddSingleton<IInventoryService>(inventory);
            services.AddSingleton<ISerialImeiService, FakeSerialImeiService>();
        });
        await host.StartAsync();

        host.Authenticate(host.Token("WarehouseStaff"));
        var stockId = Guid.NewGuid();
        var response = await host.Client.PostAsJsonAsync($"/api/inventory/stocks/{stockId}/adjust", new AdjustStockRequest
        {
            QuantityChange = 5,
            Reason = "Kiểm kê"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Detail.Should().Be("Hàng có Serial/IMEI: nhập máy qua phiếu nhập, bớt máy bằng Báo lỗi máy theo từng IMEI.");
    }

    [Fact]
    public async Task InitializeStock_WhenValidationFails_Returns400BadRequest()
    {
        var inventory = new FakeInventoryService
        {
            NextInitializeResult = InitializeStockResult.ValidationFailed(new()
            {
                ["productId"] = ["Hàng có Serial/IMEI: nhập máy qua phiếu nhập hoặc nhập theo lô Serial/IMEI, không khởi tạo số dư trực tiếp."]
            })
        };

        await using var host = new InventoryApiHost(services =>
        {
            services.AddSingleton<IInventoryService>(inventory);
            services.AddSingleton<ISerialImeiService, FakeSerialImeiService>();
        });
        await host.StartAsync();

        host.Authenticate(host.Token("WarehouseStaff"));
        var response = await host.Client.PostAsJsonAsync("/api/inventory/stocks/initialize", new InitializeStockRequest
        {
            ProductId = Guid.NewGuid(),
            Quantity = 5
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Errors.Should().ContainKey("productId");
    }

    [Fact]
    public async Task Defective_And_Restore_Should_Enforce_Authentication_And_Roles()
    {
        var serials = new FakeSerialImeiService();
        await using var host = new InventoryApiHost(services =>
        {
            services.AddSingleton<IInventoryService, FakeInventoryService>();
            services.AddSingleton<ISerialImeiService>(serials);
        });
        await host.StartAsync();

        var deviceId = Guid.NewGuid();
        var defectiveReq = new ReportDefectiveDeviceRequest { Reason = "Hư màn hình" };
        var restoreReq = new RestoreDeviceRequest { Note = "Đã thay" };

        // 401 Unauthorized
        (await host.Client.PostAsJsonAsync($"/api/inventory/serials/{deviceId}/defective", defectiveReq)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.Client.PostAsJsonAsync($"/api/inventory/serials/{deviceId}/restore", restoreReq)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 403 Forbidden for Manager and SalesStaff
        foreach (var role in new[] { "Manager", "SalesStaff" })
        {
            host.Authenticate(host.Token(role));
            (await host.Client.PostAsJsonAsync($"/api/inventory/serials/{deviceId}/defective", defectiveReq)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await host.Client.PostAsJsonAsync($"/api/inventory/serials/{deviceId}/restore", restoreReq)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        // 200 OK for Admin and WarehouseStaff
        foreach (var role in new[] { "Admin", "WarehouseStaff" })
        {
            host.Authenticate(host.Token(role));
            (await host.Client.PostAsJsonAsync($"/api/inventory/serials/{deviceId}/defective", defectiveReq)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await host.Client.PostAsJsonAsync($"/api/inventory/serials/{deviceId}/restore", restoreReq)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        serials.ReportDefectiveCount.Should().Be(2);
        serials.RestoreCount.Should().Be(2);
    }

    [Fact]
    public async Task Defective_And_Restore_Should_Handle_Validation_NotFound_And_Conflict()
    {
        var serials = new FakeSerialImeiService();
        await using var host = new InventoryApiHost(services =>
        {
            services.AddSingleton<IInventoryService, FakeInventoryService>();
            services.AddSingleton<ISerialImeiService>(serials);
        });
        await host.StartAsync();

        host.Authenticate(host.Token("WarehouseStaff"));
        var deviceId = Guid.NewGuid();

        // 400 ValidationProblem
        serials.NextDeviceStatusResult = DeviceStatusResult.ValidationFailed(new() { ["reason"] = ["Lý do báo lỗi là bắt buộc."] });
        var valResp = await host.Client.PostAsJsonAsync($"/api/inventory/serials/{deviceId}/defective", new ReportDefectiveDeviceRequest());
        valResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 404 NotFound
        serials.NextDeviceStatusResult = DeviceStatusResult.NotFound();
        var nfResp = await host.Client.PostAsJsonAsync($"/api/inventory/serials/{deviceId}/defective", new ReportDefectiveDeviceRequest { Reason = "Lỗi" });
        nfResp.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 409 Conflict
        serials.NextDeviceStatusResult = DeviceStatusResult.Conflict("Thiết bị không ở trạng thái sẵn sàng trong kho.");
        var confResp = await host.Client.PostAsJsonAsync($"/api/inventory/serials/{deviceId}/defective", new ReportDefectiveDeviceRequest { Reason = "Lỗi" });
        confResp.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Restore 404 NotFound
        serials.NextDeviceStatusResult = DeviceStatusResult.NotFound();
        var restNfResp = await host.Client.PostAsJsonAsync($"/api/inventory/serials/{deviceId}/restore", new RestoreDeviceRequest());
        restNfResp.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Restore 409 Conflict
        serials.NextDeviceStatusResult = DeviceStatusResult.Conflict("Thiết bị không ở trạng thái báo lỗi.");
        var restConfResp = await host.Client.PostAsJsonAsync($"/api/inventory/serials/{deviceId}/restore", new RestoreDeviceRequest());
        restConfResp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}

public sealed class FakeInventoryService : IInventoryService
{
    public int GetStocksCount { get; private set; }
    public int InitializeCount { get; private set; }
    public int DeleteCount { get; private set; }
    public Guid LastPerformedByUserId { get; private set; }
    public AdjustInventoryStockResult? NextAdjustResult { get; set; }
    public InitializeStockResult? NextInitializeResult { get; set; }

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
        if (NextInitializeResult != null) return Task.FromResult(NextInitializeResult);
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
        return Task.FromResult(NextAdjustResult ?? AdjustInventoryStockResult.NotFound());
    }
}

public sealed class FakeSerialImeiService : ISerialImeiService
{
    public int GetSerialsCount { get; private set; }
    public int BatchImportCount { get; private set; }
    public int ReportDefectiveCount { get; private set; }
    public int RestoreCount { get; private set; }
    public Guid LastPerformedByUserId { get; private set; }
    public bool? LastIncludeCost { get; private set; }
    public DeviceStatusResult? NextDeviceStatusResult { get; set; }

    public Task<SerialImeiSearchResult> GetSerialsAsync(SerialImeiFilterRequest request, bool includeCost = false, CancellationToken cancellationToken = default)
    {
        GetSerialsCount++;
        LastIncludeCost = includeCost;
        return Task.FromResult(SerialImeiSearchResult.Success(new PagedResultDto<SerialImeiDto>
        {
            Items = [new SerialImeiDto { Id = Guid.NewGuid(), UnitCost = includeCost ? 15_000_000m : null }],
            Page = 1, PageSize = 20, TotalCount = 1, TotalPages = 1
        }));
    }

    public Task<BatchImportSerialImeiResult> BatchImportAsync(BatchImportSerialImeiRequest request, Guid performedByUserId, CancellationToken cancellationToken = default)
    {
        BatchImportCount++;
        LastPerformedByUserId = performedByUserId;
        return Task.FromResult(BatchImportSerialImeiResult.Success(request.Items?.Count ?? 0));
    }

    public Task<DeviceStatusResult> ReportDefectiveAsync(
        Guid deviceId,
        ReportDefectiveDeviceRequest request,
        Guid actorUserId,
        bool includeCost = false,
        CancellationToken cancellationToken = default)
    {
        ReportDefectiveCount++;
        LastPerformedByUserId = actorUserId;
        LastIncludeCost = includeCost;
        if (NextDeviceStatusResult != null) return Task.FromResult(NextDeviceStatusResult);
        return Task.FromResult(DeviceStatusResult.Success(new SerialImeiDto
        {
            Id = deviceId,
            Status = SerialImeiStatus.Defective,
            UnitCost = includeCost ? 15_000_000m : null
        }));
    }

    public Task<DeviceStatusResult> RestoreAsync(
        Guid deviceId,
        RestoreDeviceRequest request,
        Guid actorUserId,
        bool includeCost = false,
        CancellationToken cancellationToken = default)
    {
        RestoreCount++;
        LastPerformedByUserId = actorUserId;
        LastIncludeCost = includeCost;
        if (NextDeviceStatusResult != null) return Task.FromResult(NextDeviceStatusResult);
        return Task.FromResult(DeviceStatusResult.Success(new SerialImeiDto
        {
            Id = deviceId,
            Status = SerialImeiStatus.InStock,
            UnitCost = includeCost ? 15_000_000m : null
        }));
    }
}
