using System;
using TechStore.Client.Models.Catalog;
using Microsoft.UI.Xaml.Data;

namespace TechStore.Client.Converters;

public sealed class ProductStatusToVietnameseConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language) => value switch
    {
        ProductStatus.InStock => "Còn hàng",
        ProductStatus.LowStock => "Sắp hết",
        ProductStatus.OutOfStock => "Hết hàng",
        ProductStatus.Discontinued => "Ngừng bán",
        _ => "Không rõ trạng thái"
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, string language) =>
        throw new NotSupportedException("Product status is displayed as localized text only.");
}
