#if DEBUG
using TechStore.Client.Features.Categories.Models;
using TechStore.Client.Services.Api;

namespace TechStore.Client.Features.Categories.Preview;

/// <summary>Explicit --category-preview launch only; never used as a live API fallback.</summary>
internal sealed class CategoryPreviewApiClient : ICategoryApiClient
{
    internal bool FailLoading { get; set; }
    internal IReadOnlyList<CategoryNode>? OverrideRoots { get; set; }
    internal Task<IReadOnlyList<CategoryNode>>? PendingLoad { get; set; }
    internal CategoryApiException? DeleteFailure { get; set; }
    internal List<Guid> DeletedIds { get; } = [];

    public Task<IReadOnlyList<CategoryNode>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (PendingLoad is not null) return PendingLoad;
        if (FailLoading) throw new CategoryApiException(CategoryApiFailure.Unavailable);
        if (OverrideRoots is not null) return Task.FromResult(OverrideRoots);
        return Task.FromResult<IReadOnlyList<CategoryNode>>([
            Parent("Điện thoại thông minh", "smartphones", 642,
                Child("iPhone / Apple iOS", 248), Child("Samsung Galaxy", 185), Child("Xiaomi / Redmi", 112), Child("OPPO / OnePlus", 97)),
            Parent("Laptop & Máy tính xách tay", "laptops", 480,
                Child("MacBook / macOS", 140), Child("Laptop Gaming", 165), Child("Laptop Văn phòng & Mỏng nhẹ", 175)),
            Parent("Máy tính bảng & Tablet", "tablets", 160,
                Child("iPad (Apple iOS)", 95), Child("Android Tablet (Samsung Tab...)", 65)),
            Parent("Phụ kiện & Âm thanh", "accessories", 138,
                Child("Củ sạc & Cáp kết nối", 82), Child("Tai nghe & Loa Bluetooth", 56, false))
        ]);
    }

    public Task<TechStore.Shared.DTOs.CategoryDto> CreateAsync(TechStore.Shared.Requests.CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (FailLoading) throw new CategoryApiException(CategoryApiFailure.Unavailable);
        return Task.FromResult(new TechStore.Shared.DTOs.CategoryDto
        {
            Id = Guid.NewGuid(),
            Name = request.Name ?? "",
            Slug = request.Slug ?? "",
            Description = request.Description,
            ParentId = request.ParentId,
            IsActive = request.IsActive ?? true
        });
    }

    public Task<TechStore.Shared.DTOs.CategoryDto> UpdateAsync(Guid id, TechStore.Shared.Requests.UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (FailLoading) throw new CategoryApiException(CategoryApiFailure.Unavailable);
        return Task.FromResult(new TechStore.Shared.DTOs.CategoryDto
        {
            Id = id,
            Name = request.Name ?? string.Empty,
            Slug = request.Slug ?? string.Empty,
            Description = request.Description,
            ParentId = request.ParentId,
            IsActive = request.IsActive ?? true
        });
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (DeleteFailure is not null) throw DeleteFailure;
        if (FailLoading) throw new CategoryApiException(CategoryApiFailure.Unavailable);
        DeletedIds.Add(id);
        return Task.CompletedTask;
    }

    private static CategoryNode Parent(string name, string slug, int count, params CategoryNode[] children) =>
        new(Guid.NewGuid(), name, slug, true, children, ProductCount: count);

    private static CategoryNode Child(string name, int count, bool active = true) =>
        new(Guid.NewGuid(), name, "", active, [], SkuCount: count);
}
#endif
