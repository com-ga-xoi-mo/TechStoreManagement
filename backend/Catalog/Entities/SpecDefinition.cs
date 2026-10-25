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
}
