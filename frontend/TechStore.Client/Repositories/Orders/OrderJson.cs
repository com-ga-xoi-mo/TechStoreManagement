using System.Text.Json;
using TechStore.Client.Services.Api;

namespace TechStore.Client.Repositories.Orders;

/// <summary>Shape checks for the Orders API payloads. Every failure is an <see cref="ApiErrorKind.InvalidResponse"/>.</summary>
internal static class OrderJson
{
    public static ApiException InvalidResponse(string message, Exception? inner = null) =>
        new(ApiErrorKind.InvalidResponse, message, innerException: inner);

    public static void RequireObject(JsonElement value, string description)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw InvalidResponse($"The {description} must be a JSON object.");
    }

    public static JsonElement Require(JsonElement value, string property, JsonValueKind kind)
    {
        if (!value.TryGetProperty(property, out var result) || result.ValueKind != kind)
            throw InvalidResponse($"The API response is missing or has an invalid '{property}' field.");
        return result;
    }

    public static void RequireString(JsonElement value, string property) =>
        _ = Require(value, property, JsonValueKind.String);

    public static void RequireNullableString(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var result)
            || result.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
            throw InvalidResponse($"The API response is missing or has an invalid '{property}' field.");
    }

    public static void RequireGuid(JsonElement value, string property)
    {
        var field = Require(value, property, JsonValueKind.String);
        if (!Guid.TryParse(field.GetString(), out var id) || id == Guid.Empty)
            throw InvalidResponse($"The API response has an invalid '{property}' field.");
    }

    public static void RequireNullableGuid(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var field))
            throw InvalidResponse($"The API response is missing the '{property}' field.");
        if (field.ValueKind == JsonValueKind.Null) return;
        if (field.ValueKind != JsonValueKind.String || !Guid.TryParse(field.GetString(), out var id) || id == Guid.Empty)
            throw InvalidResponse($"The API response has an invalid '{property}' field.");
    }

    public static void RequireInt(JsonElement value, string property)
    {
        if (!Require(value, property, JsonValueKind.Number).TryGetInt32(out _))
            throw InvalidResponse($"The API response has an invalid '{property}' field.");
    }

    public static void RequireDecimal(JsonElement value, string property)
    {
        if (!Require(value, property, JsonValueKind.Number).TryGetDecimal(out _))
            throw InvalidResponse($"The API response has an invalid '{property}' field.");
    }

    public static void RequireDateTime(JsonElement value, string property)
    {
        if (!Require(value, property, JsonValueKind.String).TryGetDateTime(out _))
            throw InvalidResponse($"The API response has an invalid '{property}' field.");
    }

    public static void RequireNullableDateTime(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var result)
            || result.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)
            || (result.ValueKind == JsonValueKind.String && !result.TryGetDateTime(out _)))
            throw InvalidResponse($"The API response is missing or has an invalid '{property}' field.");
    }

    /// <summary>Enum names only. Numeric text such as "1" is rejected even though Enum.TryParse would accept it.</summary>
    public static T ParseEnum<T>(string? text, string property) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(text) || !char.IsLetter(text[0])
            || !Enum.TryParse<T>(text, ignoreCase: true, out var value) || !Enum.IsDefined(value))
            throw InvalidResponse($"The API response has an unknown '{property}' value.");
        return value;
    }
}
