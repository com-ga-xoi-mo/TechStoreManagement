using TechStore.Api.Catalog.Entities;

namespace TechStore.Api.Catalog.Services;

internal static class ProductCoverImage
{
    public static string? UrlOf(IEnumerable<ProductImage> images)
    {
        var ordered = images.OrderBy(i => i.SortOrder).ToList();
        return (ordered.FirstOrDefault(i => i.VariantId == null) ?? ordered.FirstOrDefault())?.Url;
    }

    public static void Renumber(IList<ProductImage> imagesInOrder)
    {
        for (var i = 0; i < imagesInOrder.Count; i++) imagesInOrder[i].SortOrder = i;
    }
}
