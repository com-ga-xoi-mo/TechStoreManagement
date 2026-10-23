using TechStore.Client.Models.Customers;

namespace TechStore.Client.Repositories.Customers;

internal static class CustomerQueryUri
{
    public static string Build(CustomerQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var values = new List<string>();
        if (!string.IsNullOrWhiteSpace(query.Query)) values.Add($"q={Uri.EscapeDataString(query.Query.Trim())}");
        if (!string.IsNullOrWhiteSpace(query.Tier)) values.Add($"tier={Uri.EscapeDataString(query.Tier.Trim())}");
        if (query.IsActive.HasValue) values.Add($"isActive={query.IsActive.Value.ToString().ToLowerInvariant()}");
        values.Add($"page={Math.Max(1, query.Page)}");
        values.Add($"pageSize={Math.Clamp(query.PageSize, 1, 100)}");
        return $"api/customers?{string.Join('&', values)}";
    }
}
