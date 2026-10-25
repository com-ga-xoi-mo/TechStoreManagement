using System.Text;
using TechStore.Client.Features.Inventory.Models;

namespace TechStore.Client.Features.Inventory.Services;

public interface IInventoryReportExporter
{
    Task<InventoryReportExportResult> ExportAsync(
        IReadOnlyList<InventoryStockRow> rows,
        CancellationToken cancellationToken = default);
}

public sealed record InventoryReportExportResult(string FilePath, int RowCount);

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

public sealed class CsvInventoryReportExporter : IInventoryReportExporter
{
    private readonly string _directory;

    public CsvInventoryReportExporter(string? directory = null)
    {
        _directory = directory
            ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    public async Task<InventoryReportExportResult> ExportAsync(
        IReadOnlyList<InventoryStockRow> rows,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_directory);
        var fileName = $"inventory-report-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
        var path = System.IO.Path.Combine(_directory, fileName);
        var content = InventoryReportBuilder.BuildCsv(rows);
        await File.WriteAllTextAsync(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken);
        return new InventoryReportExportResult(path, rows.Count);
    }
}
