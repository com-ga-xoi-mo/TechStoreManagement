using System;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Models.Inventory;

namespace TechStore.Client.Converters;

public sealed class InventoryStockStatusLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language) => value switch
    {
        InventoryStockStatus.All => "Tất cả trạng thái",
        InventoryStockStatus.Available => "Còn hàng",
        InventoryStockStatus.LowStock => "Sắp hết",
        InventoryStockStatus.OutOfStock => "Hết hàng",
        _ => "Tất cả trạng thái"
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, string language) =>
        throw new NotSupportedException("Stock status is selected as an enum value.");
}
