namespace TechStore.Api.Catalog.Entities;

/// <summary>Shared human name, unit and value type of one spec key (design D13). The key is the identity.</summary>
public class SpecDefinition
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public string ValueKind { get; set; } = SpecValueKinds.String;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class SpecValueKinds
{
    public const string Number = "number";
    public const string String = "string";
    public const string Boolean = "boolean";

    public static bool IsKnown(string? kind) => kind is Number or String or Boolean;

    /// <summary>The kind of a scalar JSON value, or null for objects, arrays and null.</summary>
    public static string? Of(System.Text.Json.JsonElement value) => value.ValueKind switch
    {
        System.Text.Json.JsonValueKind.Number => Number,
        System.Text.Json.JsonValueKind.String => String,
        System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False => Boolean,
        _ => null
    };
}
