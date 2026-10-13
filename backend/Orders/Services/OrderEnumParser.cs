namespace TechStore.Api.Orders.Services;

public static class OrderEnumParser
{
    // Accepts only enum names (case-insensitive); numeric text and undefined values are rejected.
    public static bool TryParse<TEnum>(string? text, out TEnum value) where TEnum : struct, Enum
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (char.IsDigit(trimmed[0]) || trimmed[0] == '-' || trimmed[0] == '+')
        {
            return false;
        }

        return Enum.TryParse(trimmed, ignoreCase: true, out value) && Enum.IsDefined(value);
    }
}
