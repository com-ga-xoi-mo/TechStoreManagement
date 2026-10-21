using System.Text.Json;
using TechStore.Client.Models.Catalog;

namespace TechStore.Client.Controls.Presentation;

/// <summary>Display rules for product specs: definition names, units and yes/no (UI design D16).</summary>
public static class SpecPresentation
{
    public static string KeyName(string key, string? label) =>
        string.IsNullOrWhiteSpace(label) ? key : label;

    public static string ValueText(JsonElement value, string? unit) => value.ValueKind switch
    {
        JsonValueKind.True => "Có",
        JsonValueKind.False => "Không",
        JsonValueKind.Number => WithUnit(value.GetRawText(), unit),
        JsonValueKind.String => value.GetString() ?? string.Empty,
        _ => value.GetRawText()
    };

    public static string ValueText(string label, SpecValueKind kind, string? unit) => kind switch
    {
        SpecValueKind.Boolean => string.Equals(label, "true", StringComparison.OrdinalIgnoreCase) ? "Có" : "Không",
        SpecValueKind.Number => WithUnit(label, unit),
        _ => label
    };

    public static string WithCount(string text, int count) => $"{text} ({count})";

    public static string KindName(SpecValueKind kind) => kind switch
    {
        SpecValueKind.Number => "Số",
        SpecValueKind.Boolean => "Có/Không",
        _ => "Chữ"
    };

    private static string WithUnit(string number, string? unit) =>
        string.IsNullOrWhiteSpace(unit) ? number : $"{number} {unit}";
}
