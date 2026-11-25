namespace TechStore.Api.Sales.Entities;

public sealed class CashRefundPayout
{
    public Guid RefundId { get; set; }
    public Guid ShiftId { get; set; }
    public decimal Amount { get; set; }
}
