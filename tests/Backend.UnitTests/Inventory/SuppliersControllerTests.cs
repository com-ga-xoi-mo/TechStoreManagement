using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TechStore.Api.Inventory.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.UnitTests.Inventory;

public sealed class SuppliersControllerTests
{
    private class FakeSupplierService : ISupplierService
    {
        public SupplierDto? ExistingSupplier { get; set; } = new()
        {
            Id = Guid.NewGuid(),
            Name = "Existing Supplier",
            Phone = "0123456789",
            IsActive = true
        };

        public Task<SupplierResult<PagedResultDto<SupplierDto>>> SearchAsync(SupplierFilterRequest request, CancellationToken cancellationToken = default)
        {
            var list = ExistingSupplier != null ? new List<SupplierDto> { ExistingSupplier } : [];
            return Task.FromResult(SupplierResult<PagedResultDto<SupplierDto>>.Success(
                new PagedResultDto<SupplierDto>(list, 1, 20, list.Count, 1)));
        }

        public Task<SupplierResult<SupplierDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (ExistingSupplier != null && ExistingSupplier.Id == id)
            {
                return Task.FromResult(SupplierResult<SupplierDto>.Success(ExistingSupplier));
            }
            return Task.FromResult(SupplierResult<SupplierDto>.NotFound());
        }

        public Task<SupplierResult<SupplierDto>> CreateAsync(SaveSupplierRequest request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Task.FromResult(SupplierResult<SupplierDto>.ValidationFailed(new() { ["name"] = ["Tên là bắt buộc."] }));
            }
            if (request.Name == "Duplicate")
            {
                return Task.FromResult(SupplierResult<SupplierDto>.Conflict("Tên nhà cung cấp đã tồn tại."));
            }

            var created = new SupplierDto
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Phone = request.Phone ?? "0123456789",
                IsActive = true
            };
            return Task.FromResult(SupplierResult<SupplierDto>.Success(created));
        }

        public Task<SupplierResult<SupplierDto>> UpdateAsync(Guid id, SaveSupplierRequest request, CancellationToken cancellationToken = default)
        {
            if (ExistingSupplier == null || ExistingSupplier.Id != id)
            {
                return Task.FromResult(SupplierResult<SupplierDto>.NotFound());
            }
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Task.FromResult(SupplierResult<SupplierDto>.ValidationFailed(new() { ["name"] = ["Tên là bắt buộc."] }));
            }
            if (request.Name == "Duplicate")
            {
                return Task.FromResult(SupplierResult<SupplierDto>.Conflict("Tên nhà cung cấp đã tồn tại."));
            }

            var updated = new SupplierDto
            {
                Id = id,
                Name = request.Name,
                Phone = request.Phone ?? ExistingSupplier.Phone,
                IsActive = ExistingSupplier.IsActive
            };
            return Task.FromResult(SupplierResult<SupplierDto>.Success(updated));
        }

        public Task<SupplierResult<SupplierDto>> SetStatusAsync(Guid id, SetSupplierStatusRequest request, CancellationToken cancellationToken = default)
        {
            if (ExistingSupplier == null || ExistingSupplier.Id != id)
            {
                return Task.FromResult(SupplierResult<SupplierDto>.NotFound());
            }
            if (!request.IsActive.HasValue)
            {
                return Task.FromResult(SupplierResult<SupplierDto>.ValidationFailed(new() { ["isActive"] = ["Bắt buộc."] }));
            }
            if (request.IsActive.Value && ExistingSupplier.Name == "LockedSystem")
            {
                return Task.FromResult(SupplierResult<SupplierDto>.Conflict("Không thể đổi trạng thái."));
            }

            ExistingSupplier.IsActive = request.IsActive.Value;
            return Task.FromResult(SupplierResult<SupplierDto>.Success(ExistingSupplier));
        }
    }

    [Fact]
    public async Task Suppliers_Should_Enforce_Authentication_And_Role_Permissions()
    {
        var service = new FakeSupplierService();
        await using var host = new InventoryApiHost(services =>
        {
            services.AddSingleton<ISupplierService>(service);
        });
        await host.StartAsync();

        var supplierId = service.ExistingSupplier!.Id;
        var saveRequest = new SaveSupplierRequest { Name = "New Supplier", Phone = "0123456789" };
        var statusRequest = new SetSupplierStatusRequest { IsActive = false };

        // 1. Unauthenticated -> 401 on all endpoints
        (await host.Client.GetAsync("/api/inventory/suppliers")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.Client.GetAsync($"/api/inventory/suppliers/{supplierId}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.Client.PostAsJsonAsync("/api/inventory/suppliers", saveRequest)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.Client.PutAsJsonAsync($"/api/inventory/suppliers/{supplierId}", saveRequest)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.Client.PatchAsJsonAsync($"/api/inventory/suppliers/{supplierId}/status", statusRequest)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 2. SalesStaff -> 403 on all routes
        host.Authenticate(host.Token("SalesStaff"));
        (await host.Client.GetAsync("/api/inventory/suppliers")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await host.Client.GetAsync($"/api/inventory/suppliers/{supplierId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await host.Client.PostAsJsonAsync("/api/inventory/suppliers", saveRequest)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await host.Client.PutAsJsonAsync($"/api/inventory/suppliers/{supplierId}", saveRequest)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await host.Client.PatchAsJsonAsync($"/api/inventory/suppliers/{supplierId}/status", statusRequest)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 3. Manager -> 200 on GET, 403 on writes (POST/PUT/PATCH)
        host.Authenticate(host.Token("Manager"));
        (await host.Client.GetAsync("/api/inventory/suppliers")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await host.Client.GetAsync($"/api/inventory/suppliers/{supplierId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await host.Client.PostAsJsonAsync("/api/inventory/suppliers", saveRequest)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await host.Client.PutAsJsonAsync($"/api/inventory/suppliers/{supplierId}", saveRequest)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await host.Client.PatchAsJsonAsync($"/api/inventory/suppliers/{supplierId}/status", statusRequest)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 4. WarehouseStaff and Admin -> allowed on all routes
        foreach (var role in new[] { "WarehouseStaff", "Admin" })
        {
            host.Authenticate(host.Token(role));
            (await host.Client.GetAsync("/api/inventory/suppliers")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await host.Client.GetAsync($"/api/inventory/suppliers/{supplierId}")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await host.Client.PostAsJsonAsync("/api/inventory/suppliers", saveRequest)).StatusCode.Should().Be(HttpStatusCode.Created);
            (await host.Client.PutAsJsonAsync($"/api/inventory/suppliers/{supplierId}", saveRequest)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await host.Client.PatchAsJsonAsync($"/api/inventory/suppliers/{supplierId}/status", statusRequest)).StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task CreateSupplier_Should_Return_201_With_Location_Header()
    {
        var service = new FakeSupplierService();
        await using var host = new InventoryApiHost(services =>
        {
            services.AddSingleton<ISupplierService>(service);
        });
        await host.StartAsync();
        host.Authenticate(host.Token("WarehouseStaff"));

        var response = await host.Client.PostAsJsonAsync("/api/inventory/suppliers", new SaveSupplierRequest
        {
            Name = "New Partner",
            Phone = "0987654321"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<SupplierDto>();
        created.Should().NotBeNull();
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be($"/api/inventory/suppliers/{created!.Id}");
    }

    [Fact]
    public async Task Suppliers_Should_Handle_400_404_409()
    {
        var service = new FakeSupplierService();
        await using var host = new InventoryApiHost(services =>
        {
            services.AddSingleton<ISupplierService>(service);
        });
        await host.StartAsync();
        host.Authenticate(host.Token("Admin"));

        var unknownId = Guid.NewGuid();
        var existingId = service.ExistingSupplier!.Id;

        // 400 Bad Request
        var badCreate = await host.Client.PostAsJsonAsync("/api/inventory/suppliers", new SaveSupplierRequest { Name = "" });
        badCreate.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var badUpdate = await host.Client.PutAsJsonAsync($"/api/inventory/suppliers/{existingId}", new SaveSupplierRequest { Name = "" });
        badUpdate.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var badPatch = await host.Client.PatchAsJsonAsync($"/api/inventory/suppliers/{existingId}/status", new SetSupplierStatusRequest { IsActive = null });
        badPatch.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 404 Not Found
        (await host.Client.GetAsync($"/api/inventory/suppliers/{unknownId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await host.Client.PutAsJsonAsync($"/api/inventory/suppliers/{unknownId}", new SaveSupplierRequest { Name = "Valid", Phone = "0123" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await host.Client.PatchAsJsonAsync($"/api/inventory/suppliers/{unknownId}/status", new SetSupplierStatusRequest { IsActive = true })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 409 Conflict
        var conflictCreate = await host.Client.PostAsJsonAsync("/api/inventory/suppliers", new SaveSupplierRequest { Name = "Duplicate", Phone = "0123" });
        conflictCreate.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var conflictUpdate = await host.Client.PutAsJsonAsync($"/api/inventory/suppliers/{existingId}", new SaveSupplierRequest { Name = "Duplicate", Phone = "0123" });
        conflictUpdate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
