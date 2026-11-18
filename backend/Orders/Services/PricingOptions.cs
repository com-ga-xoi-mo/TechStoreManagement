using System.ComponentModel.DataAnnotations;

namespace TechStore.Api.Orders.Services;

/// <summary>Pricing settings (section "Pricing"). Checked at startup; the VAT rate is copied onto each order.</summary>
public sealed class PricingOptions
{
    public const string SectionName = "Pricing";

    /// <summary>VAT rate in percent added on top of catalog prices (catalog prices exclude VAT).</summary>
    [Range(0, 100)]
    public decimal VatRatePercent { get; set; } = 10m;

    /// <summary>Most a SalesStaff may take off an order (line plus order discounts), in percent of the amount before discounts.</summary>
    [Range(0, 100)]
    public decimal SalesStaffMaxDiscountPercent { get; set; } = 10m;
}
