namespace TechStore.Api.Common.Data;

internal static class DemoSeedNaturalKey
{
    public static T? FindSingle<T>(IEnumerable<T> rows, Func<T, string> keySelector, string key, string entity)
    {
        var matches = rows
            .Where(row => string.Equals(Normalize(keySelector(row)), Normalize(key), StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();
        if (matches.Length > 1)
            throw new InvalidOperationException($"Demo seed conflict for {entity} '{key}': duplicate natural key.");
        return matches.SingleOrDefault();
    }

    private static string Normalize(string value) => value.Trim();
}
