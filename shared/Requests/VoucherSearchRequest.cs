namespace TechStore.Shared.Requests;

public class VoucherSearchRequest
{
    public string? Q { get; set; }
    public bool? IsActive { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
