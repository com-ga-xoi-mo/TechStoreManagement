using FluentAssertions;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Repositories.Inventory;
using TechStore.Client.ViewModels.Inventory;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.Client.UnitTests.ViewModels.Inventory;

public sealed class CreatePurchaseReceiptDialogViewModelTests
{
    private sealed class FakeProductRepository : IProductRepository
    {
        public List<ProductSummary> Products { get; } = [];
        public Dictionary<Guid, ProductDetail> Details { get; } = [];

        public Dictionary<Guid, TaskCompletionSource<ProductDetail?>> PendingDetails { get; } = [];
        public List<int> RequestedPages { get; } = [];

        public Task<ProductPage> SearchAsync(ProductQuery query, CancellationToken cancellationToken = default)
        {
            var page = query.Page;
            var size = query.PageSize;
            RequestedPages.Add(page);
            var items = Products.Skip((page - 1) * size).Take(size).ToList();
            var totalPages = (int)Math.Ceiling(Products.Count / (double)size);
            return Task.FromResult(new ProductPage(items, page, size, Products.Count, totalPages));
        }

        public List<CancellationToken> DetailTokens { get; } = [];

        public Task<ProductDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            DetailTokens.Add(cancellationToken);
            if (PendingDetails.TryGetValue(id, out var pending)) return pending.Task;
            Details.TryGetValue(id, out var detail);
            return Task.FromResult(detail);
        }
    }

    private sealed class FakeSupplierRepository : ISupplierRepository
    {
        public List<SupplierItem> Suppliers { get; } = [];

        public Task<SupplierSearchResult> GetSuppliersAsync(SupplierFilterRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SupplierSearchResult(true, Suppliers, 1, 100, Suppliers.Count, 1));

        public Task<SupplierDetailResult> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SupplierMutationResult> CreateAsync(SaveSupplierRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SupplierMutationResult> UpdateAsync(Guid id, SaveSupplierRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SupplierMutationResult> SetStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeReceiptRepository : IPurchaseReceiptRepository
    {
        public CreatePurchaseReceiptRequest? LastRequest { get; private set; }
        public PurchaseReceiptCreateResult? CustomCreateResult { get; set; }
        public int SubmitCallCount { get; private set; }

        public Task<PurchaseReceiptSearchResult> GetReceiptsAsync(PurchaseReceiptFilterRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PurchaseReceiptDetailResult> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<PurchaseReceiptCreateResult> CreateAsync(CreatePurchaseReceiptRequest request, CancellationToken cancellationToken = default)
        {
            SubmitCallCount++;
            LastRequest = request;
            if (CustomCreateResult != null) return Task.FromResult(CustomCreateResult);

            var detail = new PurchaseReceiptDetail(
                Guid.NewGuid(),
                "PO-SUCCESS",
                request.SupplierId!.Value,
                "Supplier",
                request.SupplierDocumentNumber,
                request.ClientRequestId,
                request.Note,
                DateTime.UtcNow,
                Guid.NewGuid(),
                "User",
                100_000m,
                []);

            return Task.FromResult(PurchaseReceiptCreateResult.Created(new PurchaseReceiptDto
            {
                Id = detail.Id,
                PoNumber = detail.PoNumber,
                SupplierId = detail.SupplierId,
                SupplierName = detail.SupplierName
            }));
        }
    }

    private static (CreatePurchaseReceiptDialogViewModel Vm, FakeProductRepository Products, FakeSupplierRepository Suppliers, FakeReceiptRepository Receipts) CreateFixture()
    {
        var products = new FakeProductRepository();
        var suppliers = new FakeSupplierRepository();
        var receipts = new FakeReceiptRepository();

        var vm = new CreatePurchaseReceiptDialogViewModel(products, suppliers, receipts);
        return (vm, products, suppliers, receipts);
    }

    [Fact]
    public void Validation_NoSupplier_And_NoLines()
    {
        // Act
        var (vm, _, _, _) = CreateFixture();

        // Assert
        vm.ValidationMessage.Should().Be("Vui lòng chọn nhà cung cấp.");
        vm.CanSubmit.Should().BeFalse();

        vm.SelectedSupplier = new SupplierItem(Guid.NewGuid(), "FPT", null, "0900", null, null, true, 0, DateTime.UtcNow, null);
        vm.ValidationMessage.Should().Be("Phiếu nhập phải có ít nhất một mặt hàng.");
        vm.CanSubmit.Should().BeFalse();
    }

    [Fact]
    public void Validation_LineChecks_Quantity_UnitCost_And_PreviewTotal()
    {
        // Arrange
        var (vm, _, _, _) = CreateFixture();
        vm.SelectedSupplier = new SupplierItem(Guid.NewGuid(), "FPT", null, "0900", null, null, true, 0, DateTime.UtcNow, null);

        var draft = new ReceiptLineDraft(Guid.NewGuid(), "Cáp USB-C", "CAB-01", null, null, null, false);
        vm.Lines.Add(draft);

        // Act
        // Quantity < 1
        draft.Quantity = 0;

        // Assert
        vm.ValidationMessage.Should().Contain("phải lớn hơn hoặc bằng 1");
        vm.CanSubmit.Should().BeFalse();

        // Invalid unit cost
        draft.Quantity = 5;
        draft.UnitCostText = "";
        vm.ValidationMessage.Should().Contain("không hợp lệ");
        vm.CanSubmit.Should().BeFalse();

        // Valid cost
        draft.UnitCostText = "50000";
        draft.LineTotalPreview.Should().Be(250_000m);
        vm.TotalCostPreview.Should().Be(250_000m);
        vm.TotalCostFormatted.Should().Contain("250.000");
        vm.ValidationMessage.Should().BeNull();
        vm.CanSubmit.Should().BeTrue();
    }

    [Fact]
    public void Validation_SerialTrackedLine_DeviceCountMismatch_And_Duplicates()
    {
        // Arrange
        var (vm, _, _, _) = CreateFixture();
        vm.SelectedSupplier = new SupplierItem(Guid.NewGuid(), "FPT", null, "0900", null, null, true, 0, DateTime.UtcNow, null);

        var draft = new ReceiptLineDraft(Guid.NewGuid(), "iPhone 15", "IP15", null, null, null, true)
        {
            Quantity = 3,
            UnitCostText = "15000000"
        };
        vm.Lines.Add(draft);

        // Assert: 0 codes entered vs 3 expected
        vm.ValidationMessage.Should().Be("Số mã Serial/IMEI (0) phải bằng số lượng nhập (3).");
        vm.CanSubmit.Should().BeFalse();

        // 2 codes entered vs 3 expected
        draft.SerialLines = "351234567890123\n351234567890124";
        vm.ValidationMessage.Should().Be("Số mã Serial/IMEI (2) phải bằng số lượng nhập (3).");
        vm.CanSubmit.Should().BeFalse();

        // Duplicate code in same line
        draft.SerialLines = "351234567890123\n351234567890124\n351234567890123";
        vm.ValidationMessage.Should().Contain("có mã trùng nhau");
        vm.CanSubmit.Should().BeFalse();

        // Duplicate code across lines
        var draft2 = new ReceiptLineDraft(Guid.NewGuid(), "iPad Pro", "IPAD", null, null, null, true)
        {
            Quantity = 1,
            UnitCostText = "20000000",
            SerialLines = "351234567890125"
        };
        draft.SerialLines = "351234567890123\n351234567890124\n351234567890125"; // 351234567890125 overlaps with draft2
        vm.Lines.Add(draft2);

        vm.ValidationMessage.Should().Contain("bị trùng lặp giữa các dòng");
        vm.CanSubmit.Should().BeFalse();
    }

    [Fact]
    public async Task SubmitAsync_KeepsSameClientRequestIdAcrossRetries_WhenNetworkFails()
    {
        // Arrange
        var (vm, _, _, receipts) = CreateFixture();
        var clientRequestId = vm.ClientRequestId;

        vm.SelectedSupplier = new SupplierItem(Guid.NewGuid(), "FPT", null, "0900", null, null, true, 0, DateTime.UtcNow, null);
        var draft = new ReceiptLineDraft(Guid.NewGuid(), "Sản phẩm A", "SKU-A", null, null, null, false)
        {
            Quantity = 2,
            UnitCostText = "100000"
        };
        vm.Lines.Add(draft);

        // Act
        // First attempt: network error
        receipts.CustomCreateResult = PurchaseReceiptCreateResult.Failed(ReceiptFailureKind.Network, "Connection timeout");
        await vm.SubmitAsync();

        // Assert
        vm.HasError.Should().BeTrue();
        vm.ErrorMessage.Should().Be(CreatePurchaseReceiptDialogViewModel.NetworkFailureMessage);
        vm.ClientRequestId.Should().Be(clientRequestId);
        receipts.LastRequest.Should().NotBeNull();
        receipts.LastRequest!.ClientRequestId.Should().Be(clientRequestId);

        // Second attempt: succeeds
        receipts.CustomCreateResult = null;
        var createdDetail = false;
        var closed = false;
        vm.ReceiptCreated += _ => createdDetail = true;
        vm.RequestClose += (_, _) => closed = true;

        await vm.SubmitAsync();

        receipts.SubmitCallCount.Should().Be(2);
        receipts.LastRequest!.ClientRequestId.Should().Be(clientRequestId);
        createdDetail.Should().BeTrue();
        closed.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitAsync_When409Conflict_KeepsFormOpenWithDuplicateIdentifiers()
    {
        // Arrange
        var (vm, _, _, receipts) = CreateFixture();
        vm.SelectedSupplier = new SupplierItem(Guid.NewGuid(), "FPT", null, "0900", null, null, true, 0, DateTime.UtcNow, null);
        var draft = new ReceiptLineDraft(Guid.NewGuid(), "iPhone 15", "IP15", null, null, null, true)
        {
            Quantity = 1,
            UnitCostText = "15000000",
            SerialLines = "359999999999999"
        };
        vm.Lines.Add(draft);

        var duplicates = new List<string> { "359999999999999" };
        receipts.CustomCreateResult = PurchaseReceiptCreateResult.Failed(
            ReceiptFailureKind.Conflict,
            "Mã Serial/IMEI đã tồn tại trong hệ thống.",
            duplicateIdentifiers: duplicates);

        var closed = false;
        vm.RequestClose += (_, _) => closed = true;

        // Act
        await vm.SubmitAsync();

        // Assert
        closed.Should().BeFalse(); // Dialog stays open!
        vm.HasError.Should().BeTrue();
        vm.ErrorMessage.Should().Be("Mã Serial/IMEI đã tồn tại trong hệ thống.");
        vm.HasDuplicateIdentifiers.Should().BeTrue();
        vm.DuplicateIdentifiers.Should().BeEquivalentTo(duplicates);
        vm.Lines.Should().HaveCount(1);
    }

    [Fact]
    public void CancelCommand_ClosesFormWithoutSendingRequest()
    {
        // Arrange
        var (vm, _, _, receipts) = CreateFixture();
        var closed = false;
        vm.RequestClose += (_, _) => closed = true;

        // Act
        vm.CancelCommand.Execute(null);

        // Assert
        closed.Should().BeTrue();
        receipts.SubmitCallCount.Should().Be(0);
        receipts.LastRequest.Should().BeNull();
    }

    private static SupplierItem Supplier() =>
        new(Guid.NewGuid(), "FPT", null, "0900", null, null, true, 0, DateTime.UtcNow, null);

    [Fact]
    public async Task SubmitAsync_WhenServerReturns400_ShowsVietnameseFieldErrorsWithLineNames()
    {
        // Arrange
        var (vm, _, _, receipts) = CreateFixture();
        vm.SelectedSupplier = Supplier();
        vm.Lines.Add(new ReceiptLineDraft(Guid.NewGuid(), "iPhone 16", "IP16", null, null, null, false)
        {
            Quantity = 2,
            UnitCostText = "100000"
        });
        receipts.CustomCreateResult = PurchaseReceiptCreateResult.Failed(
            ReceiptFailureKind.BadRequest,
            "The API request failed with HTTP status 400.",
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["lines[0].variantId"] = ["Sản phẩm có biến thể bắt buộc chọn biến thể."]
            });

        // Act
        await vm.SubmitAsync();

        // Assert
        vm.ErrorMessage.Should().Be("Dòng 1 (iPhone 16): Sản phẩm có biến thể bắt buộc chọn biến thể.");
        vm.ErrorMessage.Should().NotContain("HTTP status");
    }

    [Fact]
    public async Task SubmitAsync_WhenForbidden_ShowsPermissionMessage()
    {
        // Arrange
        var (vm, _, _, receipts) = CreateFixture();
        vm.SelectedSupplier = Supplier();
        vm.Lines.Add(new ReceiptLineDraft(Guid.NewGuid(), "Cáp", "CAB", null, null, null, false) { UnitCostText = "1000" });
        receipts.CustomCreateResult = PurchaseReceiptCreateResult.Failed(ReceiptFailureKind.Forbidden, "The API request failed with HTTP status 403.");

        // Act
        await vm.SubmitAsync();

        // Assert
        vm.ErrorMessage.Should().Be("Bạn không có quyền tạo phiếu nhập.");
    }

    [Fact]
    public void RemoveLineCommand_RemovesTheLineAndUpdatesTotals()
    {
        // Arrange
        var (vm, _, _, _) = CreateFixture();
        var line = new ReceiptLineDraft(Guid.NewGuid(), "Cáp", "CAB", null, null, null, false) { Quantity = 2, UnitCostText = "1000" };
        vm.Lines.Add(line);

        // Act
        vm.RemoveLineCommand.Execute(line);

        // Assert
        vm.Lines.Should().BeEmpty();
        vm.TotalCostPreview.Should().Be(0m);
    }

    [Fact]
    public void LineDraft_TextInputs_UpdateCountsAndTotalsImmediately()
    {
        // Arrange
        var line = new ReceiptLineDraft(Guid.NewGuid(), "iPhone", "IP", null, null, null, true);
        var changed = new List<string?>();
        line.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        line.QuantityText = "3";
        line.UnitCostText = "1.000";
        line.SerialLines = "356789012345671\n356789012345672;356789012345673";

        // Assert
        line.Quantity.Should().Be(3);
        line.LineTotalPreview.Should().Be(3_000m);
        line.DeviceCount.Should().Be(3);
        changed.Should().Contain([nameof(ReceiptLineDraft.LineTotalFormatted), nameof(ReceiptLineDraft.DeviceCount)]);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("100001")]
    public void Validation_RejectsQuantityThatIsNotAWholeNumberInRange(string quantity)
    {
        // Arrange
        var (vm, _, _, _) = CreateFixture();
        vm.SelectedSupplier = Supplier();
        var line = new ReceiptLineDraft(Guid.NewGuid(), "Cáp", "CAB", null, null, null, false) { UnitCostText = "1000" };
        vm.Lines.Add(line);

        // Act
        line.QuantityText = quantity;

        // Assert
        vm.ValidationMessage.Should().Contain("Số lượng");
        vm.CanSubmit.Should().BeFalse();
    }

    [Fact]
    public void Validation_RejectsWarrantyAbove120Months()
    {
        // Arrange
        var (vm, _, _, _) = CreateFixture();
        vm.SelectedSupplier = Supplier();
        var line = new ReceiptLineDraft(Guid.NewGuid(), "iPhone", "IP", null, null, null, true)
        {
            UnitCostText = "1000",
            SerialLines = "356789012345671"
        };
        vm.Lines.Add(line);

        // Act
        line.WarrantyMonthsText = "121";

        // Assert
        vm.ValidationMessage.Should().Contain("từ 0 đến 120 tháng");
    }

    [Fact]
    public async Task AddLine_IsBlockedWhileVariantsLoad_AndStaleVariantsAreIgnored()
    {
        // Arrange
        var (vm, products, _, _) = CreateFixture();
        var phoneId = Guid.NewGuid();
        var cableId = Guid.NewGuid();
        var pendingPhone = new TaskCompletionSource<ProductDetail?>();
        products.PendingDetails[phoneId] = pendingPhone;
        var phone = new ImportStockProductOption(phoneId, "iPhone 16", "IP16", "Điện thoại", true);
        var cable = new ImportStockProductOption(cableId, "Cáp", "CAB", "Phụ kiện", false);

        // Act: pick the phone, then the cable before the phone's variants arrive.
        vm.SelectedProduct = phone;
        var blockedWhileLoading = vm.CanAddLine;
        vm.SelectedProduct = cable;
        await Task.Yield();
        pendingPhone.SetResult(null);
        await Task.Yield();

        // Assert
        blockedWhileLoading.Should().BeFalse();
        vm.AddLineCommand.CanExecute(null).Should().BeTrue();
        vm.VariantOptions.Should().BeEmpty();
        vm.SelectedProduct.Should().Be(cable);
    }

    [Fact]
    public async Task LoadProductsAsync_LoadsEveryPage()
    {
        // Arrange
        var (vm, products, _, _) = CreateFixture();
        for (var i = 0; i < 150; i++)
        {
            products.Products.Add(new ProductSummary(
                Guid.NewGuid(), $"SP {i:D3}", $"SKU-{i:D3}", "Brand", Guid.NewGuid(), "Danh mục", 1000m, 1000m, 0, false, true, null));
        }

        // Act
        await vm.LoadProductsAsync();

        // Assert
        vm.ProductOptions.Should().HaveCount(150);
        products.RequestedPages.Should().Equal(1, 2);
    }

    [Fact]
    public void Validation_RejectsUnitCostAboveServerLimit()
    {
        // Arrange
        var (vm, _, _, _) = CreateFixture();
        vm.SelectedSupplier = Supplier();
        var line = new ReceiptLineDraft(Guid.NewGuid(), "Cáp", "CAB", null, null, null, false);
        vm.Lines.Add(line);

        // Act
        line.UnitCostText = "2.000.000.000.000";

        // Assert
        vm.ValidationMessage.Should().Be("Giá nhập của mặt hàng \"Cáp\" không được vượt quá 1.000.000.000.000 ₫.");
        vm.CanSubmit.Should().BeFalse();
    }

    [Fact]
    public void SelectingAnotherProduct_CancelsThePreviousVariantRequest()
    {
        // Arrange
        var (vm, products, _, _) = CreateFixture();
        var phoneId = Guid.NewGuid();
        products.PendingDetails[phoneId] = new TaskCompletionSource<ProductDetail?>();
        var phone = new ImportStockProductOption(phoneId, "iPhone 16", "IP16", "Điện thoại", true);
        var cable = new ImportStockProductOption(Guid.NewGuid(), "Cáp", "CAB", "Phụ kiện", false);

        // Act
        vm.SelectedProduct = phone;
        vm.SelectedProduct = cable;

        // Assert
        products.DetailTokens.Should().HaveCount(2);
        products.DetailTokens[0].IsCancellationRequested.Should().BeTrue();
        products.DetailTokens[1].IsCancellationRequested.Should().BeFalse();
    }
}
