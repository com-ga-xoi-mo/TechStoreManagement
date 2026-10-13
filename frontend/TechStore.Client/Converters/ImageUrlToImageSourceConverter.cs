using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace TechStore.Client.Converters;

public sealed class ImageUrlToImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, string language)
    {
        if (value is not string source || !Uri.TryCreate(source, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return new BitmapImage(uri);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, string language) =>
        throw new NotSupportedException("Image source conversion is one-way.");
}
