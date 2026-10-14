using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace TechStore.Client.Converters;

public sealed class ProductImageSourceConverter : IValueConverter
{
    public Uri? BaseAddress { get; set; }

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string text || string.IsNullOrWhiteSpace(text)) return null;
        if (!Uri.TryCreate(text.Trim(), UriKind.RelativeOrAbsolute, out var uri)) return null;
        if (uri.IsAbsoluteUri)
        {
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
        }
        else
        {
            if (BaseAddress is null || !Uri.TryCreate(BaseAddress, uri, out uri)) return null;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
        }

        return new BitmapImage(uri);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class ProductPriceRangeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not TechStore.Client.Models.Catalog.ProductSummary product) return string.Empty;
        var culture = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
        var from = product.PriceFrom.ToString("N0", culture);
        var to = product.PriceTo.ToString("N0", culture);
        return product.PriceFrom == product.PriceTo ? $"{from} ₫" : $"{from} – {to} ₫";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class ProductActiveLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value is true
        ? "Đang hoạt động"
        : "Ngừng hoạt động";

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class ProductVariantCountConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value is int count
        ? count == 0 ? "Không có biến thể" : $"{count} biến thể"
        : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class ProductCurrencyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value switch
    {
        decimal amount => $"{amount.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"))} ₫",
        _ => "Chưa có"
    };

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
