using System.Text.Json;
using TechStore.Api.Catalog.Repositories;
using TechStore.Shared.DTOs;

namespace TechStore.Api.Catalog.Services;

/// <summary>Turns spec value counts into filter suggestions whose filter value round-trips through the spec filter.</summary>
public static class ProductSpecSuggestionBuilder
{
    public const int MaxValuesPerKey = 100;

    public static List<ProductSpecKeyDto> Build(IEnumerable<SpecValueCount> rows)
    {
        var byKey = rows.GroupBy(row => row.Key, StringComparer.Ordinal);
        var keys = new List<ProductSpecKeyDto>();
        foreach (var group in byKey)
        {
            var values = group
                .Where(row => row.ValueJson is not null)
                .Select(row => ToValue(group.Key, row))
                .Where(value => value is not null)
                .Select(value => value!)
                .OrderBy(value => KindOrder(value.Value))
                .ThenBy(value => value.Value.ValueKind == JsonValueKind.Number ? value.Value.GetDouble() : 0d)
                .ThenBy(value => value.Label, StringComparer.Ordinal)
                .Take(MaxValuesPerKey)
                .ToList();
            if (values.Count == 0) continue;

            var total = group.FirstOrDefault(row => row.ValueJson is null)?.ProductCount
                ?? values.Max(value => value.ProductCount);
            keys.Add(new ProductSpecKeyDto { Key = group.Key, ProductCount = total, Values = values });
        }

        return keys
            .OrderByDescending(key => key.ProductCount)
            .ThenBy(key => key.Key, StringComparer.Ordinal)
            .ToList();
    }

    private static ProductSpecValueDto? ToValue(string key, SpecValueCount row)
    {
        JsonElement element;
        using (var document = JsonDocument.Parse(row.ValueJson!))
            element = document.RootElement.Clone();

        string label;
        string filterValue;
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                label = filterValue = element.GetRawText();
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                label = filterValue = element.GetBoolean() ? "true" : "false";
                break;
            case JsonValueKind.String:
                label = element.GetString() ?? string.Empty;
                if (label.Length == 0) return null;
                filterValue = ReadsBackAsSameString(key, label) ? label : $"\"{label}\"";
                break;
            default:
                return null;
        }

        return new ProductSpecValueDto { Value = element, Label = label, FilterValue = filterValue, ProductCount = row.ProductCount };
    }

    // Reuses the real filter parser, so a suggestion can never disagree with how the filter reads it.
    private static bool ReadsBackAsSameString(string key, string text)
    {
        var parsed = ProductSpecFilterHelper.Parse([$"{key}:{text}"]);
        return parsed.IsValid
            && parsed.ParsedValues!.TryGetValue(key, out var value)
            && value is string read
            && string.Equals(read, text, StringComparison.Ordinal);
    }

    private static int KindOrder(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number => 0,
        JsonValueKind.String => 1,
        JsonValueKind.False => 2,
        _ => 3
    };
}
