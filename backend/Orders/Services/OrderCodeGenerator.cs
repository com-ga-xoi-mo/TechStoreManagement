namespace TechStore.Api.Orders.Services;

public static class OrderCodeGenerator
{
    public static string GetPrefix(DateTime utcNow) => $"ORD-{utcNow:yyyyMM}-";

    // lastCode is the highest existing code that starts with the month prefix (or null).
    public static string Next(DateTime utcNow, string? lastCode)
    {
        var prefix = GetPrefix(utcNow);
        var next = 1;

        if (lastCode != null
            && lastCode.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(lastCode.AsSpan(prefix.Length), out var lastSequence)
            && lastSequence >= 0)
        {
            next = lastSequence + 1;
        }

        return $"{prefix}{next:D4}";
    }
}
