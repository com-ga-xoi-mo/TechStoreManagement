namespace TechStore.Shared.Enums;

/// <summary>Where an order's order-level discount came from; an order uses at most one.</summary>
public enum OrderDiscountSource
{
    None,
    Manual,
    Vip,
    Voucher
}
