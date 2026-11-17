namespace TechStore.Shared.Requests;

public class PosShiftSearchRequest
{
    public string? Status { get; set; }
    public Guid? CashierUserId { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
