using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TechStore.Api.Catalog.Services;

public static class ProductSpecFilterHelper
{
    private static readonly Regex KeyRegex = new(@"^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

    public record ParseResult(bool IsValid, string? JsonString, Dictionary<string, object>? ParsedValues, string? ErrorMessage);

    public static ParseResult Parse(IEnumerable<string>? specFilters)
    {
        if (specFilters == null)
        {
            return new ParseResult(true, null, null, null);
        }

        var dict = new Dictionary<string, object>();

        foreach (var rawItem in specFilters)
        {
            if (string.IsNullOrWhiteSpace(rawItem))
            {
                continue;
            }

            var item = rawItem.Trim();
            var colonIndex = item.IndexOf(':');
            if (colonIndex <= 0)
            {
                return new ParseResult(false, null, null, $"Invalid spec format '{rawItem}'. Expected format 'key:value'.");
            }

            var key = item[..colonIndex].Trim();
            var valStr = item[(colonIndex + 1)..].Trim();

            if (string.IsNullOrEmpty(key) || !KeyRegex.IsMatch(key))
            {
                return new ParseResult(false, null, null, $"Spec key '{key}' is invalid. Keys must match '^[a-z][a-z0-9_]*$'.");
            }

            if (string.IsNullOrEmpty(valStr))
            {
                return new ParseResult(false, null, null, $"Spec value for key '{key}' cannot be empty.");
            }

            object typedVal;
            if (string.Equals(valStr, "true", StringComparison.OrdinalIgnoreCase))
            {
                typedVal = true;
            }
            else if (string.Equals(valStr, "false", StringComparison.OrdinalIgnoreCase))
            {
                typedVal = false;
            }
            else if (long.TryParse(valStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longVal))
            {
                typedVal = longVal;
            }
            else if (decimal.TryParse(valStr, NumberStyles.Float | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var decVal))
            {
                typedVal = decVal;
            }
            else
            {
                typedVal = valStr;
            }

            if (dict.TryGetValue(key, out var existingVal))
            {
                if (!Equals(existingVal, typedVal))
                {
                    return new ParseResult(false, null, null, $"Duplicate spec key '{key}' specified with conflicting values.");
                }
            }
            else
            {
                dict[key] = typedVal;
            }
        }

        if (dict.Count == 0)
        {
            return new ParseResult(true, null, null, null);
        }

        var json = JsonSerializer.Serialize(dict);
        return new ParseResult(true, json, dict, null);
    }
}
