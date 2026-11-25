using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using TechStore.Api.Analytics.Services;
using TechStore.Api.Catalog.Services;
using TechStore.Api.Common.Ai;
using TechStore.Api.Inventory.Services;
using TechStore.Shared;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Api.Assistant.Tools;

public sealed record AssistantToolResult(string Content, bool DataRead);

internal sealed record ProductItem(string Name, string Sku, string Brand, string Category, decimal Price, Dictionary<string, JsonElement> Specs, IReadOnlyList<VariantItem> Variants);
internal sealed record VariantItem(string Name, string Sku, decimal Price, Dictionary<string, JsonElement> Specs);
internal sealed record StockItem(string ProductName, string? VariantName, string Sku, string Category, int Available, int Reserved, int MinStockAlert, decimal? SellingPrice);
internal sealed record RevenueItem(DateOnly Date, decimal Revenue);
internal sealed record LowStockItem(string ProductName, string? VariantName, int Available);
internal sealed record ReportItem(string Period, DateOnly StartDate, DateOnly EndDate, string TimeZone, string AsOf, decimal Revenue, int OrderCount, decimal? RevenueChangePercent, decimal? OrderCountChangePercent, IReadOnlyList<RevenueItem> DailyRevenue, IReadOnlyList<LowStockItem> LowStock);
// Total = all matching rows, so the model can tell the user a list was cut to `limit`.
internal sealed record FinancialSummaryItem(long CompletedOrderCount, long Quantity, decimal SalesExcludingTax, decimal SalesTax, decimal PaymentReceivedAmount, decimal? RefundPaidAmount, decimal? RefundedSalesExcludingTax, decimal? NetSalesExcludingTax, decimal? CostOfGoodsSold, decimal? RestockedCost, decimal? NetCostOfGoodsSold, decimal? SalesGrossProfit, decimal? NetGrossProfit, decimal? WarrantyCost);
internal sealed record TopProductItem(string ProductName, long Quantity, decimal SalesExcludingTax);
internal sealed record CashierItem(string CashierName, long CompletedOrderCount, long Quantity, decimal SalesExcludingTax, decimal? SalesGrossProfit);
// Whitelist of Bình's financial report: no product/cashier/shift ids, no daily rows, no Cash/VietQR channel split.
internal sealed record FinancialReportItem(DateOnly StartDate, DateOnly EndDate, string AsOf, FinancialSummaryItem Summary, IReadOnlyList<TopProductItem> TopProductsByQuantity, IReadOnlyList<TopProductItem> TopProductsByRevenue, IReadOnlyList<CashierItem> CashierPerformance);
internal sealed record ItemsResult<T>(int Total, IReadOnlyList<T> Items);
internal sealed record ErrorResult(string Error);

public sealed class AssistantTools(IProductService products, IInventoryService inventory, IAnalyticsService analytics, ICategoryService categories, TimeProvider clock)
{
    // Server converts to Vietnam time so the model never shows raw UTC (e.g. 18:04Z read as the previous day).
    internal static string VietnamTime(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    private static ChatTool Tool(string name, string description, string schema) => new(name, description, JsonDocument.Parse(schema).RootElement.Clone());
    private static readonly ChatTool Search = Tool("search_products", "Tìm sản phẩm đang hoạt động.", """{"type":"object","properties":{"query":{"type":"string","maxLength":100,"description":"Từ khóa trong tên, SKU hoặc hãng"},"category":{"type":"string","maxLength":100,"description":"Tên danh mục, ví dụ Laptops, Smartphones, Tablets, Monitors, Accessories; dùng khi hỏi theo loại sản phẩm"},"brand":{"type":"string","maxLength":100},"min_price":{"type":"number","minimum":0,"description":"Giá bán tối thiểu, VND"},"max_price":{"type":"number","minimum":0,"description":"Giá bán tối đa, VND"},"specs":{"type":"array","maxItems":5,"description":"Lọc thông số, mỗi phần tử dạng key:value, key snake_case, ví dụ ram_gb:16, storage_gb:512, cpu:Apple M3","items":{"type":"string","pattern":"^[a-z][a-z0-9_]*:[^:]*\\S[^:]*$"}},"limit":{"type":"integer","minimum":1,"maximum":10,"default":5}}}""");
    private static readonly ChatTool Stock = Tool("get_stock", "Tra cứu tồn kho.", """{"type":"object","properties":{"query":{"type":"string","maxLength":100},"category":{"type":"string","maxLength":100,"description":"Tên danh mục, ví dụ Laptops, Tablets"},"only_available":{"type":"boolean","description":"Chỉ dòng còn hàng"},"low_stock":{"type":"boolean","description":"Còn hàng nhưng dưới ngưỡng cảnh báo"},"out_of_stock":{"type":"boolean","description":"Đã hết hàng (tồn 0)"},"limit":{"type":"integer","minimum":1,"maximum":10,"default":5}}}""");
    private static readonly ChatTool Report = Tool("get_sales_report", "Tổng quan nhanh: doanh thu gộp theo ngày, số đơn, % thay đổi so với kỳ trước, hàng sắp hết.", """{"type":"object","properties":{"period":{"type":"string","enum":["today","last7Days","thisMonth","custom"]},"from":{"type":"string","format":"date"},"to":{"type":"string","format":"date"}},"required":["period"]}""");
    private static readonly ChatTool Financial = Tool("get_financial_report", "Báo cáo tài chính: doanh thu thuần sau hoàn, thuế, tiền thu, tiền hoàn, giá vốn, lãi gộp, chi phí bảo hành, sản phẩm bán chạy, hiệu suất thu ngân.", """{"type":"object","properties":{"period":{"type":"string","enum":["today","last7Days","thisMonth","custom"]},"from":{"type":"string","format":"date"},"to":{"type":"string","format":"date"}},"required":["period"]}""");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static IReadOnlyList<ChatTool> ForRole(RoleType role) => role is RoleType.Admin or RoleType.Manager ? [Search, Stock, Report, Financial] : [Search, Stock];
    public static bool Allowed(RoleType role, string name) => ForRole(role).Any(t => t.Name == name);
    private static AssistantToolResult Error(string message) => new(JsonSerializer.Serialize(new ErrorResult(message), Json), false);
    private static AssistantToolResult Invalid() => Error("Tham số không hợp lệ.");
    private static AssistantToolResult Success<T>(T data) => new(JsonSerializer.Serialize(data, Json), true);

    public async Task<AssistantToolResult> RunAsync(string name, string arguments, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Invalid();
            if (name == "search_products")
            {
                if (!Text(root, "query", out var query) || !Text(root, "brand", out var brand) || !Number(root, "min_price", out var min) || !Number(root, "max_price", out var max) || min < 0 || max < 0 || min > max || !Text(root, "category", out var categoryName) || !Limit(root, out var limit)) return Invalid();
                var (categoryId, categoryError) = await CategoryAsync(categoryName, ct);
                if (categoryError is not null) return categoryError;
                List<string>? specs = null;
                if (root.TryGetProperty("specs", out var spec))
                {
                    // Array of "key:value": Gemini's schema subset drops object maps (additionalProperties).
                    if (spec.ValueKind != JsonValueKind.Array) return Invalid();
                    specs = [];
                    foreach (var item in spec.EnumerateArray())
                    {
                        if (specs.Count == 5 || item.ValueKind != JsonValueKind.String || !Regex.IsMatch(item.GetString()!, "^[a-z][a-z0-9_]*:[^:]*\\S[^:]*$")) return Invalid();
                        specs.Add(item.GetString()!);
                    }
                }
                var result = await products.SearchAsync(new ProductSearchRequest { Q = query, CategoryId = categoryId, Brand = brand, MinPrice = min, MaxPrice = max, Spec = specs, IsActive = true, Page = 1, PageSize = limit }, ct);
                if (!result.IsSuccess || result.Data is null) return Invalid();
                var items = new List<ProductItem>();
                foreach (var summary in result.Data.Items.Take(Math.Min(limit, 10)))
                {
                    var detail = await products.GetByIdAsync(summary.Id, false, ct);
                    if (!detail.Found || detail.Data is not { IsActive: true } p) continue;
                    items.Add(new(p.Name, p.Sku, p.Brand, p.CategoryName, p.BasePrice, p.Specs.Take(8).ToDictionary(x => x.Key, x => x.Value), p.Variants.Where(v => v.IsActive).Take(5).Select(v => new VariantItem(v.VariantName, v.Sku, v.Price, v.EffectiveSpecs.Take(8).ToDictionary(x => x.Key, x => x.Value))).ToArray()));
                }
                return Success(new ItemsResult<ProductItem>(result.Data.TotalCount, items));
            }
            if (name == "get_stock")
            {
                if (!Text(root, "query", out var query) || !Bool(root, "only_available", out var available) || !Bool(root, "low_stock", out var low) || !Bool(root, "out_of_stock", out var outOfStock) || !Text(root, "category", out var categoryName) || !Limit(root, out var limit)) return Invalid();
                var (categoryId, categoryError) = await CategoryAsync(categoryName, ct);
                if (categoryError is not null) return categoryError;
                var result = await inventory.GetStocksAsync(new InventoryStockFilterRequest { Search = query, CategoryId = categoryId, InStock = available, LowStock = low, OutOfStock = outOfStock, Page = 1, PageSize = limit }, ct);
                if (!result.IsSuccess || result.Data is null) return Invalid();
                return Success(new ItemsResult<StockItem>(result.Data.TotalCount, result.Data.Items.Take(limit).Select(s => new StockItem(s.ProductName, s.VariantName, s.VariantSku ?? s.ProductSku, s.CategoryName, s.AvailableQuantity, s.ReservedQuantity, s.MinStockAlert, s.SellingPrice)).ToArray()));
            }
            if (name == "get_sales_report")
            {
                if (!Text(root, "period", out var period) || period is not ("today" or "last7Days" or "thisMonth" or "custom") || !Text(root, "from", out var from) || !Text(root, "to", out var to)) return Invalid();
                (DateOnly From, DateOnly To)? range = null;
                if (period == "custom")
                {
                    if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) || !DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end) || end.DayNumber - start.DayNumber is < 0 or >= 31 || end > DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime)) return Invalid();
                    range = (start, end);
                }
                else if (from is not null || to is not null) return Invalid();
                var result = await analytics.GetDashboardAsync(new(period, range), ct);
                if (result.Error is { } error) return Error(error.Message);
                if (result.Snapshot is not { } s) return Error("Không đọc được dữ liệu.");
                return Success(new ReportItem(s.Period, s.StartDate, s.EndDate, s.TimeZone, VietnamTime(s.AsOf), s.Summary.Revenue, s.Summary.OrderCount, s.Summary.RevenueChangePercent, s.Summary.OrderCountChangePercent, s.DailyRevenue.Select(d => new RevenueItem(d.Date, d.Revenue)).ToArray(), s.LowStockItems.Select(i => new LowStockItem(i.ProductName, i.VariantName, i.AvailableQuantity)).ToArray()));
            }
            if (name == "get_financial_report")
            {
                if (!Text(root, "period", out var period) || !Text(root, "from", out var from) || !Text(root, "to", out var to)) return Invalid();
                var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
                (DateOnly From, DateOnly To) range;
                if (period == "custom")
                {
                    if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) || !DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end)) return Invalid();
                    range = (start, end);
                }
                else if (from is not null || to is not null) return Invalid();
                else range = period switch
                {
                    "today" => (today, today),
                    "last7Days" => (today.AddDays(-6), today),
                    "thisMonth" => (new DateOnly(today.Year, today.Month, 1), today),
                    _ => (DateOnly.MinValue, DateOnly.MinValue)
                };
                if (range.From == DateOnly.MinValue) return Invalid();
                // The 31-day limit lives in AnalyticsController, not the service, so the tool checks it; the service still rejects future dates.
                var days = range.To.DayNumber - range.From.DayNumber + 1;
                if (days < 1) return Error("Ngày kết thúc phải từ ngày bắt đầu trở đi.");
                if (days > AnalyticsLimits.MaxCustomRangeDays) return Error($"Kỳ báo cáo tối đa {AnalyticsLimits.MaxCustomRangeDays} ngày; hãy chia thành nhiều kỳ nhỏ hơn.");
                var result = await analytics.GetFinancialReportAsync(new(range.From, range.To), ct);
                if (result.Error is { } error) return Error(error.Message);
                if (result.Report is not { } r) return Error("Không đọc được dữ liệu.");
                var t = r.Summary;
                static TopProductItem Top(TechStore.Shared.DTOs.FinancialReportProductDto p) => new(p.ProductName, p.Quantity, p.SalesExcludingTax);
                return Success(new FinancialReportItem(r.StartDate, r.EndDate, VietnamTime(r.AsOf),
                    new(t.CompletedOrderCount, t.Quantity, t.SalesExcludingTax, t.SalesTax, t.PaymentReceivedAmount, t.RefundPaidAmount, t.RefundedSalesExcludingTax, t.NetSalesExcludingTax, t.CostOfGoodsSold, t.RestockedCost, t.NetCostOfGoodsSold, t.SalesGrossProfit, t.NetGrossProfit, t.WarrantyCost),
                    r.TopProductsByQuantity.Take(5).Select(Top).ToArray(),
                    r.TopProductsByRevenue.Take(5).Select(Top).ToArray(),
                    r.CashierPerformance.Take(10).Select(c => new CashierItem(c.CashierName, c.CompletedOrderCount, c.Quantity, c.SalesExcludingTax, c.SalesGrossProfit)).ToArray()));
            }
            return Error("Tool không khả dụng với vai trò hiện tại.");
        }
        catch (JsonException) { return Invalid(); }
    }
    // shortcut: matches one category only (no sub-categories); fine while demo categories are flat.
    private async Task<(Guid? Id, AssistantToolResult? Error)> CategoryAsync(string? name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)) return (null, null);
        var tree = await categories.GetTreeAsync(ct);
        var all = Flatten(tree.Data).Where(c => c.IsActive).ToArray();
        static string Key(string value) => value.Trim().ToLowerInvariant().TrimEnd('s');
        var match = all.FirstOrDefault(c => Key(c.Name) == Key(name) || Key(c.Slug) == Key(name));
        return match is null
            ? (null, Error("Không có danh mục này. Danh mục hiện có: " + string.Join(", ", all.Select(c => c.Name)) + "."))
            : (match.Id, null);
    }

    private static IEnumerable<TechStore.Shared.DTOs.CategoryTreeNodeDto> Flatten(IEnumerable<TechStore.Shared.DTOs.CategoryTreeNodeDto> nodes) =>
        nodes.SelectMany(n => Flatten(n.SubCategories ?? []).Prepend(n));

    private static bool Text(JsonElement root, string name, out string? value)
    {
        value = null;
        if (!root.TryGetProperty(name, out var v)) return true;
        if (v.ValueKind != JsonValueKind.String) return false;
        value = v.GetString();
        return value is not null && value.Length <= 100;
    }
    private static bool Number(JsonElement root, string name, out decimal? value)
    {
        value = null;
        if (!root.TryGetProperty(name, out var v)) return true;
        if (v.ValueKind != JsonValueKind.Number || !v.TryGetDecimal(out var n)) return false;
        value = n; return true;
    }
    private static bool Bool(JsonElement root, string name, out bool? value)
    {
        value = null;
        if (!root.TryGetProperty(name, out var v)) return true;
        if (v.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false;
        value = v.GetBoolean(); return true;
    }
    private static bool Limit(JsonElement root, out int limit)
    {
        limit = 5;
        if (!root.TryGetProperty("limit", out var v)) return true;
        return v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out limit) && limit is >= 1 and <= 10;
    }
}
