namespace TechStore.Shared.Requests;

/// <summary>Name for a spec key that has no definition yet; ignored when the key is already defined.</summary>
public class SpecLabelRequest
{
    public string? Label { get; set; }
    public string? Unit { get; set; }
    /// <summary><c>number</c>, <c>string</c> or <c>boolean</c>.</summary>
    public string? ValueKind { get; set; }
}
