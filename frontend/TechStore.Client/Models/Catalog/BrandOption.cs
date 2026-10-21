namespace TechStore.Client.Models.Catalog;

/// <summary>A brand used by stored products, in the spelling most of them use (API design D14).</summary>
public sealed record BrandOption(string Name, int ProductCount);
