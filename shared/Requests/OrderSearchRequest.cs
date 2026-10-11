namespace TechStore.Shared.Requests;

public class OrderSearchRequest
{
    public string? Status { get; set; }
    public Guid? CustomerId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Q { get; set; }
    public string? Sort { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
