using System.Threading.Tasks;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Repositories.Inventory;
using Xunit;

namespace TechStore.Client.Tests.Inventory;

public sealed class FakeInventoryRepositoryTests
{
    [Fact]
    public async Task FakeInventoryRepository_AdjustmentChangesOnlyExactPreviewBalanceOnSuccess()
    {
        var repository = new FakeInventoryRepository();
        var key = new StockBalanceKey("preview-product-06", "preview-variant-07");
        var result = await repository.AdjustStockAsync(new StockAdjustmentRequest(key, StockAdjustmentDirection.Increase, 2, "Kiểm kê"));

        Assert.Equal(StockAdjustmentOutcome.Succeeded, result.Outcome);
        Assert.True(result.IsPreviewData);
        Assert.Equal(8, result.Balance!.PhysicalQuantity);
        Assert.Equal(8, (await repository.GetStockDetailAsync(key))!.Balance.PhysicalQuantity);
        Assert.Equal(4, (await repository.GetStockDetailAsync(new StockBalanceKey("preview-product-07", "preview-variant-08")))!.Balance.PhysicalQuantity);
    }

    [Fact]
    public async Task FakeInventoryRepository_ConflictFailureUnknownAndSerialDoNotMutate()
    {
        var key = new StockBalanceKey("preview-product-06", "preview-variant-07");
        foreach (var scenario in new[] { StockAdjustmentPreviewScenario.Conflict, StockAdjustmentPreviewScenario.Error })
        {
            var repository = new FakeInventoryRepository(adjustmentScenario: scenario);
            var result = await repository.AdjustStockAsync(new StockAdjustmentRequest(key, StockAdjustmentDirection.Increase, 1, "test"));
            Assert.NotEqual(StockAdjustmentOutcome.Succeeded, result.Outcome);
            Assert.Equal(6, (await repository.GetStockDetailAsync(key))!.Balance.PhysicalQuantity);
        }

        var regularRepository = new FakeInventoryRepository();
        var serialKey = new StockBalanceKey("preview-product-01", "preview-variant-01");
        var serialResult = await regularRepository.AdjustStockAsync(new StockAdjustmentRequest(serialKey, StockAdjustmentDirection.Increase, 1, "test"));
        Assert.Equal(StockAdjustmentOutcome.Failed, serialResult.Outcome);
        Assert.Equal(8, (await regularRepository.GetStockDetailAsync(serialKey))!.Balance.PhysicalQuantity);

        var unknown = await regularRepository.AdjustStockAsync(new StockAdjustmentRequest(new StockBalanceKey("unknown"), StockAdjustmentDirection.Increase, 1, "test"));
        Assert.Equal(StockAdjustmentOutcome.Failed, unknown.Outcome);
    }

    [Fact]
    public async Task FakeInventoryRepository_AdjustmentValidatesRequestAndSupportsCancellation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StockAdjustmentRequest(new StockBalanceKey("p"), StockAdjustmentDirection.Increase, 0, "reason"));
        Assert.Throws<ArgumentException>(() => new StockAdjustmentRequest(new StockBalanceKey("p"), StockAdjustmentDirection.Increase, 1, "  "));
        var delayed = new FakeInventoryRepository(adjustmentScenario: StockAdjustmentPreviewScenario.Delayed);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => delayed.AdjustStockAsync(
            new StockAdjustmentRequest(new StockBalanceKey("preview-product-06", "preview-variant-07"), StockAdjustmentDirection.Increase, 1, "test"), cancellation.Token));
    }

    [Fact]
    public async Task FakeInventoryRepository_ReturnsMatchingPreviewDetailAndRepositoryMovements()
    {
        var repository = new FakeInventoryRepository();
        var page = await repository.GetStockPageAsync(new StockListQuery(PageSize: 20));
        var selected = page.Items[0];

        var detail = await repository.GetStockDetailAsync(new StockBalanceKey(selected.ProductId, selected.VariantId));

        Assert.NotNull(detail);
        Assert.Same(selected, detail.Balance);
        Assert.True(detail.IsPreviewData);
        Assert.Equal(5, detail.RecentMovements.Count);
        Assert.Equal("move-sale-20260318-1415", detail.RecentMovements[0].MovementId);
        Assert.Equal(8, detail.RecentMovements[0].PhysicalQuantityAfter);
        Assert.Equal("move-receipt-20240310-1400", detail.RecentMovements[^1].MovementId);
    }

    [Fact]
    public async Task FakeInventoryRepository_DoesNotReturnDetailForUnknownBalanceIdentity()
    {
        var repository = new FakeInventoryRepository();

        var detail = await repository.GetStockDetailAsync(new StockBalanceKey("unknown-product", "unknown-variant"));

        Assert.Null(detail);
    }

    [Fact]
    public async Task FakeInventoryRepository_MatchesBothProductAndVariantAndReturnsFreshSnapshots()
    {
        var repository = new FakeInventoryRepository();
        var key = new StockBalanceKey("preview-product-01", "preview-variant-01");
        var first = await repository.GetStockDetailAsync(key);
        var second = await repository.GetStockDetailAsync(key);
        var wrongVariant = await repository.GetStockDetailAsync(new StockBalanceKey("preview-product-01", "preview-variant-99"));

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
        Assert.Equal(first.Balance.VariantId, second.Balance.VariantId);
        Assert.Equal(first.RecentMovements.Select(m => m.MovementId), second.RecentMovements.Select(m => m.MovementId));
        Assert.Null(wrongVariant);
    }

    [Fact]
    public async Task FakeInventoryRepository_EmptyHistoryScenarioReturnsNoInventedMovements()
    {
        var repository = new FakeInventoryRepository(detailScenario: StockDetailPreviewScenario.EmptyHistory);
        var detail = await repository.GetStockDetailAsync(new StockBalanceKey("preview-product-01", "preview-variant-01"));

        Assert.NotNull(detail);
        Assert.Empty(detail.RecentMovements);
        Assert.Equal(8, detail.Balance.PhysicalQuantity);
    }

    [Fact]
    public async Task FakeInventoryRepository_DetailErrorIsDistinctFromMissingBalanceAndCanRetry()
    {
        var errorRepository = new FakeInventoryRepository(detailScenario: StockDetailPreviewScenario.Error);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            errorRepository.GetStockDetailAsync(new StockBalanceKey("preview-product-01", "preview-variant-01")));

        var retryRepository = new FakeInventoryRepository(detailScenario: StockDetailPreviewScenario.FailOnce);
        var key = new StockBalanceKey("preview-product-01", "preview-variant-01");
        await Assert.ThrowsAsync<InvalidOperationException>(() => retryRepository.GetStockDetailAsync(key));
        Assert.NotNull(await retryRepository.GetStockDetailAsync(key));
    }

    [Fact]
    public async Task FakeInventoryRepository_NotFoundAndCancellationAreExplicit()
    {
        var notFoundRepository = new FakeInventoryRepository(detailScenario: StockDetailPreviewScenario.NotFound);
        Assert.Null(await notFoundRepository.GetStockDetailAsync(new StockBalanceKey("preview-product-01")));

        var delayedRepository = new FakeInventoryRepository(detailScenario: StockDetailPreviewScenario.Delayed);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            delayedRepository.GetStockDetailAsync(new StockBalanceKey("preview-product-01", "preview-variant-01"), cancellation.Token));
    }

    [Fact]
    public async Task UnavailableInventoryRepository_DoesNotFallBackToPreviewDetail()
    {
        var repository = new UnavailableInventoryRepository();

        await Assert.ThrowsAsync<InventoryRepositoryUnavailableException>(
            () => repository.GetStockDetailAsync(new StockBalanceKey("product-1")));
    }

    [Fact]
    public async Task UnavailableInventoryRepository_DoesNotFallbackForAdjustment()
    {
        var repository = new UnavailableInventoryRepository();
        await Assert.ThrowsAsync<InventoryRepositoryUnavailableException>(() => repository.AdjustStockAsync(
            new StockAdjustmentRequest(new StockBalanceKey("p"), StockAdjustmentDirection.Increase, 1, "test")));
    }

    [Fact]
    public async Task FakeInventoryRepository_HasMultiplePagesAndCoherentPreviewBalances()
    {
        var repository = new FakeInventoryRepository();
        var firstPage = await repository.GetStockPageAsync(new StockListQuery(PageSize: 8));
        var secondPage = await repository.GetStockPageAsync(new StockListQuery(PageNumber: 2, PageSize: 8));
        var all = await repository.GetStockPageAsync(new StockListQuery(PageSize: 100));

        Assert.Equal(20, firstPage.TotalItems);
        Assert.Equal(8, firstPage.Items.Count);
        Assert.Equal(8, secondPage.Items.Count);
        Assert.True(firstPage.IsPreviewData);
        Assert.Equal(all.Items[0].ProductId, all.Items[1].ProductId);
        Assert.NotEqual(all.Items[0].VariantId, all.Items[1].VariantId);
        Assert.All(all.Items, row =>
        {
            Assert.Contains(row.CategoryId, new[] { "phones", "laptops", "tablets", "accessories" });
            Assert.Equal(row.PhysicalQuantity - row.ReservedQuantity, row.AvailableQuantity);
            Assert.Equal(row.AvailableQuantity == 0
                    ? InventoryStockStatus.OutOfStock
                    : row.AvailableQuantity <= row.MinimumStockAlert
                        ? InventoryStockStatus.LowStock
                        : InventoryStockStatus.Available,
                row.Status);
        });
    }

    [Fact]
    public async Task FakeInventoryRepository_CombinesSearchCategoryAndStatusBeforePaging()
    {
        var repository = new FakeInventoryRepository();
        var page = await repository.GetStockPageAsync(new StockListQuery(
            SearchText: "iphone",
            CategoryId: "phones",
            Status: InventoryStockStatus.LowStock,
            PageNumber: 1,
            PageSize: 8));

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, row =>
        {
            Assert.Contains("iphone", row.ProductName.ToLowerInvariant());
            Assert.Equal("phones", row.CategoryId);
            Assert.Equal(InventoryStockStatus.LowStock, row.Status);
        });
    }

    [Fact]
    public async Task FakeInventoryRepository_SearchMatchesProductAndVariantSkus()
    {
        var repository = new FakeInventoryRepository();

        var productSku = await repository.GetStockPageAsync(new StockListQuery(SearchText: "SAM-S24U", PageSize: 8));
        var variantSku = await repository.GetStockPageAsync(new StockListQuery(SearchText: "APL-IP15PM-02", PageSize: 8));

        Assert.Single(productSku.Items);
        Assert.Equal("Samsung Galaxy S24 Ultra", productSku.Items[0].ProductName);
        Assert.Single(variantSku.Items);
        Assert.Equal("512GB Titan", variantSku.Items[0].VariantName);
    }

    [Fact]
    public async Task FakeInventoryRepository_ReturnsEmptyForUnmatchedCriteria()
    {
        var repository = new FakeInventoryRepository();

        var page = await repository.GetStockPageAsync(new StockListQuery(SearchText: "not-a-product"));

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalItems);
        Assert.Equal(0, page.TotalPages);
    }

    [Fact]
    public async Task FakeInventoryRepository_PagingUsesTotalFilteredCountAndOneBasedPageNumbers()
    {
        var repository = new FakeInventoryRepository();

        var page = await repository.GetStockPageAsync(new StockListQuery(PageNumber: 3, PageSize: 8));

        Assert.Equal(20, page.TotalItems);
        Assert.Equal(3, page.PageNumber);
        Assert.Equal(4, page.Items.Count);
        Assert.Equal(17, page.FirstItemNumber);
        Assert.Equal(20, page.LastItemNumber);
    }
}
