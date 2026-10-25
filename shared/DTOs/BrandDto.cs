namespace TechStore.Shared.DTOs;

/// <summary>A brand used by stored products, with the spelling most of them use.</summary>
public class BrandDto
{
    public string Name { get; set; } = string.Empty;
    public int ProductCount { get; set; }
}
