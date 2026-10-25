using System.Globalization;
using System.Text;

namespace TechStore.Client.Features.Categories.Models;

public static class CategoryIconResolver
{
    public static CategoryIconDescriptor Resolve(string? name, string? slug)
    {
        var text = Normalize($"{name} {slug}");
        if (Contains(text, "tablet", "ipad", "may tinh bang"))
            return new("tablet", "#F0FDFA", "#0D9488", "#CCFBF1");
        if (Contains(text, "laptop", "macbook", "may tinh xach tay"))
            return new("laptop", "#EEF2FF", "#4F46E5", "#E0E7FF");
        if (Contains(text, "accessor", "audio", "headphone", "phu kien", "am thanh", "tai nghe", "loa", "sac", "cap ket noi"))
            return new("headphones", "#ECFDF5", "#059669", "#D1FAE5");
        if (Contains(text, "smartphone", "phone", "dien thoai", "galaxy", "redmi"))
            return new("phone", "#EFF6FF", "#2563EB", "#DBEAFE");
        return new("category", "#F1F5F9", "#64748B", "#E2E8F0");
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
