using System.Globalization;
using System.Text;

namespace TechStore.Client.ViewModels.Catalog;

/// <summary>Turns the name a user types into a snake_case spec key (UI design D16).</summary>
public static class SpecKeyGenerator
{
    public const int MaxKeyLength = 64;
    private const int MaxUnitLength = 20;

    /// <summary>"Tần số quét (Hz)" → ("Tần số quét", "Hz"); a name without a short trailing "(…)" has no unit.</summary>
    public static (string Label, string? Unit) SplitUnit(string? text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.EndsWith(')'))
        {
            var open = trimmed.LastIndexOf('(');
            if (open > 0)
            {
                var unit = trimmed[(open + 1)..^1].Trim();
                var label = trimmed[..open].Trim();
                if (unit.Length is > 0 and <= MaxUnitLength && label.Length > 0)
                    return (label, unit);
            }
        }
        return (trimmed, null);
    }

    /// <summary>Snake_case key from a label and optional unit, or null when nothing usable is left.</summary>
    public static string? Generate(string? label, string? unit = null)
    {
        var key = Slug(label);
        if (key.Length == 0) return null;
        var unitSlug = Slug(unit);
        if (unitSlug.Length > 0 && !key.EndsWith("_" + unitSlug, StringComparison.Ordinal) && key != unitSlug)
            key = $"{key}_{unitSlug}";
        if (char.IsDigit(key[0])) key = "spec_" + key;
        return key.Length <= MaxKeyLength ? key : key[..MaxKeyLength].TrimEnd('_');
    }

    /// <summary>Lower case, no accents (đ → d), every other character run becomes one underscore.</summary>
    public static string Slug(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var decomposed = text.Trim().Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var lastUnderscore = true;
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            var lower = char.ToLowerInvariant(c);
            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(lower);
                lastUnderscore = false;
            }
            else if (!lastUnderscore)
            {
                builder.Append('_');
                lastUnderscore = true;
            }
        }
        return builder.ToString().Trim('_');
    }
}
