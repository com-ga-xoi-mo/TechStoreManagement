using TechStore.Api.Common.Entities;
using TechStore.Api.Identity.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Orders.Entities;

public class OrderReturn : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid OrderItemId { get; set; }
    public Guid ProcessedByUserId { get; set; }
    public int Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal RefundAmount { get; set; }
    public PaymentMethod RefundMethod { get; set; }
    public Guid? RefundPosSessionId { get; set; }
    public DateTime RefundedAt { get; set; } = DateTime.UtcNow;
    public bool IsRestocked { get; set; } = false;

    // Navigation properties
    public virtual Order Order { get; set; } = null!;
    public virtual OrderItem OrderItem { get; set; } = null!;
    public virtual User ProcessedByUser { get; set; } = null!;
}
