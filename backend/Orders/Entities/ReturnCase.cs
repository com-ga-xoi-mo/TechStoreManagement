using TechStore.Api.Common.Entities;

namespace TechStore.Api.Orders.Entities;

public static class ReturnStatus
{
    public const string Pending = "Pending";
    public const string AwaitingBank = "AwaitingBank";
    public const string Refunded = "Refunded";
    public const string Failed = "Failed";
}

public sealed class ReturnCase : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid RequestedById { get; set; }
    public bool WasUndelivered { get; set; }
    public Guid? PayoutActorId { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = ReturnStatus.Pending;
    public string? Method { get; set; }
    public string? BankReference { get; set; }
    public Guid? ReplacementOrderId { get; set; }
    public DateTime? BankApprovedAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    public List<ReturnCaseLine> Lines { get; set; } = [];
}

public sealed class ReturnCaseLine : BaseEntity
{
    public Guid CaseId { get; set; }
    public Guid OrderItemId { get; set; }
    public int Quantity { get; set; }
    public decimal NetAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public OrderItem Item { get; set; } = null!;
    public List<OrderReturn> Settlements { get; set; } = [];
}
