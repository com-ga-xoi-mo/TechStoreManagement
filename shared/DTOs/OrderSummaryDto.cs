namespace TechStore.Shared.DTOs;

public class OrderSummaryDto
{
    public Guid Id { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
