namespace TechStore.Api.Catalog.Repositories;

/// <summary>One exact brand spelling and how many products use it.</summary>
public record BrandCount(string Brand, int ProductCount);
