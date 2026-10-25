using TechStore.Client.Features.Inventory.Models;
using TechStore.Client.Features.Inventory.Services;
using TechStore.Shared.DTOs;

namespace TechStore.Client.UnitTests.Features.Inventory;

public sealed class InventoryReportBuilderTests
{
    [Fact]
    public void BuildCsv_UsesExpectedColumnsAndEscapesValues()
    {
        var row = new InventoryStockRow(new InventoryStockDto
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            ProductName = "Laptop, Pro",
            ProductSku = "LP-01",
            CategoryName = "Laptop",
            Quantity = 8,
            ReservedQuantity = 2,
            SellingPrice = 12_500_000
        });

        var csv = InventoryReportBuilder.BuildCsv([row]);

        Assert.Contains("\"Sản phẩm\",\"SKU\",\"Danh mục\",\"Giá bán\",\"Có sẵn\",\"Đang giữ\",\"Trạng thái\"", csv);
        Assert.Contains("\"Laptop, Pro\"", csv);
        Assert.Contains("\"LP-01\"", csv);
        Assert.Contains("\"6 chiếc\"", csv);
        Assert.Contains("\"2\"", csv);
    }

    [Fact]
    public async Task CsvExporter_WritesFilteredRowsToConfiguredDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "techstore-report-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var row = new InventoryStockRow(new InventoryStockDto
            {
                Id = Guid.NewGuid(),
                ProductId = Guid.NewGuid(),
                ProductName = "Phone",
                ProductSku = "P-01",
                Quantity = 10,
                ReservedQuantity = 1
            });
            var result = await new CsvInventoryReportExporter(directory).ExportAsync([row]);

            Assert.Equal(1, result.RowCount);
            Assert.True(File.Exists(result.FilePath));
            Assert.Contains("Phone", await File.ReadAllTextAsync(result.FilePath));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
