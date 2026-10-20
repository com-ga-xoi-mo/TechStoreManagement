using System.Globalization;
using TechStore.Client.Models.Orders;

namespace TechStore.Client.Repositories.Orders;

internal static class OrderQueryUri
{
    public static string Build(OrderQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        Validate(query);
        var parameters = new List<string>();
        Add("q", query.Query?.Trim());
        Add("status", query.Status?.ToString());
        Add("sort", query.Sort);
        Add("page", query.Page.ToString(CultureInfo.InvariantCulture));
        Add("pageSize", query.PageSize.ToString(CultureInfo.InvariantCulture));
        return "api/orders?" + string.Join('&', parameters);

        void Add(string name, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            parameters.Add($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}");
        }
    }

    private static void Validate(OrderQuery query)
    {
        if (query.Page < 1) throw new ArgumentOutOfRangeException(nameof(query), "Page must be at least 1.");
        if (query.PageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(query), "Page size must be between 1 and 100.");
        if (query.Status is { } status && !Enum.IsDefined(status))
            throw new ArgumentException("The order status filter is invalid.", nameof(query));

        var sort = (query.Sort ?? string.Empty).Split(':', 2, StringSplitOptions.TrimEntries);
        if (sort[0] is not ("createdAt" or "totalAmount")
            || (sort.Length == 2 && sort[1] is not ("asc" or "desc")))
            throw new ArgumentException("The order sort value is invalid.", nameof(query));
    }
}
