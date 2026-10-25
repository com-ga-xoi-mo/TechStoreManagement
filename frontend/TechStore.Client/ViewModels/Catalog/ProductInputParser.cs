using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using TechStore.Client.Models.Catalog;

namespace TechStore.Client.ViewModels.Catalog;

public static class ProductInputParser
{
    private static readonly Regex KeyRegex = new(@"^[a-z][a-z0-9_]*$", RegexOptions.Compiled);
    private static readonly Regex ThousandsWithDotRegex = new(@"^\d{1,3}(\.\d{3})+(,\d{1,2})?$", RegexOptions.Compiled);
    private static readonly Regex CommaDecimalRegex = new(@"^\d+(,\d{1,2})?$", RegexOptions.Compiled);
    private static readonly Regex DotDecimalRegex = new(@"^\d+(\.\d{1,2})?$", RegexOptions.Compiled);
    private static readonly Regex NumberSpecRegex = new(@"^-?(0|[1-9]\d*)([.,]\d+)?$", RegexOptions.Compiled);

    public static bool TryParseMoney(string? text, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var s = text.Trim();
        if (s.StartsWith('-')) return false;

        if (ThousandsWithDotRegex.IsMatch(s))
        {
            var normalized = s.Replace(".", "").Replace(",", ".");
            if (decimal.TryParse(normalized, CultureInfo.InvariantCulture, out var parsed)
                && parsed >= 0m && parsed < 10_000_000_000_000_000m)
            {
                value = parsed;
                return true;
            }
            return false;
        }

        if (CommaDecimalRegex.IsMatch(s))
        {
            var normalized = s.Replace(",", ".");
            if (decimal.TryParse(normalized, CultureInfo.InvariantCulture, out var parsed)
                && parsed >= 0m && parsed < 10_000_000_000_000_000m)
            {
                value = parsed;
                return true;
            }
            return false;
        }

        if (DotDecimalRegex.IsMatch(s))
        {
            if (decimal.TryParse(s, CultureInfo.InvariantCulture, out var parsed)
                && parsed >= 0m && parsed < 10_000_000_000_000_000m)
            {
                value = parsed;
                return true;
            }
            return false;
        }

        return false;
    }

    public static SpecParseResult ParseSpecs(IReadOnlyList<SpecDraft> rows)
    {
        var values = new Dictionary<string, JsonElement>();
        var rowErrors = new List<string?>(rows.Count);
        var seenKeys = new HashSet<string>();

        foreach (var row in rows)
        {
            var key = row.Key?.Trim() ?? string.Empty;
            string? error = null;

            if (!KeyRegex.IsMatch(key))
            {
                error = "Tên thông số phải dùng snake_case, ví dụ ram_gb.";
            }
            else if (!seenKeys.Add(key))
            {
                error = "Tên thông số bị trùng.";
            }

            var val = row.Value?.Trim() ?? string.Empty;
            if (error is null && val.Length == 0)
            {
                error = "Giá trị không được để trống.";
            }

            JsonElement parsedElement = default;
            if (error is null)
            {
                switch (row.Kind)
                {
                    case SpecValueKind.Text:
                        parsedElement = JsonSerializer.SerializeToElement(val);
                        break;

                    case SpecValueKind.Number:
                        if (!NumberSpecRegex.IsMatch(val))
                        {
                            error = "Giá trị phải là số, ví dụ 16 hoặc 6.1.";
                        }
                        else
                        {
                            var normalized = val.Replace(',', '.');
                            if (!decimal.TryParse(normalized, CultureInfo.InvariantCulture, out _))
                            {
                                error = "Giá trị phải là số, ví dụ 16 hoặc 6.1.";
                            }
                            else
                            {
                                using var doc = JsonDocument.Parse(normalized);
                                parsedElement = doc.RootElement.Clone();
                            }
                        }
                        break;

                    case SpecValueKind.Boolean:
                        if (string.Equals(val, "true", StringComparison.OrdinalIgnoreCase))
                        {
                            using var doc = JsonDocument.Parse("true");
                            parsedElement = doc.RootElement.Clone();
                        }
                        else if (string.Equals(val, "false", StringComparison.OrdinalIgnoreCase))
                        {
                            using var doc = JsonDocument.Parse("false");
                            parsedElement = doc.RootElement.Clone();
                        }
                        else
                        {
                            error = "Giá trị phải là true hoặc false.";
                        }
                        break;
                }
            }

            rowErrors.Add(error);
            if (error is null)
            {
                values[key] = parsedElement;
            }
        }

        return new SpecParseResult(values, rowErrors);
    }

    public static IReadOnlyList<SpecDraft> SpecsFromJson(IReadOnlyDictionary<string, JsonElement> specs)
    {
        var list = new List<SpecDraft>(specs.Count);
        foreach (var (key, element) in specs)
        {
            SpecValueKind kind;
            string value;

            switch (element.ValueKind)
            {
                case JsonValueKind.Number:
                    kind = SpecValueKind.Number;
                    value = element.GetRawText();
                    break;

                case JsonValueKind.True:
                    kind = SpecValueKind.Boolean;
                    value = "true";
                    break;

                case JsonValueKind.False:
                    kind = SpecValueKind.Boolean;
                    value = "false";
                    break;

                case JsonValueKind.String:
                    kind = SpecValueKind.Text;
                    value = element.GetString() ?? string.Empty;
                    break;

                default:
                    kind = SpecValueKind.Text;
                    value = element.GetRawText();
                    break;
            }

            list.Add(new SpecDraft(key, kind, value));
        }

        return list;
    }
}

public sealed record SpecParseResult(
    Dictionary<string, JsonElement> Values,
    IReadOnlyList<string?> RowErrors)
{
    public bool IsValid => RowErrors.All(e => e is null);
}
