using TechStore.Client.Controls.Presentation;
using TechStore.Client.Models.Orders;
using TechStore.Shared.Enums;

namespace TechStore.Client.ViewModels.Orders;

public sealed record PaymentOption(PaymentMethod Method, string Label)
{
    public static IReadOnlyList<PaymentOption> All { get; } =
    [
        .. Enum.GetValues<PaymentMethod>().Select(method => new PaymentOption(method, OrderPresentation.PaymentLabel(method)))
    ];
}

/// <summary>A draft line plus the server's complaint about it (if any), with display-ready text.</summary>
public sealed record DraftLineItem(OrderDraftLine Line, string? Error = null)
{
    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    public string Title => string.IsNullOrWhiteSpace(Line.VariantName)
        ? Line.ProductName
        : $"{Line.ProductName} · {Line.VariantName}";

    /// <summary>The device identifier for a serial-tracked line, otherwise the SKU.</summary>
    public string Detail => Line.IsSerialTracked
        ? $"{Line.Sku} · IMEI/Serial: {(!string.IsNullOrWhiteSpace(Line.Imei) ? Line.Imei : Line.SerialNumber)}"
        : Line.Sku;

    public string Quantity => Line.Quantity.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));

    public string UnitPrice => OrderPresentation.FormatMoney(Line.UnitPrice);

    public string Discount => Line.DiscountAmount > 0 ? "−" + OrderPresentation.FormatMoney(Line.DiscountAmount) : "—";

    public string Total => OrderPresentation.FormatMoney(Line.EstimatedTotal);
}
