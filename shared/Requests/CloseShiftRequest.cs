namespace TechStore.Shared.Requests;

public class CloseShiftRequest
{
    public decimal? ClosingBalance { get; set; }
    public string? Note { get; set; }
}
