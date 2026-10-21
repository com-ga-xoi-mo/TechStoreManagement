using System.Text.Json;

namespace TechStore.Shared.DTOs;

/// <summary>A specification key that occurs in stored products, with the values that can be filtered.</summary>
public class ProductSpecKeyDto
{
    public string Key { get; set; } = string.Empty;
    public int ProductCount { get; set; }
    public List<ProductSpecValueDto> Values { get; set; } = [];
}

public class ProductSpecValueDto
{
    public JsonElement Value { get; set; }
    public string Label { get; set; } = string.Empty;
    /// <summary>Send as <c>spec=&lt;key&gt;:&lt;FilterValue&gt;</c> to match exactly this value.</summary>
    public string FilterValue { get; set; } = string.Empty;
    public int ProductCount { get; set; }
}
