using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace TechStore.Client.Converters;

public sealed class HexColorToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language)
    {
        if (value is not string hex || hex.Length != 7 || hex[0] != '#')
        {
            return new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        }

        try
        {
            var red = System.Convert.ToByte(hex.Substring(1, 2), 16);
            var green = System.Convert.ToByte(hex.Substring(3, 2), 16);
            var blue = System.Convert.ToByte(hex.Substring(5, 2), 16);
            return new SolidColorBrush(Color.FromArgb(255, red, green, blue));
        }
        catch (FormatException)
        {
            return new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, string language) =>
        throw new NotSupportedException("Variant color is displayed as a brush only.");
}
