using System.Globalization;
using System.Text.RegularExpressions;
using TechStore.Client.Models.Catalog;

namespace TechStore.Client.Repositories.Catalog;

internal static class ProductQueryUri
{
    private static readonly Regex SpecKeyPattern = new("^[a-z][a-z0-9_]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Build(ProductQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        Validate(query);
        var parameters = new List<string>();
        Add("q", query.Query?.Trim());
        Add("brand", query.Brand?.Trim());
        Add("categoryId", query.CategoryId?.ToString("D"));
        Add("isActive", query.IsActive?.ToString().ToLowerInvariant());
        Add("minPrice", query.MinPrice?.ToString(CultureInfo.InvariantCulture));
        Add("maxPrice", query.MaxPrice?.ToString(CultureInfo.InvariantCulture));
        foreach (var spec in query.Specs) Add("spec", spec);
        Add("sort", query.Sort);
        Add("page", query.Page.ToString(CultureInfo.InvariantCulture));
        Add("pageSize", query.PageSize.ToString(CultureInfo.InvariantCulture));
        return "api/products?" + string.Join('&', parameters);

        void Add(string name, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            parameters.Add($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}");
        }
    }

    private static void Validate(ProductQuery query)
    {
        if (query.Page < 1) throw new ArgumentOutOfRangeException(nameof(query), "Page must be at least 1.");
        if (query.PageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(query), "Page size must be between 1 and 100.");
        if (query.MinPrice is < 0 || query.MaxPrice is < 0
            || (query.MinPrice.HasValue && query.MaxPrice.HasValue && query.MinPrice > query.MaxPrice))
            throw new ArgumentException("The product price range is invalid.", nameof(query));

        var sort = query.Sort.Split(':', 2, StringSplitOptions.TrimEntries);
        if (sort.Length is < 1 or > 2
            || sort[0] is not ("name" or "price" or "createdAt")
            || (sort.Length == 2 && sort[1] is not ("asc" or "desc")))
            throw new ArgumentException("The product sort value is invalid.", nameof(query));

        var valuesByKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var spec in query.Specs)
        {
            var separator = spec?.IndexOf(':') ?? -1;
            if (separator <= 0 || separator == spec!.Length - 1)
                throw new ArgumentException("A product spec must use key:value format.", nameof(query));

            var key = spec[..separator];
            var value = spec[(separator + 1)..];
            if (!SpecKeyPattern.IsMatch(key) || string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A product spec key or value is invalid.", nameof(query));
            if (valuesByKey.TryGetValue(key, out var previous) && !StringComparer.Ordinal.Equals(previous, value))
                throw new ArgumentException("A product spec key cannot have conflicting values.", nameof(query));
            valuesByKey[key] = value;
        }
    }
}
