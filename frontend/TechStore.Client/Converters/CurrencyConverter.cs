using System;
using System.Globalization;
using Microsoft.UI.Xaml.Data;

namespace TechStore.Client.Converters;

/// <summary>
/// Converts numeric values (decimal, double, int, long, string) into formatted Vietnamese Dong currency string (e.g., 34.990.000 ₫).
/// If the value is null, empty, or non-numeric, returns an explicit placeholder ("—").
/// </summary>
public sealed class CurrencyConverter : IValueConverter
{
    private static readonly CultureInfo VietnameseCulture = new("vi-VN");

    public object Convert(object? value, Type targetType, object? parameter, string language)
    {
        if (value == null)
        {
            return "—";
        }

        if (value is decimal decVal)
        {
            return FormatVnd(decVal);
        }

        if (value is double dblVal)
        {
            return FormatVnd((decimal)dblVal);
        }

        if (value is int intVal)
        {
            return FormatVnd(intVal);
        }

        if (value is long lngVal)
        {
            return FormatVnd(lngVal);
        }

        if (value is string strVal)
        {
            if (string.IsNullOrWhiteSpace(strVal))
            {
                return "—";
            }

            if (decimal.TryParse(strVal, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedDecimal) ||
                decimal.TryParse(strVal, NumberStyles.Any, VietnameseCulture, out parsedDecimal))
            {
                return FormatVnd(parsedDecimal);
            }

            return "—";
        }

        return "—";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, string language)
    {
        throw new NotSupportedException("Two-way binding is not supported for CurrencyConverter.");
    }

    private static string FormatVnd(decimal amount)
    {
        // Spec requirement: 34990000 -> 34.990.000 ₫
        return $"{amount.ToString("#,##0", VietnameseCulture)} ₫";
    }
}
