using System.Security.Cryptography;
using System.Text;
using TechStore.Shared.Enums;

namespace TechStore.Api.Common.Data;

/// <summary>
/// Immutable reference fixture used to add the Sprint 2 demo rows.
/// Existing Sprint 1 rows are reconciled by their natural keys separately.
/// </summary>
internal static class DemoSeedManifest
{
    public static readonly DateTime AnchorUtc = new(2026, 10, 18, 5, 0, 0, DateTimeKind.Utc);

    public static IReadOnlyList<CategorySeed> Categories { get; } =
    [
        new("smartphones", "Smartphones", "Dien thoai thong minh"),
        new("laptops", "Laptops", "May tinh xach tay"),
        new("tablets", "Tablets", "Tablets and e-readers"),
        new("monitors", "Monitors", "Professional monitors"),
        new("accessories", "Accessories", "Computer and mobile accessories")
    ];

    public static IReadOnlyList<ProductSeed> Products { get; } = BuildProducts();
    public static IReadOnlyList<CustomerSeed> Customers { get; } = BuildCustomers();
    public static IReadOnlyList<SerialSeed> Serials { get; } = BuildSerials();
    public static IReadOnlyList<OrderSeed> Orders { get; } = BuildOrders();

    public static Guid Id(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"techstore-demo-seed-v2:{key}"));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }

    public static DateTime AtHours(int offset) => AnchorUtc.AddHours(offset);
    public static DateTime AtDays(int offset) => AnchorUtc.AddDays(offset);

    private static IReadOnlyList<ProductSeed> BuildProducts()
    {
        (string Sku, string Name, string Brand, string Category, decimal Price, decimal Cost, bool SerialTracked, string Specs, string? VariantSku)[] definitions =
        [
            ("IP15PM-256", "iPhone 15 Pro Max 256GB", "Apple", "smartphones", 29990000m, 26000000m, true, "{\"cpu\":\"Apple A17 Pro\",\"ram_gb\":8,\"storage_gb\":256}", "IP15PM-256-NT"),
            ("S24U-256", "Samsung Galaxy S24 Ultra 256GB", "Samsung", "smartphones", 27990000m, 24000000m, true, "{\"cpu\":\"Snapdragon 8 Gen 3 for Galaxy\",\"ram_gb\":12,\"storage_gb\":256}", "S24U-256-GR"),
            ("MBP14-M3-512", "MacBook Pro 14 inch M3 512GB", "Apple", "laptops", 39990000m, 35000000m, true, "{\"cpu\":\"Apple M3\",\"ram_gb\":16,\"storage_gb\":512}", "MBP14-M3-512-SG"),
            ("DELL-XPS15-9530", "Dell XPS 15 9530 Core i7", "Dell", "laptops", 45000000m, 40000000m, true, "{\"cpu\":\"Intel Core i7-13700H\",\"ram_gb\":32,\"storage_gb\":1024}", null),
            ("SONY-WH1000XM5", "Sony WH-1000XM5 Chong On", "Sony", "smartphones", 7990000m, 6500000m, false, "{\"bluetooth\":\"5.2\",\"battery_mah\":1200}", null),
            ("DEMO-TAB-IPAD-AIR", "iPad Air 11 M2 128GB", "Apple", "tablets", 16990000m, 14500000m, true, "{\"cpu\":\"Apple M2\",\"ram_gb\":8,\"storage_gb\":128}", null),
            ("DEMO-TAB-GALAXY-S9", "Galaxy Tab S9 256GB", "Samsung", "tablets", 19990000m, 17000000m, true, "{\"cpu\":\"Snapdragon 8 Gen 2\",\"ram_gb\":12,\"storage_gb\":256}", null),
            ("DEMO-TAB-LENOVO-P12", "Lenovo Tab P12 256GB", "Lenovo", "tablets", 8990000m, 7400000m, true, "{\"cpu\":\"Dimensity 7050\",\"ram_gb\":8,\"storage_gb\":256}", null),
            ("DEMO-MON-DELL-U2723", "Dell UltraSharp U2723QE", "Dell", "monitors", 15990000m, 13200000m, true, "{\"resolution\":\"4K\",\"size_in\":27}", null),
            ("DEMO-MON-LG-27GP", "LG UltraGear 27GP850", "LG", "monitors", 11990000m, 9800000m, true, "{\"resolution\":\"QHD\",\"size_in\":27}", null),
            ("DEMO-MON-ASUS-PROART", "ASUS ProArt PA278CV", "ASUS", "monitors", 10990000m, 9000000m, true, "{\"resolution\":\"QHD\",\"size_in\":27}", null),
            ("DEMO-SSD-SAMSUNG-T7", "Samsung Portable SSD T7 1TB", "Samsung", "accessories", 2690000m, 2100000m, true, "{\"storage_gb\":1000,\"interface\":\"USB 3.2\"}", null),
            ("DEMO-SSD-SANDISK-EXT", "SanDisk Extreme Portable 1TB", "SanDisk", "accessories", 2990000m, 2350000m, true, "{\"storage_gb\":1000,\"interface\":\"USB-C\"}", null),
            ("DEMO-DOCK-ANKER-777", "Anker PowerExpand 777 Dock", "Anker", "accessories", 5990000m, 4700000m, true, "{\"ports\":12,\"power_w\":100}", null),
            ("DEMO-KEY-LOGI-MX", "Logitech MX Keys S", "Logitech", "accessories", 2490000m, 1900000m, true, "{\"layout\":\"US\",\"wireless\":true}", null),
            ("DEMO-MOUSE-LOGI-MX3", "Logitech MX Master 3S", "Logitech", "accessories", 2690000m, 2050000m, false, "{\"dpi\":8000,\"wireless\":true}", null),
            ("DEMO-WEBCAM-LOGI-BRIO", "Logitech Brio 4K Webcam", "Logitech", "accessories", 4290000m, 3400000m, false, "{\"resolution\":\"4K\",\"microphone\":true}", null),
            ("DEMO-HEADSET-JABRA-EVOLVE", "Jabra Evolve2 65 Flex", "Jabra", "accessories", 6290000m, 5000000m, false, "{\"bluetooth\":\"5.2\",\"noise_cancelling\":true}", null),
            ("DEMO-CHARGER-ANKER-735", "Anker 735 Charger 65W", "Anker", "accessories", 1190000m, 850000m, false, "{\"power_w\":65,\"ports\":3}", null),
            ("DEMO-CABLE-USB-C", "USB-C 100W Braided Cable", "TechStore", "accessories", 290000m, 120000m, false, "{\"power_w\":100,\"length_m\":2}", null)
        ];

        return definitions.Select((item, index) => new ProductSeed(
            item.Sku,
            item.Name,
            item.Brand,
            item.Category,
            item.Price,
            item.Cost,
            item.SerialTracked,
            item.Specs,
            AtDays(-20 + index),
            item.VariantSku)).ToArray();
    }

    private static IReadOnlyList<CustomerSeed> BuildCustomers() => new[]
        {
            new CustomerSeed("Nguyen Van An", "0901234567", "nguyenvanan@gmail.com", new DateOnly(1995, 5, 20), AtDays(-30), CustomerTier.Standard, 150),
            new CustomerSeed("Tran Thi Bich", "0912345678", "tranthibich@gmail.com", new DateOnly(1998, 8, 15), AtDays(-60), CustomerTier.Silver, 600)
        }
        .Concat(Enumerable.Range(1, 8).Select(i => new CustomerSeed(
            $"Demo Customer {i:00}",
            $"09300000{i:00}",
            $"demo.customer{i:00}@techstore.test",
            new DateOnly(1985 + i, 1 + i, 10 + i),
            AtDays(-40 - i),
            (i % 4) switch
            {
                0 => CustomerTier.Gold,
                1 => CustomerTier.Standard,
                2 => CustomerTier.Silver,
                _ => CustomerTier.Platinum
            },
            i * 100)))
        .ToArray();

    private static IReadOnlyList<SerialSeed> BuildSerials() => Enumerable.Range(0, 10)
        .SelectMany(productIndex => Enumerable.Range(0, 5).Select(unitIndex =>
        {
            var sequence = productIndex * 5 + unitIndex + 1;
            return new SerialSeed(
                Products[productIndex + 5].Sku,
                $"490000000{sequence:000000}",
                AtDays(-19 + productIndex));
        }))
        .ToArray();

    private static IReadOnlyList<OrderSeed> BuildOrders()
    {
        var rows = new List<OrderSeed>();
        var completedDayOffsets = new[] { -1, -2, -3, -5, -6, 0, -1, -2, -3, -6, 0, -5 };
        for (var i = 1; i <= 12; i++)
        {
            var completedAt = AtDays(completedDayOffsets[i - 1]).AddMinutes(-1);
            var createdAt = completedAt.AddMinutes(-20);
            rows.Add(new OrderSeed(
                $"DEMO-202610-{i:0000}", OrderStatus.Completed,
                i % 2 == 0 ? PaymentMethod.VietQr : PaymentMethod.Cash,
                5 + (i - 1) % 10, 2 + (i - 1) % 8,
                createdAt, completedAt, completedAt,
                i <= 10 ? 0 : 1));
        }

        for (var i = 13; i <= 14; i++)
        {
            var createdAt = AtDays(-1).AddHours(-i);
            rows.Add(new OrderSeed(
                $"DEMO-202610-{i:0000}", OrderStatus.Processing, PaymentMethod.Cash,
                5 + i - 13, 2 + (i - 1) % 8,
                createdAt, createdAt.AddMinutes(5), null, 2));
        }

        for (var i = 15; i <= 17; i++)
        {
            rows.Add(new OrderSeed(
                $"DEMO-202610-{i:0000}", OrderStatus.Pending, PaymentMethod.Cash,
                15 + (i - 15), 2 + (i - 1) % 8,
                AtDays(-1).AddHours(-i), null, null, 0));
        }

        for (var i = 18; i <= 20; i++)
        {
            rows.Add(new OrderSeed(
                $"DEMO-202610-{i:0000}", OrderStatus.Cancelled, PaymentMethod.Cash,
                17 + (i - 18), 2 + (i - 1) % 8,
                AtDays(-2).AddHours(-i), null, null, 0));
        }

        return rows;
    }
}

internal sealed record CategorySeed(string Slug, string Name, string Description);

internal sealed record ProductSeed(
    string Sku,
    string Name,
    string Brand,
    string CategorySlug,
    decimal Price,
    decimal Cost,
    bool SerialTracked,
    string Specs,
    DateTime CreatedAt,
    string? VariantSku);

internal sealed record CustomerSeed(
    string Name,
    string Phone,
    string Email,
    DateOnly DateOfBirth,
    DateTime CreatedAt,
    CustomerTier Tier,
    int LoyaltyPoints);

internal sealed record SerialSeed(string ProductSku, string Imei, DateTime CreatedAt);

internal sealed record OrderSeed(
    string OrderCode,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    int ProductIndex,
    int CustomerIndex,
    DateTime CreatedAt,
    DateTime? PaidAt,
    DateTime? CompletedAt,
    int SerialIndex);
