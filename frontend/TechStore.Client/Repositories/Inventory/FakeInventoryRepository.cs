using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TechStore.Client.Models.Inventory;

namespace TechStore.Client.Repositories.Inventory;

/// <summary>Deterministic inventory rows for the explicitly enabled UI preview.</summary>
public sealed class FakeInventoryRepository : IInventoryRepository
{
    private readonly StockListPreviewScenario _scenario;
    private readonly StockDetailPreviewScenario _detailScenario;
    private readonly StockAdjustmentPreviewScenario _adjustmentScenario;
    private readonly Dictionary<(string ProductId, string? VariantId), StockBalance> _adjustedBalances = [];
    private int _detailCalls;
    private static readonly IReadOnlyList<StockBalance> PreviewBalances = CreatePreviewBalances();
    private static readonly IReadOnlyList<StockMovement> IPhoneMovements =
    [
        new("move-sale-20260318-1415", new DateTimeOffset(2026, 3, 18, 14, 15, 0, TimeSpan.Zero), StockMovementType.Sale, -1, 8, "Xuất bán lẻ đơn hàng #DH-8920"),
        new("move-sale-20260318-1030", new DateTimeOffset(2026, 3, 18, 10, 30, 0, TimeSpan.Zero), StockMovementType.Sale, -1, 9, "Xuất bán lẻ đơn hàng #DH-8917"),
        new("move-adjust-20240314-1645", new DateTimeOffset(2024, 3, 14, 16, 45, 0, TimeSpan.Zero), StockMovementType.Adjustment, -1, 10, "Chuyển hàng trưng bày Showroom"),
        new("move-return-20240312-0920", new DateTimeOffset(2024, 3, 12, 9, 20, 0, TimeSpan.Zero), StockMovementType.Return, 1, 11, "Khách đổi trả bảo hành #DH-8890"),
        new("move-receipt-20240310-1400", new DateTimeOffset(2024, 3, 10, 14, 0, 0, TimeSpan.Zero), StockMovementType.Receipt, 10, 10, "Nhập kho lô NK-0391")
    ];

    public FakeInventoryRepository(
        StockListPreviewScenario scenario = StockListPreviewScenario.Populated,
        StockDetailPreviewScenario detailScenario = StockDetailPreviewScenario.Populated,
        StockAdjustmentPreviewScenario adjustmentScenario = StockAdjustmentPreviewScenario.Success)
    {
        _scenario = scenario;
        _detailScenario = detailScenario;
        _adjustmentScenario = adjustmentScenario;
    }

    public async Task<StockListPage> GetStockPageAsync(
        StockListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        if (_scenario == StockListPreviewScenario.Delayed)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
        }

        if (_scenario == StockListPreviewScenario.Error)
        {
            throw new InvalidOperationException("Inventory stock preview load failure.");
        }

        var source = _scenario == StockListPreviewScenario.Empty
            ? Array.Empty<StockBalance>()
            : PreviewBalances.Select(GetCurrentBalance).ToArray();
        var search = query.SearchText?.Trim();
        IEnumerable<StockBalance> filtered = source;

        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(row =>
                row.ProductName.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                row.ProductSku.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (row.VariantSku?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(query.CategoryId))
        {
            filtered = filtered.Where(row => string.Equals(row.CategoryId, query.CategoryId, StringComparison.Ordinal));
        }

        if (query.Status != InventoryStockStatus.All)
        {
            filtered = filtered.Where(row => row.Status == query.Status);
        }

        var matches = filtered.ToArray();
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Max(1, query.PageSize);
        var items = matches.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        return new StockListPage(items, matches.Length, pageNumber, pageSize, IsPreviewData: true);
    }

    public async Task<StockDetailSnapshot?> GetStockDetailAsync(
        StockBalanceKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();

        if (_detailScenario == StockDetailPreviewScenario.Delayed)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
        }

        if (_detailScenario == StockDetailPreviewScenario.Error ||
            (_detailScenario == StockDetailPreviewScenario.FailOnce && Interlocked.Increment(ref _detailCalls) == 1))
        {
            throw new InvalidOperationException("Inventory detail preview load failure.");
        }

        if (_detailScenario == StockDetailPreviewScenario.NotFound)
        {
            return null;
        }

        var balance = PreviewBalances.Select(GetCurrentBalance).FirstOrDefault(item =>
            string.Equals(item.ProductId, key.ProductId, StringComparison.Ordinal) &&
            string.Equals(item.VariantId, key.VariantId, StringComparison.Ordinal));
        if (balance is null)
        {
            return null;
        }

        var movements = _detailScenario != StockDetailPreviewScenario.EmptyHistory &&
            string.Equals(balance.VariantId, "preview-variant-01", StringComparison.Ordinal)
            ? IPhoneMovements
            : Array.Empty<StockMovement>();
        StockDetailSnapshot snapshot = new(
            balance,
            movements,
            DateTimeOffset.UtcNow,
            isPreviewData: true);
        return snapshot;
    }

    public async Task<StockAdjustmentResult> AdjustStockAsync(
        StockAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (_adjustmentScenario == StockAdjustmentPreviewScenario.Delayed)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(350), cancellationToken);
        }

        var key = (request.Key.ProductId, request.Key.VariantId);
        var balance = PreviewBalances.Select(GetCurrentBalance).FirstOrDefault(item =>
            string.Equals(item.ProductId, key.ProductId, StringComparison.Ordinal) &&
            string.Equals(item.VariantId, key.VariantId, StringComparison.Ordinal));
        if (balance is null)
        {
            return StockAdjustmentResult.Failure("Không tìm thấy số dư tồn kho này.", isPreviewData: true);
        }

        if (balance.IsSerialTracked is not false)
        {
            return StockAdjustmentResult.Failure("Không thể điều chỉnh trực tiếp mặt hàng serial/IMEI.", isPreviewData: true);
        }

        if (_adjustmentScenario == StockAdjustmentPreviewScenario.Conflict)
        {
            return StockAdjustmentResult.Conflict("Số tồn đã thay đổi. Hãy tải lại trước khi quyết định gửi lại.", isPreviewData: true);
        }

        if (_adjustmentScenario == StockAdjustmentPreviewScenario.Error)
        {
            return StockAdjustmentResult.Failure("Không thể điều chỉnh số tồn trong bản Preview.", isPreviewData: true);
        }

        var delta = request.Direction == StockAdjustmentDirection.Increase ? request.Quantity : -request.Quantity;
        var physicalQuantity = balance.PhysicalQuantity + delta;
        if (physicalQuantity < balance.ReservedQuantity)
        {
            return StockAdjustmentResult.Conflict("Số lượng mới thấp hơn lượng đang giữ.", isPreviewData: true);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var updated = new StockBalance(
            balance.ProductId, balance.ProductName, balance.ProductSku,
            balance.VariantId, balance.VariantName, balance.VariantSku,
            balance.CategoryId, balance.CategoryName, balance.SellingPrice,
            physicalQuantity, balance.ReservedQuantity, balance.MinimumStockAlert,
            balance.IsSerialTracked);
        _adjustedBalances[key] = updated;
        return StockAdjustmentResult.Success(updated, isPreviewData: true);
    }

    private StockBalance GetCurrentBalance(StockBalance balance) =>
        _adjustedBalances.TryGetValue((balance.ProductId, balance.VariantId), out var adjusted)
            ? adjusted
            : balance;

    private static IReadOnlyList<StockBalance> CreatePreviewBalances()
    {
        var entries = new (string Name, string Sku, string Category, string CategoryName, string Variant, decimal Price, int Physical, int Reserved, int Alert, bool IsSerialTracked)[]
        {
            ("iPhone 15 Pro Max", "APL-IP15PM", "phones", "Điện thoại", "256GB Titan", 34_990_000m, 8, 2, 3, true),
            ("iPhone 15 Pro Max", "APL-IP15PM", "phones", "Điện thoại", "512GB Titan", 40_990_000m, 3, 1, 2, true),
            ("Samsung Galaxy S24 Ultra", "SAM-S24U", "phones", "Điện thoại", "512GB Gray", 33_990_000m, 5, 1, 2, true),
            ("Google Pixel 9 Pro", "GOO-PX9P", "phones", "Điện thoại", "256GB Hazel", 27_990_000m, 2, 1, 2, true),
            ("Xiaomi 14T Pro", "XIA-14TP", "phones", "Điện thoại", "512GB Black", 16_990_000m, 10, 2, 3, true),
            ("OPPO Reno 12", "OPP-R12", "phones", "Điện thoại", "256GB Sunset", 12_490_000m, 1, 1, 1, true),
            ("MacBook Air M3", "APL-MBA-M3", "laptops", "Laptop", "13 inch · 16GB", 32_990_000m, 6, 1, 2, false),
            ("MacBook Pro M3 Pro", "APL-MBP-M3P", "laptops", "Laptop", "14 inch · 18GB", 52_990_000m, 4, 2, 2, false),
            ("ASUS Zenbook 14 OLED", "ASU-ZB14", "laptops", "Laptop", "UX3405 · 16GB", 29_990_000m, 3, 0, 2, false),
            ("Dell XPS 13", "DEL-XPS13", "laptops", "Laptop", "9340 · 16GB", 36_990_000m, 0, 0, 2, false),
            ("Lenovo ThinkPad X1 Carbon", "LEN-X1C", "laptops", "Laptop", "Gen 12 · 32GB", 49_990_000m, 7, 2, 2, false),
            ("Acer Nitro V 15", "ACE-NV15", "laptops", "Laptop", "RTX 4050 · 16GB", 24_990_000m, 9, 1, 3, false),
            ("iPad Air M2", "APL-IPA-M2", "tablets", "Tablet", "11 inch · Wi-Fi", 16_990_000m, 5, 1, 2, false),
            ("iPad Pro M4", "APL-IPP-M4", "tablets", "Tablet", "13 inch · 256GB", 34_990_000m, 2, 0, 2, false),
            ("Samsung Galaxy Tab S9", "SAM-TABS9", "tablets", "Tablet", "256GB Wi-Fi", 19_990_000m, 4, 1, 2, false),
            ("Xiaomi Pad 6S Pro", "XIA-PAD6SP", "tablets", "Tablet", "256GB Graphite", 13_990_000m, 8, 0, 3, false),
            ("AirPods Pro 2", "APL-APP2", "accessories", "Phụ kiện", "USB-C", 6_190_000m, 14, 2, 4, false),
            ("Logitech MX Master 3S", "LOG-MXM3S", "accessories", "Phụ kiện", "Graphite", 2_490_000m, 3, 1, 2, false),
            ("Keychron K8 Pro", "KEY-K8P", "accessories", "Phụ kiện", "Brown Switch", 2_890_000m, 6, 0, 2, false),
            ("Samsung 45W Adapter", "SAM-45WA", "accessories", "Phụ kiện", "USB-C Black", 990_000m, 0, 0, 1, false)
        };

        var productIds = entries.Select(entry => entry.Name)
            .Distinct(StringComparer.Ordinal)
            .Select((name, index) => (name, id: $"preview-product-{index + 1:D2}"))
            .ToDictionary(item => item.name, item => item.id, StringComparer.Ordinal);

        return entries.Select((entry, index) => new StockBalance(
            productIds[entry.Name],
            entry.Name,
            entry.Sku,
            $"preview-variant-{index + 1:D2}",
            entry.Variant,
            $"{entry.Sku}-{index + 1:D2}",
            entry.Category,
            entry.CategoryName,
            entry.Price,
            entry.Physical,
            entry.Reserved,
            entry.Alert,
            entry.IsSerialTracked)).ToArray();
    }
}
