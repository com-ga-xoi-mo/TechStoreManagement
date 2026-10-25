using TechStore.Client.Models.Inventory;
using TechStore.Client.Services.Files;
using TechStore.Client.Services.Inventory;
using TechStore.Shared.DTOs;

namespace TechStore.Client.UnitTests.ViewModels.Inventory;

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
    public void SuggestedFileName_UsesVietnameseNameAndTimestamp()
    {
        // Arrange
        var localNow = new DateTime(2026, 10, 26, 9, 5, 7);

        // Act
        var fileName = InventoryReportBuilder.SuggestedFileName(localNow);

        // Assert
        Assert.Equal("bao-cao-ton-kho-20261026-090507.csv", fileName);
    }

    [Fact]
    public async Task CsvFileWriter_WritesUtf8BomWithoutOverwriting()
    {
        // Arrange
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
            var content = InventoryReportBuilder.BuildCsv([row]);

            // Act
            var firstPath = await CsvFileWriter.WriteToFolderAsync(directory, "bao-cao.csv", content);
            var secondPath = await CsvFileWriter.WriteToFolderAsync(directory, "bao-cao.csv", content);

            // Assert
            Assert.Equal(Path.Combine(directory, "bao-cao.csv"), firstPath);
            Assert.Equal(Path.Combine(directory, "bao-cao-2.csv"), secondPath);
            var bytes = await File.ReadAllBytesAsync(secondPath);
            Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
            Assert.Contains("Phone", await File.ReadAllTextAsync(secondPath));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
