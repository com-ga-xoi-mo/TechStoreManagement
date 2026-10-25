using System.Text;
using TechStore.Client.Models.Inventory;

namespace TechStore.Client.Services.Inventory;

public static class InventoryReportBuilder
{
    private static readonly string[] Headers =
    [
        "Sản phẩm",
        "SKU",
        "Danh mục",
        "Giá bán",
        "Có sẵn",
        "Đang giữ",
        "Trạng thái"
    ];

    public static string SuggestedFileName(DateTime localNow) =>
        $"bao-cao-ton-kho-{localNow:yyyyMMdd-HHmmss}.csv";

    public static string BuildCsv(IEnumerable<InventoryStockRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', Headers.Select(Escape)));
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',',
                Escape(row.ProductName),
                Escape(row.Sku),
                Escape(row.CategoryName),
                Escape(row.PriceText),
                Escape(row.AvailableText),
                Escape(row.ReservedText),
                Escape(row.StatusText)));
        }

        return builder.ToString();
    }

    private static string Escape(string? value)
    {
        var text = value ?? string.Empty;
        return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
