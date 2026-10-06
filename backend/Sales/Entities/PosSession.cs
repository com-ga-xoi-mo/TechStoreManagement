using TechStore.Api.Common.Entities;
using TechStore.Api.Identity.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Sales.Entities;

public class PosSession : BaseEntity
{
    public Guid CashierUserId { get; set; }
    public decimal OpeningBalance { get; set; } = 0;
    public decimal? ClosingBalance { get; set; }
    public decimal CashSalesTotal { get; set; } = 0;
    public decimal VietQrSalesTotal { get; set; } = 0;
    public decimal CardSalesTotal { get; set; } = 0;
    public PosSessionStatus Status { get; set; } = PosSessionStatus.Open;
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    // Navigation properties
    public virtual User CashierUser { get; set; } = null!;
    public virtual ICollection<VietQrTransaction> VietQrTransactions { get; set; } = new List<VietQrTransaction>();
}
