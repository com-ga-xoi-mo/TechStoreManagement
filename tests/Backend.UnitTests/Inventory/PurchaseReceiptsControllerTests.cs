using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TechStore.Api.Inventory.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.UnitTests.Inventory;

public sealed class PurchaseReceiptsControllerTests
{
    private class FakePurchaseReceiptService : IPurchaseReceiptService
    {
        public PurchaseReceiptDto ExistingReceipt { get; set; } = new()
        {
            Id = Guid.NewGuid(),
            PoNumber = "PN-20261116-001",
            SupplierName = "FPT Trading",
            TotalCost = 500_000m
        };

        public Task<PurchaseReceiptSearchResult> SearchAsync(PurchaseReceiptFilterRequest request, CancellationToken cancellationToken = default)
        {
            var list = new List<PurchaseReceiptSummaryDto>
            {
                new()
                {
                    Id = ExistingReceipt.Id,
                    PoNumber = ExistingReceipt.PoNumber,
                    SupplierName = ExistingReceipt.SupplierName,
                    TotalCost = ExistingReceipt.TotalCost
                }
            };
            return Task.FromResult(PurchaseReceiptSearchResult.Success(
                new PagedResultDto<PurchaseReceiptSummaryDto>(list, 1, 20, 1, 1)));
        }

        public Task<PurchaseReceiptDetailResult> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (id == ExistingReceipt.Id)
            {
                return Task.FromResult(PurchaseReceiptDetailResult.Success(ExistingReceipt));
            }

            return Task.FromResult(PurchaseReceiptDetailResult.NotFound());
        }

        public Task<PurchaseReceiptResult> CreateAsync(CreatePurchaseReceiptRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
        {
            if (!request.ClientRequestId.HasValue)
            {
                return Task.FromResult(PurchaseReceiptResult.ValidationFailed(new() { ["clientRequestId"] = ["Required."] }));
            }

            if (request.Note == "Replay")
            {
                return Task.FromResult(PurchaseReceiptResult.Replayed(ExistingReceipt));
            }

            if (request.Note == "Conflict")
            {
                return Task.FromResult(PurchaseReceiptResult.Conflict("Duplicate code", ["IMEI-001"]));
            }

            var created = new PurchaseReceiptDto
            {
                Id = Guid.NewGuid(),
                PoNumber = "PN-NEW",
                SupplierName = "Supplier",
                TotalCost = 100_000m
            };
            return Task.FromResult(PurchaseReceiptResult.Created(created));
        }
    }

    [Fact]
    public async Task Receipts_Should_Enforce_Authentication_And_Role_Permissions()
    {
        var service = new FakePurchaseReceiptService();
        await using var host = new InventoryApiHost(services =>
        {
            services.AddSingleton<IPurchaseReceiptService>(service);
        });
        await host.StartAsync();

        var receiptId = service.ExistingReceipt.Id;
        var createRequest = new CreatePurchaseReceiptRequest
        {
            ClientRequestId = Guid.NewGuid(),
            SupplierId = Guid.NewGuid(),
            Lines = [new PurchaseReceiptLineRequest { ProductId = Guid.NewGuid(), Quantity = 1, UnitCost = 1000m }]
        };

        // 1. Unauthenticated -> 401
        (await host.Client.GetAsync("/api/inventory/purchase-receipts")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.Client.GetAsync($"/api/inventory/purchase-receipts/{receiptId}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.Client.PostAsJsonAsync("/api/inventory/purchase-receipts", createRequest)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 2. SalesStaff -> 403 on all routes
        host.Authenticate(host.Token("SalesStaff"));
        (await host.Client.GetAsync("/api/inventory/purchase-receipts")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await host.Client.GetAsync($"/api/inventory/purchase-receipts/{receiptId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await host.Client.PostAsJsonAsync("/api/inventory/purchase-receipts", createRequest)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 3. Manager -> 200 on GET, 403 on POST
        host.Authenticate(host.Token("Manager"));
        (await host.Client.GetAsync("/api/inventory/purchase-receipts")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await host.Client.GetAsync($"/api/inventory/purchase-receipts/{receiptId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await host.Client.PostAsJsonAsync("/api/inventory/purchase-receipts", createRequest)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 4. WarehouseStaff and Admin -> 200 on GET, 201 on POST
        foreach (var role in new[] { "WarehouseStaff", "Admin" })
        {
            host.Authenticate(host.Token(role));
            (await host.Client.GetAsync("/api/inventory/purchase-receipts")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await host.Client.GetAsync($"/api/inventory/purchase-receipts/{receiptId}")).StatusCode.Should().Be(HttpStatusCode.OK);
            var postResp = await host.Client.PostAsJsonAsync("/api/inventory/purchase-receipts", createRequest);
            postResp.StatusCode.Should().Be(HttpStatusCode.Created);
        }
    }

    [Fact]
    public async Task CreateReceipt_Should_Return_201_With_Location_Header_Or_200_On_Replay()
    {
        var service = new FakePurchaseReceiptService();
        await using var host = new InventoryApiHost(services =>
        {
            services.AddSingleton<IPurchaseReceiptService>(service);
        });
        await host.StartAsync();
        host.Authenticate(host.Token("WarehouseStaff"));

        // Normal create -> 201 Created with Location
        var response201 = await host.Client.PostAsJsonAsync("/api/inventory/purchase-receipts", new CreatePurchaseReceiptRequest
        {
            ClientRequestId = Guid.NewGuid(),
            SupplierId = Guid.NewGuid()
        });
        response201.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response201.Content.ReadFromJsonAsync<PurchaseReceiptDto>();
        created.Should().NotBeNull();
        response201.Headers.Location.Should().NotBeNull();
        response201.Headers.Location!.ToString().Should().Be($"/api/inventory/purchase-receipts/{created!.Id}");

        // Replay -> 200 OK
        var response200 = await host.Client.PostAsJsonAsync("/api/inventory/purchase-receipts", new CreatePurchaseReceiptRequest
        {
            ClientRequestId = Guid.NewGuid(),
            Note = "Replay"
        });
        response200.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateReceipt_Should_Handle_400_404_409()
    {
        var service = new FakePurchaseReceiptService();
        await using var host = new InventoryApiHost(services =>
        {
            services.AddSingleton<IPurchaseReceiptService>(service);
        });
        await host.StartAsync();
        host.Authenticate(host.Token("Admin"));

        // 400 Bad Request
        var badPost = await host.Client.PostAsJsonAsync("/api/inventory/purchase-receipts", new CreatePurchaseReceiptRequest());
        badPost.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 404 Not Found on detail
        var notFound = await host.Client.GetAsync($"/api/inventory/purchase-receipts/{Guid.NewGuid()}");
        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 409 Conflict with extensions
        var conflict = await host.Client.PostAsJsonAsync("/api/inventory/purchase-receipts", new CreatePurchaseReceiptRequest
        {
            ClientRequestId = Guid.NewGuid(),
            Note = "Conflict"
        });
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await conflict.Content.ReadFromJsonAsync<CustomProblemDetails>();
        problem.Should().NotBeNull();
        problem!.DuplicateIdentifiers.Should().Contain("IMEI-001");
    }

    private class CustomProblemDetails
    {
        public string? Title { get; set; }
        public string? Detail { get; set; }
        public List<string>? DuplicateIdentifiers { get; set; }
    }
}
