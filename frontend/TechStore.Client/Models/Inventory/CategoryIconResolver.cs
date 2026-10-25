using System.Globalization;
using System.Text;
using TechStore.Client.Controls;

namespace TechStore.Client.Models.Inventory;

public static class CategoryIconResolver
{
    public static CategoryIconDescriptor Resolve(string? name, string? slug)
    {
        var text = Normalize($"{name} {slug}");
        if (Contains(text, "tablet", "ipad", "may tinh bang"))
            return new("tablet", StatusTone.Success);
        if (Contains(text, "laptop", "macbook", "may tinh xach tay"))
            return new("laptop", StatusTone.Accent);
        if (Contains(text, "accessor", "audio", "headphone", "phu kien", "am thanh", "tai nghe", "loa", "sac", "cap ket noi"))
            return new("headphones", StatusTone.Warning);
        if (Contains(text, "smartphone", "phone", "dien thoai", "galaxy", "redmi"))
            return new("phone", StatusTone.Info);
        return new("category", StatusTone.Neutral);
    }

    private static bool Contains(string text, params string[] candidates) => candidates.Any(text.Contains);

    private static string Normalize(string text)
    {
        var result = new StringBuilder();
        foreach (var character in text.ToLowerInvariant().Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                result.Append(character == 'đ' ? 'd' : character == '-' ? ' ' : character);
        return result.ToString();
    }
}
