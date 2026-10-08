using System.Text.Json;
using System.Text.RegularExpressions;

namespace TechStore.Api.Catalog.Services;

public static class ProductSpecValidatorHelper
{
    private static readonly Regex KeyRegex = new(@"^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

    public static bool ValidateSpecs(
        IDictionary<string, JsonElement>? specs,
        string fieldPath,
        IDictionary<string, List<string>> errors)
    {
        if (specs == null)
        {
            return true;
        }

        var fieldErrors = new List<string>();

        foreach (var (key, val) in specs)
        {
            if (string.IsNullOrWhiteSpace(key) || !KeyRegex.IsMatch(key))
            {
                fieldErrors.Add($"Spec key '{key}' is invalid. Keys must be flat snake_case matching '^[a-z][a-z0-9_]*$'.");
            }

            switch (val.ValueKind)
            {
                case JsonValueKind.String:
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    break;
                case JsonValueKind.Object:
                    fieldErrors.Add($"Spec value for key '{key}' cannot be a nested object. Only flat primitive values are allowed.");
                    break;
                case JsonValueKind.Array:
                    fieldErrors.Add($"Spec value for key '{key}' cannot be an array. Only flat primitive values are allowed.");
                    break;
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    fieldErrors.Add($"Spec value for key '{key}' cannot be null or undefined.");
                    break;
                default:
                    fieldErrors.Add($"Spec value for key '{key}' has an unsupported type '{val.ValueKind}'.");
                    break;
            }
        }

        if (fieldErrors.Count > 0)
        {
            if (!errors.TryGetValue(fieldPath, out var list))
            {
                list = [];
                errors[fieldPath] = list;
            }
            list.AddRange(fieldErrors);
            return false;
        }

        return true;
    }
}
