using System;
using System.Globalization;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Models.Inventory;

namespace TechStore.Client.Converters;

public sealed class StockMovementTypeLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language) => value switch
    {
        StockMovementType.Receipt => "Nhập kho",
        StockMovementType.Sale => "Bán hàng",
        StockMovementType.Return => "Trả hàng",
        StockMovementType.Adjustment => "Điều chỉnh",
        StockMovementType.Transfer => "Điều chuyển",
        _ => "Khác"
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, string language) =>
        throw new NotSupportedException("Movement type is display-only.");
}

public sealed class StockMovementQuantityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language) => value is int quantity
        ? $"{quantity.ToString("+#,0;-#,0;0", CultureInfo.CurrentCulture)} sản phẩm"
        : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, string language) =>
        throw new NotSupportedException("Movement quantity is display-only.");
}

public sealed class StockMovementTimestampConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language) => value switch
    {
        DateTimeOffset timestamp => timestamp.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture),
        _ => string.Empty
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, string language) =>
        throw new NotSupportedException("Movement timestamp is display-only.");
}
