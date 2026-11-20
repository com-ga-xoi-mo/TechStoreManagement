namespace TechStore.Shared.Requests;

public class SupplierFilterRequest
{
    public string? Search { get; set; }
    public bool? IsActive { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
