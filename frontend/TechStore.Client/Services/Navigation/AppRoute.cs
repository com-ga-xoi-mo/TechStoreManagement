using TechStore.Client.Models.Catalog;

namespace TechStore.Client.Services.Navigation;

public enum AppRouteKind
{
    Root,
    ProductList,
    ProductDetail
}

/// <summary>Value-only route state. It deliberately carries no page, ViewModel, service, or scope.</summary>
public sealed class AppRoute : IEquatable<AppRoute>
{
    private AppRoute(
        ShellDestination destination,
        AppRouteKind kind,
        Guid? productId = null,
        ProductQuery? returnQuery = null,
        Guid? focusProductId = null)
    {
        if (!Enum.IsDefined(destination)) throw new ArgumentOutOfRangeException(nameof(destination));
        if (kind != AppRouteKind.Root && destination != ShellDestination.Products)
            throw new ArgumentException("Catalog routes must stay under the Products Shell destination.", nameof(destination));
        if (kind == AppRouteKind.ProductDetail && (!productId.HasValue || productId.Value == Guid.Empty))
            throw new ArgumentException("A product detail route requires a non-empty product id.", nameof(productId));
        if (kind != AppRouteKind.ProductDetail && productId is not null)
            throw new ArgumentException("Only product detail routes can carry a product id.", nameof(productId));
        if (kind == AppRouteKind.Root && (returnQuery is not null || focusProductId is not null))
            throw new ArgumentException("Root routes cannot carry Catalog return context.");
        if (returnQuery is not null && returnQuery.Specs is null)
            throw new ArgumentException("The Catalog return query must contain a spec collection.", nameof(returnQuery));

        Destination = destination;
        Kind = kind;
        ProductId = productId;
        ReturnQuery = returnQuery is null
            ? null
            : returnQuery with { Specs = Array.AsReadOnly(returnQuery.Specs.ToArray()) };
        FocusProductId = focusProductId;
    }

    public ShellDestination Destination { get; }
    public AppRouteKind Kind { get; }
    public Guid? ProductId { get; }
    public ProductQuery? ReturnQuery { get; }
    public Guid? FocusProductId { get; }

    public static AppRoute Root(ShellDestination destination) => new(destination, AppRouteKind.Root);
    public static AppRoute Products(ProductQuery? returnQuery = null, Guid? focusProductId = null) =>
        new(ShellDestination.Products, AppRouteKind.ProductList, returnQuery: returnQuery, focusProductId: focusProductId);
    public static AppRoute ProductDetail(Guid productId, ProductQuery? returnQuery = null, Guid? focusProductId = null) =>
        new(ShellDestination.Products, AppRouteKind.ProductDetail, productId, returnQuery, focusProductId);

    public bool Equals(AppRoute? other) => other is not null
        && Destination == other.Destination
        && Kind == other.Kind
        && ProductId == other.ProductId
        && FocusProductId == other.FocusProductId
        && ProductQueryValueComparer.Equals(ReturnQuery, other.ReturnQuery);

    public override bool Equals(object? obj) => obj is AppRoute other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Destination);
        hash.Add(Kind);
        hash.Add(ProductId);
        hash.Add(FocusProductId);
        ProductQueryValueComparer.AddToHashCode(ref hash, ReturnQuery);
        return hash.ToHashCode();
    }
}

internal static class ProductQueryValueComparer
{
    public static bool Equals(ProductQuery? left, ProductQuery? right) => ReferenceEquals(left, right)
        || left is not null && right is not null
        && string.Equals(left.Query, right.Query, StringComparison.Ordinal)
        && string.Equals(left.Brand, right.Brand, StringComparison.Ordinal)
        && left.CategoryId == right.CategoryId
        && left.IsActive == right.IsActive
        && left.MinPrice == right.MinPrice
        && left.MaxPrice == right.MaxPrice
        && string.Equals(left.Sort, right.Sort, StringComparison.Ordinal)
        && left.Page == right.Page
        && left.PageSize == right.PageSize
        && left.Specs.SequenceEqual(right.Specs, StringComparer.Ordinal);

    public static void AddToHashCode(ref HashCode hash, ProductQuery? query)
    {
        if (query is null) return;
        hash.Add(query.Query, StringComparer.Ordinal);
        hash.Add(query.Brand, StringComparer.Ordinal);
        hash.Add(query.CategoryId);
        hash.Add(query.IsActive);
        hash.Add(query.MinPrice);
        hash.Add(query.MaxPrice);
        hash.Add(query.Sort, StringComparer.Ordinal);
        hash.Add(query.Page);
        hash.Add(query.PageSize);
        foreach (var spec in query.Specs) hash.Add(spec, StringComparer.Ordinal);
    }
}
