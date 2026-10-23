namespace TechStore.Shared.Requests;

public class CustomerSearchRequest
{
    public string? Q { get; set; }
    public string? Tier { get; set; }
    public bool? IsActive { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
