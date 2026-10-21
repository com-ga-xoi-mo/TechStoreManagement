namespace TechStore.Shared.DTOs;

/// <summary>Human name of a spec key; <see cref="ValueKind"/> is <c>number</c>, <c>string</c> or <c>boolean</c>.</summary>
public class SpecDefinitionDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public string ValueKind { get; set; } = string.Empty;
}
