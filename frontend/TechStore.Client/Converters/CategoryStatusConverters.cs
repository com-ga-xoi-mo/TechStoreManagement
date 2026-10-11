using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace TechStore.Client.Converters;

public sealed class CategoryStatusLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language) =>
        value is true ? "Đang kích hoạt" : "Đã ẩn";

    public object ConvertBack(object? value, Type targetType, object? parameter, string language) =>
        throw new NotSupportedException("Category status is read-only in this view.");
}

public sealed class CategoryStatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language) =>
        (Brush)Microsoft.UI.Xaml.Application.Current.Resources[
            value is true ? "TechStoreSuccessBgBrush" : "TechStoreNeutralBgBrush"];

    public object ConvertBack(object? value, Type targetType, object? parameter, string language) =>
        throw new NotSupportedException("Category status is read-only in this view.");
}

public sealed class SelectionStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language) =>
        value is true ? "Đang chọn" : "Chưa chọn";

    public object ConvertBack(object? value, Type targetType, object? parameter, string language) =>
        throw new NotSupportedException("Selection status is display-only.");
}
