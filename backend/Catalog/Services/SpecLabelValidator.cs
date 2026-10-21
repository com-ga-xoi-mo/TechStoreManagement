using System.Text.Json;
using System.Text.RegularExpressions;
using TechStore.Api.Catalog.Entities;
using TechStore.Shared.Requests;

namespace TechStore.Api.Catalog.Services;

/// <summary>Shape checks of <c>specLabels</c> that need no database (design D13).</summary>
public static class SpecLabelValidator
{
    public const int MaxLabelLength = 100;
    public const int MaxUnitLength = 20;
    private static readonly Regex KeyRegex = new(@"^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

    /// <param name="specGroups">Product specs and every variant's specs of the request.</param>
    public static void Validate(
        IDictionary<string, SpecLabelRequest>? labels,
        IEnumerable<IDictionary<string, JsonElement>?> specGroups,
        IDictionary<string, List<string>> errors)
    {
        if (labels is null || labels.Count == 0) return;

        var kindsByKey = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var group in specGroups)
        {
            if (group is null) continue;
            foreach (var (key, value) in group)
            {
                if (SpecValueKinds.Of(value) is not { } kind) continue;
                if (!kindsByKey.TryGetValue(key, out var kinds)) kindsByKey[key] = kinds = [];
                kinds.Add(kind);
            }
        }

        var seenLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, label) in labels)
        {
            var field = $"specLabels.{key}";
            void Add(string message)
            {
                if (!errors.TryGetValue(field, out var list)) errors[field] = list = [];
                list.Add(message);
            }

            if (!KeyRegex.IsMatch(key) || key.Length > 64)
            {
                Add($"Spec key '{key}' must match '^[a-z][a-z0-9_]*$' and have at most 64 characters.");
                continue;
            }
            if (!kindsByKey.TryGetValue(key, out var usedKinds))
            {
                Add($"Spec key '{key}' is not used by any spec in this request.");
                continue;
            }

            var text = label?.Label?.Trim();
            if (string.IsNullOrEmpty(text))
                Add($"A name is required for spec '{key}'.");
            else if (text.Length > MaxLabelLength)
                Add($"The name of spec '{key}' cannot exceed {MaxLabelLength} characters.");
            else if (seenLabels.TryGetValue(text, out var otherKey))
                Add($"The name '{text}' is also given to spec '{otherKey}' in this request.");
            else
                seenLabels[text] = key;

            if (label?.Unit is { } unit && unit.Trim().Length > MaxUnitLength)
                Add($"The unit of spec '{key}' cannot exceed {MaxUnitLength} characters.");

            if (!SpecValueKinds.IsKnown(label?.ValueKind))
                Add($"The value kind of spec '{key}' must be number, string or boolean.");
            else if (usedKinds.Count != 1 || !usedKinds.Contains(label!.ValueKind!))
                Add($"The values of spec '{key}' do not match the value kind '{label!.ValueKind}'.");
        }
    }
}
