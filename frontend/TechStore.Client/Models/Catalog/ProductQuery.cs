namespace TechStore.Client.Models.Catalog;

public sealed record ProductQuery
{
    public string? Query { get; init; }
    public string? Brand { get; init; }
    public Guid? CategoryId { get; init; }
    public bool? IsActive { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public IReadOnlyList<string> Specs { get; init; } = [];
    public string Sort { get; init; } = "createdAt:desc";
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
