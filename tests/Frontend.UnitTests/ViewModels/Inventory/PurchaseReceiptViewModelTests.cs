using FluentAssertions;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Repositories.Inventory;
using TechStore.Client.ViewModels.Inventory;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.Client.UnitTests.ViewModels.Inventory;

public sealed class PurchaseReceiptViewModelTests
{
    private sealed class FakeReceiptRepository : IPurchaseReceiptRepository
    {
        public List<PurchaseReceiptSummary> Items { get; } = [];
        public PurchaseReceiptDetail? DetailItem { get; set; }
        public PurchaseReceiptSearchResult? CustomSearchResult { get; set; }
        public PurchaseReceiptDetailResult? CustomDetailResult { get; set; }
        public int GetReceiptsCallCount { get; private set; }

        public Task<PurchaseReceiptSearchResult> GetReceiptsAsync(PurchaseReceiptFilterRequest request, CancellationToken cancellationToken = default)
        {
            GetReceiptsCallCount++;
            if (CustomSearchResult != null) return Task.FromResult(CustomSearchResult);

            IEnumerable<PurchaseReceiptSummary> query = Items;
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                query = query.Where(r => r.PoNumber.Contains(request.Search, StringComparison.OrdinalIgnoreCase)
                    || (r.SupplierDocumentNumber != null && r.SupplierDocumentNumber.Contains(request.Search, StringComparison.OrdinalIgnoreCase)));
            }

            if (request.SupplierId.HasValue)
            {
                query = query.Where(r => r.SupplierId == request.SupplierId.Value);
            }

            var list = query.ToList();
            var page = request.Page ?? 1;
            var pageSize = request.PageSize ?? 20;
            var paged = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            var totalPages = (int)Math.Ceiling(list.Count / (double)pageSize);

            return Task.FromResult(new PurchaseReceiptSearchResult(
                true,
                paged,
                page,
                pageSize,
                list.Count,
                totalPages));
        }

        public Task<PurchaseReceiptDetailResult> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (CustomDetailResult != null) return Task.FromResult(CustomDetailResult);
            if (DetailItem != null && DetailItem.Id == id)
            {
                return Task.FromResult(new PurchaseReceiptDetailResult(true, DetailItem));
            }

            return Task.FromResult(new PurchaseReceiptDetailResult(false, null, ReceiptFailureKind.NotFound, "Not found"));
        }

        public Task<PurchaseReceiptCreateResult> CreateAsync(CreatePurchaseReceiptRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeSupplierRepository : ISupplierRepository
    {
        public List<SupplierItem> Suppliers { get; } = [];

        public Task<SupplierSearchResult> GetSuppliersAsync(SupplierFilterRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SupplierSearchResult(
                true,
                Suppliers,
                1,
                100,
                Suppliers.Count,
                1));
        }

        public Task<SupplierDetailResult> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SupplierMutationResult> CreateAsync(SaveSupplierRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SupplierMutationResult> UpdateAsync(Guid id, SaveSupplierRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SupplierMutationResult> SetStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    [Fact]
    public async Task LoadAsync_PopulatesReceipts_And_UpdatesPagination()
    {
        var repo = new FakeReceiptRepository();
        var supplierRepo = new FakeSupplierRepository();

        for (var i = 1; i <= 25; i++)
        {
            repo.Items.Add(new PurchaseReceiptSummary(
                Guid.NewGuid(),
                $"PO-{i:D4}",
                Guid.NewGuid(),
                "Supplier A",
                $"DOC-{i}",
                DateTime.UtcNow,
                Guid.NewGuid(),
                "User A",
                1,
                10,
                10_000_000m));
        }

        var vm = new PurchaseReceiptListViewModel(repo, supplierRepo) { PageSize = 10 };
        await vm.LoadAsync();

        vm.Items.Should().HaveCount(10);
        vm.TotalCount.Should().Be(25);
        vm.TotalPages.Should().Be(3);
        vm.Page.Should().Be(1);
        vm.PageButtons.Should().HaveCount(3);
        vm.IsEmpty.Should().BeFalse();
        vm.HasError.Should().BeFalse();
        vm.CanGoNext.Should().BeTrue();
        vm.CanGoPrevious.Should().BeFalse();

        await vm.NextPageCommand.ExecuteAsync(null);
        vm.Page.Should().Be(2);
        vm.CanGoPrevious.Should().BeTrue();
        vm.CanGoNext.Should().BeTrue();

        await vm.GoToPageCommand.ExecuteAsync(3);
        vm.Page.Should().Be(3);
        vm.Items.Should().HaveCount(5);
        vm.CanGoNext.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_WhenForbidden403_SetsIsForbidden_And_SpecificErrorMessage_WithoutRetryLoop()
    {
        var repo = new FakeReceiptRepository
        {
            CustomSearchResult = PurchaseReceiptSearchResult.Failed(ReceiptFailureKind.Forbidden, "Forbidden")
        };
        var supplierRepo = new FakeSupplierRepository();

        var vm = new PurchaseReceiptListViewModel(repo, supplierRepo);
        await vm.LoadAsync();

        vm.IsForbidden.Should().BeTrue();
        vm.ErrorMessage.Should().Be("Bạn không có quyền xem phiếu nhập.");
        vm.HasError.Should().BeTrue();
        vm.Items.Should().BeEmpty();
        repo.GetReceiptsCallCount.Should().Be(1);
    }

    [Fact]
    public async Task SearchText_And_SupplierFilter_ReloadsListFromPage1()
    {
        var supplier1 = Guid.NewGuid();
        var supplier2 = Guid.NewGuid();
        var repo = new FakeReceiptRepository();
        var supplierRepo = new FakeSupplierRepository();

        repo.Items.Add(new PurchaseReceiptSummary(
            Guid.NewGuid(), "PO-0001", supplier1, "Supplier 1", "HD-01", DateTime.UtcNow, null, null, 1, 1, 100m));
        repo.Items.Add(new PurchaseReceiptSummary(
            Guid.NewGuid(), "PO-0002", supplier2, "Supplier 2", "HD-02", DateTime.UtcNow, null, null, 1, 1, 100m));

        var vm = new PurchaseReceiptListViewModel(repo, supplierRepo);
        await vm.LoadAsync();
        vm.Items.Should().HaveCount(2);

        vm.SearchText = "HD-01";
        // Let filter event run
        await vm.LoadAsync();

        vm.Items.Should().HaveCount(1);
        vm.Items[0].PoNumber.Should().Be("PO-0001");

        vm.SearchText = string.Empty;
        vm.SelectedSupplierId = supplier2;
        await vm.LoadAsync();

        vm.Items.Should().HaveCount(1);
        vm.Items[0].PoNumber.Should().Be("PO-0002");
    }

    [Fact]
    public void ViewDetail_And_CreateReceipt_FireCorrespondingEvents()
    {
        var repo = new FakeReceiptRepository();
        var supplierRepo = new FakeSupplierRepository();
        var vm = new PurchaseReceiptListViewModel(repo, supplierRepo) { CanWrite = true };

        PurchaseReceiptSummary? detailRequested = null;
        var createRequested = false;

        vm.ReceiptDetailRequested += summary => detailRequested = summary;
        vm.CreateReceiptRequested += () => createRequested = true;

        var targetSummary = new PurchaseReceiptSummary(
            Guid.NewGuid(), "PO-0001", Guid.NewGuid(), "Supplier", null, null, null, null, 1, 1, 100m);

        vm.ViewDetailCommand.Execute(targetSummary);
        detailRequested.Should().Be(targetSummary);

        vm.CreateReceiptCommand.Execute(null);
        createRequested.Should().BeTrue();
    }

    [Fact]
    public async Task PurchaseReceiptDetailDialogViewModel_LoadAsync_PopulatesReceiptAndStatusLabels()
    {
        var receiptId = Guid.NewGuid();
        var detail = new PurchaseReceiptDetail(
            receiptId,
            "PO-202611-0099",
            Guid.NewGuid(),
            "Apple VN",
            "INV-999",
            Guid.NewGuid(),
            "Ghi chú kiểm tra",
            DateTime.UtcNow,
            Guid.NewGuid(),
            "Admin",
            30_000_000m,
            [
                new PurchaseReceiptLine(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "iPhone 16",
                    "IP16-128",
                    null,
                    null,
                    null,
                    2,
                    15_000_000m,
                    30_000_000m,
                    true,
                    12,
                    [
                        new PurchaseReceiptDevice(Guid.NewGuid(), null, "359999999999991", SerialImeiStatus.InStock),
                        new PurchaseReceiptDevice(Guid.NewGuid(), null, "359999999999992", SerialImeiStatus.Defective)
                    ])
            ]);

        var repo = new FakeReceiptRepository { DetailItem = detail };
        var vm = new PurchaseReceiptDetailDialogViewModel(repo);

        await vm.LoadAsync(receiptId);

        vm.HasError.Should().BeFalse();
        vm.Receipt.Should().NotBeNull();
        vm.Title.Should().Be("Chi tiết phiếu nhập PO-202611-0099");
        vm.Receipt!.Lines.Should().HaveCount(1);

        var devices = vm.Receipt.Lines[0].Devices;
        devices.Should().HaveCount(2);
        devices[0].StatusLabel.Should().Be("Trong kho");
        devices[1].StatusLabel.Should().Be("Lỗi / Hư hỏng");

        var closed = false;
        vm.RequestClose += (_, _) => closed = true;
        vm.CloseCommand.Execute(null);
        closed.Should().BeTrue();
    }
}
