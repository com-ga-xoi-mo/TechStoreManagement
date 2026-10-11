using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TechStore.Client.Models.Catalog;

namespace TechStore.Client.Repositories.Catalog;

public sealed class FakeCategoryRepository : ICategoryRepository
{
    private readonly CategoryTreePreviewScenario _treeScenario;

    public FakeCategoryRepository(CategoryTreePreviewScenario treeScenario = CategoryTreePreviewScenario.Populated)
    {
        _treeScenario = treeScenario;
    }

    private static readonly IReadOnlyList<CategoryOption> PreviewCategories =
    [
        new("phones", "Điện thoại"),
        new("laptops", "Laptop"),
        new("tablets", "Tablet"),
        new("accessories", "Phụ kiện")
    ];

    private static readonly CategoryTreeSnapshot PreviewTree = new(
    [
        new("phones", "Điện thoại thông minh", null, "Điện thoại và thiết bị di động", 642, true,
        [
            new("ios", "iPhone / Apple iOS", "phones", "Điện thoại Apple", 248, true, []),
            new("samsung", "Samsung Galaxy", "phones", null, 185, true, []),
            new("xiaomi", "Xiaomi / Redmi", "phones", null, 112, true, []),
            new("oppo", "OPPO / OnePlus", "phones", null, 97, true, []),
            new("pixel", "Google Pixel", "phones", null, 0, true, []),
            new("gaming-phones", "Điện thoại gaming", "phones", null, 0, true, []),
            new("other-phones", "Điện thoại khác", "phones", null, 0, true, [])
        ]),
        new("laptops", "Laptop & Máy tính xách tay", null, "Máy tính xách tay", 480, true,
        [
            new("macos", "MacBook / macOS", "laptops", null, 140, true, []),
            new("gaming", "Laptop Gaming", "laptops", null, 165, true, []),
            new("office", "Laptop Văn phòng & Mỏng nhẹ", "laptops", null, 175, true, []),
            new("workstation", "Máy trạm", "laptops", null, 0, true, []),
            new("chromebook", "Chromebook", "laptops", null, 0, true, [])
        ]),
        new("tablets", "Máy tính bảng & Tablet", null, null, 160, true,
        [
            new("ipad", "iPad (Apple iOS)", "tablets", null, 95, true, []),
            new("android-tablet", "Android Tablet", "tablets", null, 65, true, []),
            new("drawing-tablet", "Bảng vẽ điện tử", "tablets", null, 0, true, []),
            new("e-reader", "Máy đọc sách", "tablets", null, 0, true, [])
        ]),
        new("accessories", "Phụ kiện & Âm thanh", null, null, 138, true,
        [
            new("cables", "Củ sạc & Cáp kết nối", "accessories", null, 82, true, []),
            new("audio", "Tai nghe & Loa Bluetooth", "accessories", null, 56, false, []),
            new("cases", "Ốp lưng & Bảo vệ", "accessories", null, 0, true, []),
            new("storage", "Lưu trữ & Kết nối", "accessories", null, 0, true, [])
        ])
    ],
    IsPreviewData: true);

    public Task<IReadOnlyList<CategoryOption>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(PreviewCategories);
    }

    public async Task<CategoryTreeSnapshot> GetCategoryTreeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (_treeScenario)
        {
            case CategoryTreePreviewScenario.Empty:
                return new CategoryTreeSnapshot([], IsPreviewData: true);
            case CategoryTreePreviewScenario.Delayed:
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
                break;
            case CategoryTreePreviewScenario.Error:
                throw new InvalidOperationException("Category-tree preview load failure.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return PreviewTree;
    }
}
