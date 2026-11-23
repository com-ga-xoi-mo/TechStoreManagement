using TechStore.Api.Common.Entities;
using TechStore.Api.Customers.Entities;
using TechStore.Api.Identity.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Orders.Entities;

public class Order : BaseEntity
{
    public string OrderCode { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public Guid CashierUserId { get; set; }
    public Guid? PosSessionId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; } = 0;
    public decimal TaxAmount { get; set; } = 0;
    /// <summary>VAT rate in percent used when the order was priced; never recalculated.</summary>
    public decimal TaxRate { get; set; } = 0;
    public decimal TotalAmount { get; set; }
    public Guid? VoucherId { get; set; }
    /// <summary>Where the order-level discount came from; at most one source per order.</summary>
    public OrderDiscountSource DiscountSource { get; set; } = OrderDiscountSource.None;
    public decimal? CashTendered { get; set; }
    public decimal? ChangeGiven { get; set; }
    public Guid? CheckoutKey { get; set; }
    public string? Notes { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Navigation properties
    public virtual Customer? Customer { get; set; }
    public virtual User CashierUser { get; set; } = null!;
    public virtual Voucher? Voucher { get; set; }
    public virtual ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public virtual ICollection<OrderReturn> Returns { get; set; } = new List<OrderReturn>();
}
