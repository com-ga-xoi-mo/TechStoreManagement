namespace TechStore.Shared.DTOs;

public class PosShiftDto
{
    public Guid Id { get; set; }
    public Guid CashierUserId { get; set; }
    public string? CashierName { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal OpeningBalance { get; set; }
    public decimal CashSalesTotal { get; set; }
    public decimal? ExpectedClosingBalance { get; set; }
    public decimal? ClosingBalance { get; set; }
    public decimal? Discrepancy { get; set; }
    public string? ClosingNote { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}
