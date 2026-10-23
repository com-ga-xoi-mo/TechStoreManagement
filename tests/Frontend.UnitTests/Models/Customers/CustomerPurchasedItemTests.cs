using TechStore.Client.Models.Customers;

namespace TechStore.Client.UnitTests.Models.Customers;

public sealed class CustomerPurchasedItemTests
{
    [Fact]
    public void NonSerialTrackedPurchase_ExplainsThatPerUnitIdentifiersAndWarrantyAreNotTracked()
    {
        var item = CreateItem(serialImeiId: null, serialNumber: null, imei: null, warrantyEndAt: null, deviceStatus: null);

        Assert.Equal("Không quản lý serial/IMEI", item.ImeiLabel);
        Assert.Equal(string.Empty, item.SerialLabel);
        Assert.Equal("Không quản lý bảo hành theo máy", item.WarrantyLabel);
        Assert.Equal("Không quản lý", item.DeviceStatusLabel);
        Assert.Equal("Danger", item.WarrantyTone);
        Assert.Equal("Danger", item.DeviceStatusTone);
    }

    [Fact]
    public void TrackedPurchaseWithoutWarrantyData_DistinguishesMissingDataFromNotTracked()
    {
        var item = CreateItem(Guid.NewGuid(), "SERIAL-1", "123456789012345", null, null);

        Assert.Equal("IMEI: 123456789012345", item.ImeiLabel);
        Assert.Equal("Serial: SERIAL-1", item.SerialLabel);
        Assert.Equal("Chưa có dữ liệu", item.WarrantyLabel);
        Assert.Equal("Chưa có dữ liệu", item.DeviceStatusLabel);
    }

    [Fact]
    public void TrackedSoldPurchase_ShowsWarrantyAndCurrentDeviceState()
    {
        var item = CreateItem(Guid.NewGuid(), "SERIAL-1", "123456789012345", DateTime.UtcNow.AddDays(90), "Sold");

        Assert.Contains("Còn hạn đến", item.WarrantyLabel);
        Assert.Equal("Đang sử dụng", item.DeviceStatusLabel);
        Assert.Equal("Success", item.WarrantyTone);
        Assert.Equal("Success", item.DeviceStatusTone);
    }

    [Fact]
    public void TrackedPurchaseWithExpiredWarranty_UsesDangerTone()
    {
        var item = CreateItem(Guid.NewGuid(), "SERIAL-2", "123456789012345", DateTime.UtcNow.AddDays(-1), "Sold");

        Assert.Contains("Hết hạn", item.WarrantyLabel);
        Assert.Equal("Danger", item.WarrantyTone);
    }

    [Fact]
    public void ProductCategory_SelectsPhoneLaptopOrAccessoryIcon()
    {
        var baseItem = CreateItem(null, null, null, null, null);

        Assert.Equal("\uE8EA", (baseItem with { CategoryName = "Smartphones" }).ProductIconGlyph);
        Assert.Equal("\uE7F8", (baseItem with { CategoryName = "Laptops" }).ProductIconGlyph);
        Assert.Equal("\uE7F6", (baseItem with { CategoryName = "Accessories" }).ProductIconGlyph);
    }

    [Fact]
    public void CancelledOrder_UsesDangerTone()
    {
        var order = new CustomerOrder(Guid.NewGuid(), "ORD-1", "Cancelled", "Cash", 100m, 1,
            DateTime.UtcNow, null, null);

        Assert.Equal("Danger", order.StatusTone);
    }

    private static CustomerPurchasedItem CreateItem(
        Guid? serialImeiId,
        string? serialNumber,
        string? imei,
        DateTime? warrantyEndAt,
        string? deviceStatus) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "ORD-1", DateTime.UtcNow, Guid.NewGuid(), null,
            "Test device", null, "TEST-SKU", serialImeiId, serialNumber, imei, 1, 100m,
            serialImeiId is null ? null : DateTime.UtcNow.AddDays(-275), warrantyEndAt, deviceStatus);
}
