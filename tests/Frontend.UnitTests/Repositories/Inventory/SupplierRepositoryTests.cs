using FluentAssertions;
using TechStore.Client.Repositories.Inventory;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.Client.UnitTests.Repositories.Inventory;

public sealed class SupplierRepositoryTests
{
    private sealed class MockApiClient : IApiClient
    {
        public Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest request, TimeSpan timeout, CancellationToken cancellationToken = default) => PostAsync<TRequest, TResponse>(path, request, cancellationToken);

        public Task<ApiResponse<TResponse>> PostWithStatusAsync<TRequest, TResponse>(
            string relativePath,
            TRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public string? LastPath { get; private set; }
        public string? LastMethod { get; private set; }
        public object? LastBody { get; private set; }
        public object? ResponseOrException { get; set; }

        public Task<TResponse> GetAsync<TResponse>(string relativePath, CancellationToken cancellationToken = default)
        {
            LastPath = relativePath;
            LastMethod = "GET";
            if (ResponseOrException is Exception ex) return Task.FromException<TResponse>(ex);
            return Task.FromResult((TResponse)ResponseOrException!);
        }

        public Task<TResponse> PostAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default)
        {
            LastPath = relativePath;
            LastMethod = "POST";
            LastBody = request;
            if (ResponseOrException is Exception ex) return Task.FromException<TResponse>(ex);
            return Task.FromResult((TResponse)ResponseOrException!);
        }

        public Task<TResponse> PutAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default)
        {
            LastPath = relativePath;
            LastMethod = "PUT";
            LastBody = request;
            if (ResponseOrException is Exception ex) return Task.FromException<TResponse>(ex);
            return Task.FromResult((TResponse)ResponseOrException!);
        }

        public Task<TResponse> PatchAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default)
        {
            LastPath = relativePath;
            LastMethod = "PATCH";
            LastBody = request;
            if (ResponseOrException is Exception ex) return Task.FromException<TResponse>(ex);
            return Task.FromResult((TResponse)ResponseOrException!);
        }

        public Task PostAsync<TRequest>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task PatchAsync(string relativePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TResponse> PostFileAsync<TResponse>(string relativePath, ApiFileContent file, IReadOnlyDictionary<string, string>? fields = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    [Fact]
    public async Task GetSuppliersAsync_BuildsCorrectQueryString_And_MapsDtoToModel()
    {
        // Arrange
        var supplierId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var paged = new PagedResultDto<SupplierDto>
        {
            Items =
            [
                new SupplierDto
                {
                    Id = supplierId,
                    Name = "Công ty TNHH Apple",
                    ContactName = "Nguyen Van A",
                    Phone = "0987654321",
                    Email = "contact@apple.vn",
                    Address = "Hanoi",
                    IsActive = true,
                    ReceiptCount = 5,
                    CreatedAt = now,
                    UpdatedAt = now
                }
            ],
            Page = 2,
            PageSize = 10,
            TotalCount = 15,
            TotalPages = 2
        };

        var api = new MockApiClient { ResponseOrException = paged };
        var repo = new SupplierRepository(api);

        // Act
        var result = await repo.GetSuppliersAsync(new SupplierFilterRequest
        {
            Search = "Apple",
            IsActive = true,
            Page = 2,
            PageSize = 10
        });

        // Assert
        api.LastPath.Should().Be("api/inventory/suppliers?search=Apple&isActive=true&page=2&pageSize=10");
        result.Succeeded.Should().BeTrue();
        result.TotalCount.Should().Be(15);
        result.TotalPages.Should().Be(2);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(10);
        result.Items.Should().ContainSingle();

        var item = result.Items[0];
        item.Id.Should().Be(supplierId);
        item.Name.Should().Be("Công ty TNHH Apple");
        item.ContactName.Should().Be("Nguyen Van A");
        item.Phone.Should().Be("0987654321");
        item.Email.Should().Be("contact@apple.vn");
        item.Address.Should().Be("Hanoi");
        item.IsActive.Should().BeTrue();
        item.ReceiptCount.Should().Be(5);
        item.CreatedAt.Should().Be(now);
        item.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public async Task GetByIdAsync_BuildsCorrectPath_And_MapsDto()
    {
        // Arrange
        var id = Guid.NewGuid();
        var dto = new SupplierDto
        {
            Id = id,
            Name = "Samsung",
            Phone = "0123456789",
            IsActive = true
        };

        var api = new MockApiClient { ResponseOrException = dto };
        var repo = new SupplierRepository(api);

        // Act
        var result = await repo.GetByIdAsync(id);

        // Assert
        api.LastPath.Should().Be($"api/inventory/suppliers/{id:D}");
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Id.Should().Be(id);
        result.Data.Name.Should().Be("Samsung");
    }

    [Fact]
    public async Task CreateAsync_ForwardsRequest_And_MapsDto()
    {
        // Arrange
        var id = Guid.NewGuid();
        var request = new SaveSupplierRequest
        {
            Name = "Sony",
            Phone = "0909123456"
        };
        var dto = new SupplierDto
        {
            Id = id,
            Name = "Sony",
            Phone = "0909123456",
            IsActive = true
        };

        var api = new MockApiClient { ResponseOrException = dto };
        var repo = new SupplierRepository(api);

        // Act
        var result = await repo.CreateAsync(request);

        // Assert
        api.LastPath.Should().Be("api/inventory/suppliers");
        api.LastMethod.Should().Be("POST");
        api.LastBody.Should().Be(request);
        result.Succeeded.Should().BeTrue();
        result.Data!.Id.Should().Be(id);
    }

    [Fact]
    public async Task UpdateAsync_ForwardsRequest_And_MapsDto()
    {
        // Arrange
        var id = Guid.NewGuid();
        var request = new SaveSupplierRequest
        {
            Name = "Sony Vietnam",
            Phone = "0909123456"
        };
        var dto = new SupplierDto
        {
            Id = id,
            Name = "Sony Vietnam",
            Phone = "0909123456",
            IsActive = true
        };

        var api = new MockApiClient { ResponseOrException = dto };
        var repo = new SupplierRepository(api);

        // Act
        var result = await repo.UpdateAsync(id, request);

        // Assert
        api.LastPath.Should().Be($"api/inventory/suppliers/{id:D}");
        api.LastMethod.Should().Be("PUT");
        api.LastBody.Should().Be(request);
        result.Succeeded.Should().BeTrue();
        result.Data!.Name.Should().Be("Sony Vietnam");
    }

    [Fact]
    public async Task SetStatusAsync_ForwardsRequest_And_MapsDto()
    {
        // Arrange
        var id = Guid.NewGuid();
        var dto = new SupplierDto
        {
            Id = id,
            Name = "Sony",
            Phone = "0909123456",
            IsActive = false
        };

        var api = new MockApiClient { ResponseOrException = dto };
        var repo = new SupplierRepository(api);

        // Act
        var result = await repo.SetStatusAsync(id, false);

        // Assert
        api.LastPath.Should().Be($"api/inventory/suppliers/{id:D}/status");
        api.LastMethod.Should().Be("PATCH");
        var req = (SetSupplierStatusRequest)api.LastBody!;
        req.IsActive.Should().BeFalse();
        result.Succeeded.Should().BeTrue();
        result.Data!.IsActive.Should().BeFalse();
    }

    [Theory]
    [InlineData(400, SupplierFailureKind.BadRequest)]
    [InlineData(401, SupplierFailureKind.Unauthorized)]
    [InlineData(403, SupplierFailureKind.Forbidden)]
    [InlineData(404, SupplierFailureKind.NotFound)]
    [InlineData(409, SupplierFailureKind.Conflict)]
    public async Task Repository_MapsHttpStatusesCorrectly(int statusCode, SupplierFailureKind expectedFailure)
    {
        // Arrange
        var fieldErrors = new Dictionary<string, IReadOnlyList<string>>
        {
            ["name"] = ["Tên nhà cung cấp đã tồn tại."]
        };
        var apiEx = new ApiException(
            ApiErrorKind.Http,
            "Error",
            statusCode,
            detail: "Chi tiết lỗi",
            fieldErrors: statusCode == 400 ? fieldErrors : null);

        var api = new MockApiClient { ResponseOrException = apiEx };
        var repo = new SupplierRepository(api);

        // Act
        var getRes = await repo.GetSuppliersAsync(new SupplierFilterRequest());

        // Assert
        getRes.Succeeded.Should().BeFalse();
        getRes.Failure.Should().Be(expectedFailure);

        var mutRes = await repo.CreateAsync(new SaveSupplierRequest());
        mutRes.Succeeded.Should().BeFalse();
        mutRes.Failure.Should().Be(expectedFailure);
        mutRes.Message.Should().Be("Chi tiết lỗi");

        if (statusCode == 400)
        {
            mutRes.FieldErrors.Should().NotBeNull();
            mutRes.FieldErrors!.Should().ContainKey("name");
        }
    }

    [Fact]
    public async Task Repository_MapsNetworkErrorCorrectly()
    {
        // Arrange
        var apiEx = new ApiException(ApiErrorKind.Network, "Connection failed");
        var api = new MockApiClient { ResponseOrException = apiEx };
        var repo = new SupplierRepository(api);

        // Act
        var result = await repo.GetSuppliersAsync(new SupplierFilterRequest());

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure.Should().Be(SupplierFailureKind.Network);
    }
}
