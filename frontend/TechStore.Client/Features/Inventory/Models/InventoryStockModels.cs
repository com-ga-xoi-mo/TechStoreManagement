using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using TechStore.Client.Controls;
using TechStore.Client.Features.Categories.Models;
using TechStore.Shared.DTOs;

namespace TechStore.Client.Features.Inventory.Models;

public enum InventoryStockStatusFilter
{
    All,
    InStock,
    LowStock,
    OutOfStock
}

public enum InventoryStockAction
{
    Import,
    ViewSerials,
    Adjust,
    Delete
}

public sealed record InventoryStockStatusOption(InventoryStockStatusFilter Value, string Label);

public enum InventoryStockStatus
{
    InStock,
    LowStock,
    OutOfStock
}

public sealed record InventoryStockPresentation(
    InventoryStockStatus Status,
    string StatusText,
    StatusTone Tone,
    bool IsDimmed);

public sealed record InventoryStockDeleteRequest(
    Guid StockId,
    Guid ProductId,
    string ProductName,
    string Sku,
    int? TotalSerialsCount);

public static class InventoryStockPresentationRules
{
    public static InventoryStockPresentation StatusFor(int availableQuantity, int minStockAlert = 5)
    {
        var threshold = Math.Max(0, minStockAlert);
        if (availableQuantity > threshold)
            return new(InventoryStockStatus.InStock, "Còn hàng", StatusTone.Success, false);
        if (availableQuantity > 0)
            return new(InventoryStockStatus.LowStock, "Sắp hết hàng", StatusTone.Warning, false);
        return new(InventoryStockStatus.OutOfStock, "Hết hàng", StatusTone.Danger, true);
    }

    public static string QuantityLabel(int quantity) => $"{Math.Max(0, quantity).ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} chiếc";

    public static string CurrencyLabel(decimal? value) => value is decimal amount
        ? $"{amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} ₫"
        : "—";

    public static string Unavailable(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;
}

public sealed partial class InventoryStockRow(InventoryStockDto dto) : ObservableObject
{
    public InventoryStockDto Dto { get; } = dto ?? throw new ArgumentNullException(nameof(dto));
    public Guid Id => Dto.Id;
    public Guid ProductId => Dto.ProductId;
    public Guid? CategoryId => Dto.CategoryId;
    public string ProductName => InventoryStockPresentationRules.Unavailable(Dto.ProductName);
    public string Sku => InventoryStockPresentationRules.Unavailable(Dto.VariantSku ?? Dto.ProductSku);
    public string VariantName => Dto.VariantName ?? string.Empty;
    public string CategoryName => InventoryStockPresentationRules.Unavailable(Dto.CategoryName);
    public string PriceText => InventoryStockPresentationRules.CurrencyLabel(Dto.SellingPrice);
    public int Quantity => Dto.Quantity;
    public int ReservedQuantity => Math.Max(0, Dto.ReservedQuantity);
    public int AvailableQuantity => Math.Max(0, Dto.AvailableQuantity);
    public int? TotalSerialsCount => Dto.TotalSerialsCount;
    public string TotalSerialsCountText => TotalSerialsCount is int count
        ? count.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))
        : "—";
    public string AvailableText => InventoryStockPresentationRules.QuantityLabel(AvailableQuantity);
    public string ReservedText => ReservedQuantity.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    public InventoryStockPresentation StockStatus => InventoryStockPresentationRules.StatusFor(
        AvailableQuantity,
        Dto.MinStockAlert > 0 ? Dto.MinStockAlert : 5);
    public InventoryStockStatus Status => StockStatus.Status;
    public string StatusText => StockStatus.StatusText;
    public StatusTone StatusTone => StockStatus.Tone;
    public bool IsDimmed => StockStatus.IsDimmed;
    public CategoryIconDescriptor Icon => CategoryIconResolver.Resolve(ProductName, Sku);
    public string SearchText => $"{ProductName} {Sku} {VariantName}";
    public InventoryStockDeleteRequest ToDeleteRequest() => new(
        Id, ProductId, ProductName, Sku, TotalSerialsCount);
}
