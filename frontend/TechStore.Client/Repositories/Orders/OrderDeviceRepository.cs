using System.Text.Json;
using TechStore.Client.Models.Orders;
using TechStore.Client.Models.Sales;
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

    public async Task<ScannedDevice?> FindInStockByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("A code is required.", nameof(code));

        var wanted = code.Trim();
        var json = await apiClient.GetAsync<JsonElement>(
            $"api/inventory/serials?status=InStock&search={Uri.EscapeDataString(wanted)}&page=1&pageSize=10", cancellationToken);
        RequireObject(json, "device page");
        Require(json, "items", JsonValueKind.Array);

        foreach (var item in json.GetProperty("items").EnumerateArray())
        {
            RequireObject(item, "device");
            RequireGuid(item, "id");
            RequireGuid(item, "productId");
            RequireNullableGuid(item, "variantId");
            RequireNullableString(item, "imei");
            RequireNullableString(item, "serialNumber");

            var imei = ReadNullableString(item, "imei");
            var serial = ReadNullableString(item, "serialNumber");
            if (!string.Equals(imei, wanted, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(serial, wanted, StringComparison.OrdinalIgnoreCase))
                continue;

            var variant = item.GetProperty("variantId");
            return new ScannedDevice(
                new AvailableDevice(Guid.Parse(item.GetProperty("id").GetString()!), imei, serial),
                Guid.Parse(item.GetProperty("productId").GetString()!),
                variant.ValueKind == JsonValueKind.Null ? null : Guid.Parse(variant.GetString()!));
        }

        return null;
    }

    private static string? ReadNullableString(JsonElement item, string property)
    {
        var value = item.GetProperty(property);
        return value.ValueKind == JsonValueKind.Null ? null : value.GetString();
    }
}
