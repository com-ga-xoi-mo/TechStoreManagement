using System.Globalization;
using System.Text.Json;
using TechStore.Client.Controls;

namespace TechStore.Client.Controls.Presentation;

public static class CatalogPresentation
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

    public static string FormatMoney(decimal amount) =>
        $"{amount.ToString("N0", VietnameseCulture)} ₫";

    public static string PriceRange(decimal from, decimal to)
    {
        var fromText = from.ToString("N0", VietnameseCulture);
        var toText = to.ToString("N0", VietnameseCulture);
        return from == to ? $"{fromText} ₫" : $"{fromText} – {toText} ₫";
    }

    public static string ActiveLabel(bool isActive) =>
        isActive ? "Đang hoạt động" : "Ngừng hoạt động";

    public static StatusTone ActiveTone(bool isActive) =>
        isActive ? StatusTone.Success : StatusTone.Neutral;

    public static string VariantCountLabel(int count) =>
        count == 0 ? "Không có biến thể" : $"{count} biến thể";

    public static string SpecValueText(JsonElement element) =>
        element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? string.Empty
            : element.GetRawText();

    public static string OptionalText(string? text) =>
        string.IsNullOrWhiteSpace(text) ? "—" : text;

    public static string SerialTrackedLabel(bool isSerialTracked) =>
        isSerialTracked ? "Có" : "Không";

    public static Uri? ResolveImageUri(string? url, Uri? apiBase)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim();
        if (!Uri.IsWellFormedUriString(trimmed, UriKind.RelativeOrAbsolute)) return null;
        if (!Uri.TryCreate(trimmed, UriKind.RelativeOrAbsolute, out var uri)) return null;
        if (uri.IsAbsoluteUri)
        {
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
            return uri;
        }

        if (apiBase is null || !Uri.TryCreate(apiBase, uri, out var combined)) return null;
        if (combined.Scheme != Uri.UriSchemeHttp && combined.Scheme != Uri.UriSchemeHttps) return null;
        return combined;
    }

    public static MarginInfo? MarginText(decimal basePrice, decimal costPrice)
    {
        if (basePrice <= 0) return null;
        var profit = basePrice - costPrice;
        if (profit >= 0)
        {
            var pct = Math.Round(profit / basePrice * 100m, 1, MidpointRounding.AwayFromZero);
            var pctText = pct.ToString("0.0", VietnameseCulture);
            return new MarginInfo($"Lãi {FormatMoney(profit)} ({pctText}%)", IsLoss: false);
        }

        var loss = -profit;
        return new MarginInfo($"Lỗ {FormatMoney(loss)}", IsLoss: true);
    }
}

public sealed record MarginInfo(string Text, bool IsLoss);
