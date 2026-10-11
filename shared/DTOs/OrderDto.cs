namespace TechStore.Shared.DTOs;

public class OrderDto
{
    public Guid Id { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public Guid CashierUserId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<OrderItemDto> Items { get; set; } = [];
}
