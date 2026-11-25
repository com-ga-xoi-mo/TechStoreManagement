namespace TechStore.Shared.Requests;

public class VietQrSearchRequest
{
    public string? Status { get; set; }
    public Guid? PosSessionId { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
