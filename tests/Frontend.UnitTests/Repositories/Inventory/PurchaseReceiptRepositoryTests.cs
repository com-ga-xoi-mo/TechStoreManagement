using System.Net;
using FluentAssertions;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Repositories.Inventory;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.Client.UnitTests.Repositories.Inventory;

public sealed class PurchaseReceiptRepositoryTests
{
    private sealed class MockApiClient : IApiClient
    {
        public string? LastPath { get; private set; }
        public string? LastMethod { get; private set; }
        public object? LastBody { get; private set; }
        public object? ResponseOrException { get; set; }
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

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

        public Task<ApiResponse<TResponse>> PostWithStatusAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default)
        {
            LastPath = relativePath;
            LastMethod = "POST";
            LastBody = request;
            if (ResponseOrException is Exception ex) return Task.FromException<ApiResponse<TResponse>>(ex);
            return Task.FromResult(new ApiResponse<TResponse>(StatusCode, (TResponse)ResponseOrException!));
        }

        public Task<TResponse> PutAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TResponse> PatchAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task PostAsync<TRequest>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task PatchAsync(string relativePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TResponse> PostFileAsync<TResponse>(string relativePath, ApiFileContent file, IReadOnlyDictionary<string, string>? fields = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    [Fact]
    public async Task GetReceiptsAsync_BuildsCorrectQueryString_And_MapsDtoToModel()
    {
        var supplierId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var paged = new PagedResultDto<PurchaseReceiptSummaryDto>
        {
            Items =
            [
                new PurchaseReceiptSummaryDto
                {
                    Id = receiptId,
                    PoNumber = "PO-202611-0001",
                    SupplierId = supplierId,
                    SupplierName = "FPT Trading",
                    SupplierDocumentNumber = "HD-9988",
                    ReceivedAt = now,
                    ReceivedByName = "Nguyen Van B",
                    LineCount = 2,
                    TotalQuantity = 10,
                    TotalCost = 50_000_000m
                }
            ],
            Page = 2,
            PageSize = 10,
            TotalCount = 25,
            TotalPages = 3
        };

        var mockApi = new MockApiClient { ResponseOrException = paged };
        var repository = new PurchaseReceiptRepository(mockApi);

        var result = await repository.GetReceiptsAsync(new PurchaseReceiptFilterRequest
        {
            Search = "PO-2026",
            SupplierId = supplierId,
            Page = 2,
            PageSize = 10
        });

        mockApi.LastMethod.Should().Be("GET");
        mockApi.LastPath.Should().Be($"api/inventory/purchase-receipts?search=PO-2026&supplierId={supplierId:D}&page=2&pageSize=10");

        result.Succeeded.Should().BeTrue();
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(10);
        result.TotalCount.Should().Be(25);
        result.TotalPages.Should().Be(3);
        result.Items.Should().HaveCount(1);

        var item = result.Items[0];
        item.Id.Should().Be(receiptId);
        item.PoNumber.Should().Be("PO-202611-0001");
        item.SupplierName.Should().Be("FPT Trading");
        item.SupplierDocumentNumber.Should().Be("HD-9988");
        item.ReceiverDisplay.Should().Be("Nguyen Van B");
        item.DocumentNumberDisplay.Should().Be("HD-9988");
        item.TotalCostFormatted.Should().Contain("50.000.000");
    }

    [Fact]
    public async Task GetByIdAsync_CallsCorrectPath_And_MapsDetailWithLinesAndDevices()
    {
        var receiptId = Guid.NewGuid();
        var dto = new PurchaseReceiptDto
        {
            Id = receiptId,
            PoNumber = "PO-100",
            SupplierName = "Samsung VN",
            TotalCost = 30_000_000m,
            Lines =
            [
                new PurchaseReceiptLineDto
                {
                    Id = Guid.NewGuid(),
                    ProductId = Guid.NewGuid(),
                    ProductName = "Galaxy S24",
                    ProductSku = "SS-S24",
                    Quantity = 2,
                    UnitCost = 15_000_000m,
                    LineTotal = 30_000_000m,
                    IsSerialTracked = true,
                    WarrantyMonths = 12,
                    Devices =
                    [
                        new PurchaseReceiptDeviceDto
                        {
                            Id = Guid.NewGuid(),
                            Imei = "351234567890123",
                            Status = SerialImeiStatus.InStock
                        },
                        new PurchaseReceiptDeviceDto
                        {
                            Id = Guid.NewGuid(),
                            Imei = "351234567890124",
                            Status = SerialImeiStatus.InStock
                        }
                    ]
                }
            ]
        };

        var mockApi = new MockApiClient { ResponseOrException = dto };
        var repository = new PurchaseReceiptRepository(mockApi);

        var result = await repository.GetByIdAsync(receiptId);

        mockApi.LastPath.Should().Be($"api/inventory/purchase-receipts/{receiptId:D}");
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.PoNumber.Should().Be("PO-100");
        result.Data.Lines.Should().HaveCount(1);
        result.Data.Lines[0].Devices.Should().HaveCount(2);
        result.Data.Lines[0].Devices[0].StatusLabel.Should().Be("Trong kho");
        result.Data.Lines[0].Devices[0].Identifier.Should().Be("351234567890123");
    }

    [Fact]
    public async Task CreateAsync_When201Created_ReturnsSuccessWithIsReplayFalse()
    {
        var dto = new PurchaseReceiptDto
        {
            Id = Guid.NewGuid(),
            PoNumber = "PO-NEW",
            SupplierName = "Apple VN",
            TotalCost = 25_000_000m
        };

        var mockApi = new MockApiClient
        {
            StatusCode = HttpStatusCode.Created,
            ResponseOrException = dto
        };
        var repository = new PurchaseReceiptRepository(mockApi);

        var request = new CreatePurchaseReceiptRequest
        {
            SupplierId = Guid.NewGuid(),
            ClientRequestId = Guid.NewGuid(),
            Lines = []
        };

        var result = await repository.CreateAsync(request);

        result.Succeeded.Should().BeTrue();
        result.IsReplay.Should().BeFalse();
        result.Data.Should().NotBeNull();
        result.Data!.PoNumber.Should().Be("PO-NEW");
    }

    [Fact]
    public async Task CreateAsync_When200Ok_ReturnsSuccessWithIsReplayTrue()
    {
        var dto = new PurchaseReceiptDto
        {
            Id = Guid.NewGuid(),
            PoNumber = "PO-REPLAY",
            SupplierName = "Apple VN",
            TotalCost = 25_000_000m
        };

        var mockApi = new MockApiClient
        {
            StatusCode = HttpStatusCode.OK,
            ResponseOrException = dto
        };
        var repository = new PurchaseReceiptRepository(mockApi);

        var request = new CreatePurchaseReceiptRequest
        {
            SupplierId = Guid.NewGuid(),
            ClientRequestId = Guid.NewGuid(),
            Lines = []
        };

        var result = await repository.CreateAsync(request);

        result.Succeeded.Should().BeTrue();
        result.IsReplay.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.PoNumber.Should().Be("PO-REPLAY");
    }

    [Fact]
    public async Task CreateAsync_When409ConflictWithDuplicateIdentifiers_ReturnsConflictFailureWithDuplicates()
    {
        var duplicates = new List<string> { "351234567890123", "351234567890124" };
        var exception = new ApiException(
            ApiErrorKind.Http,
            "Conflict error",
            statusCode: 409,
            detail: "Mã Serial/IMEI đã tồn tại trong hệ thống.",
            duplicateIdentifiers: duplicates);

        var mockApi = new MockApiClient { ResponseOrException = exception };
        var repository = new PurchaseReceiptRepository(mockApi);

        var result = await repository.CreateAsync(new CreatePurchaseReceiptRequest
        {
            SupplierId = Guid.NewGuid(),
            ClientRequestId = Guid.NewGuid(),
            Lines = []
        });

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().Be(ReceiptFailureKind.Conflict);
        result.Message.Should().Be("Mã Serial/IMEI đã tồn tại trong hệ thống.");
        result.DuplicateIdentifiers.Should().BeEquivalentTo(duplicates);
    }

    [Fact]
    public async Task CreateAsync_When400BadRequestWithFieldErrors_ReturnsBadRequestWithFieldErrors()
    {
        var fieldErrors = new Dictionary<string, IReadOnlyList<string>>
        {
            ["Lines"] = new List<string> { "Phiếu nhập phải có ít nhất một mặt hàng." }
        };
        var exception = new ApiException(
            ApiErrorKind.Http,
            "Validation failed",
            statusCode: 400,
            detail: "Dữ liệu không hợp lệ.",
            fieldErrors: fieldErrors);

        var mockApi = new MockApiClient { ResponseOrException = exception };
        var repository = new PurchaseReceiptRepository(mockApi);

        var result = await repository.CreateAsync(new CreatePurchaseReceiptRequest
        {
            SupplierId = Guid.NewGuid(),
            ClientRequestId = Guid.NewGuid(),
            Lines = []
        });

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().Be(ReceiptFailureKind.BadRequest);
        result.FieldErrors.Should().ContainKey("Lines");
    }

    [Theory]
    [InlineData(401, ReceiptFailureKind.Unauthorized)]
    [InlineData(403, ReceiptFailureKind.Forbidden)]
    [InlineData(404, ReceiptFailureKind.NotFound)]
    public async Task GetReceiptsAsync_MapsHttpStatusToExpectedFailureKind(int statusCode, ReceiptFailureKind expectedKind)
    {
        var exception = new ApiException(ApiErrorKind.Http, "Error", statusCode: statusCode);
        var mockApi = new MockApiClient { ResponseOrException = exception };
        var repository = new PurchaseReceiptRepository(mockApi);

        var result = await repository.GetReceiptsAsync(new PurchaseReceiptFilterRequest());

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().Be(expectedKind);
    }

    [Fact]
    public async Task CreateAsync_WhenNetworkException_ReturnsNetworkFailure()
    {
        var mockApi = new MockApiClient { ResponseOrException = new HttpRequestException("Connection refused") };
        var repository = new PurchaseReceiptRepository(mockApi);

        var result = await repository.CreateAsync(new CreatePurchaseReceiptRequest
        {
            SupplierId = Guid.NewGuid(),
            ClientRequestId = Guid.NewGuid(),
            Lines = []
        });

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().Be(ReceiptFailureKind.Network);
        result.Message.Should().BeNull("raw transport text is never shown; the ViewModel picks the Vietnamese message");
    }
}
