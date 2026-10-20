using System.Text.Json;
using TechStore.Client.Models.Orders;
using TechStore.Client.Services.Api;
using static TechStore.Client.Repositories.Orders.OrderJson;

namespace TechStore.Client.Repositories.Orders;

/// <summary>
/// Reads the devices that can still be sold. It maps only id, IMEI and serial number: the API row also
/// carries unit cost, which must never reach the sales screen, so it is not even deserialized.
/// </summary>
public sealed class OrderDeviceRepository(IApiClient apiClient) : IOrderDeviceRepository
{
    public const int PageSize = 100;

    public async Task<AvailableDevicePage> GetInStockAsync(
        Guid productId,
        Guid? variantId,
        CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty) throw new ArgumentException("A product id is required.", nameof(productId));
        if (variantId == Guid.Empty) throw new ArgumentException("A variant id cannot be empty.", nameof(variantId));

        var path = $"api/inventory/serials?status=InStock&productId={productId:D}";
        if (variantId.HasValue) path += $"&variantId={variantId.Value:D}";
        path += $"&page=1&pageSize={PageSize}";

        var json = await apiClient.GetAsync<JsonElement>(path, cancellationToken);
        RequireObject(json, "device page");
        Require(json, "items", JsonValueKind.Array);
        RequireInt(json, "totalCount");

        var items = new List<AvailableDevice>();
        foreach (var item in json.GetProperty("items").EnumerateArray())
        {
            RequireObject(item, "device");
            RequireGuid(item, "id");
            RequireNullableString(item, "imei");
            RequireNullableString(item, "serialNumber");
            items.Add(new AvailableDevice(
                Guid.Parse(item.GetProperty("id").GetString()!),
                ReadNullableString(item, "imei"),
                ReadNullableString(item, "serialNumber")));
        }

        var totalCount = json.GetProperty("totalCount").GetInt32();
        if (totalCount < items.Count)
            throw InvalidResponse("The device page totalCount is smaller than the items returned.");
        return new AvailableDevicePage(items, totalCount);
    }

    private static string? ReadNullableString(JsonElement item, string property)
    {
        var value = item.GetProperty(property);
        return value.ValueKind == JsonValueKind.Null ? null : value.GetString();
    }
}
