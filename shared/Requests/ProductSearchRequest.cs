namespace TechStore.Shared.Requests;

public class ProductSearchRequest
{
    public string? Q { get; set; }
    public string? Brand { get; set; }
    public Guid? CategoryId { get; set; }
    public bool? IsActive { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public List<string>? Spec { get; set; }
    public string? Sort { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
