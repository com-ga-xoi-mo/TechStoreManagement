using System.Globalization;
using TechStore.Shared.Enums;

namespace TechStore.Client.Controls.Presentation;

/// <summary>Pure display rules for orders (no WinUI types, testable headless).</summary>
public static class OrderPresentation
{
    public const string WalkInCustomer = "Khách lẻ";

    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    public static string StatusLabel(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Chờ xử lý",
        OrderStatus.Processing => "Đang xử lý",
        OrderStatus.Completed => "Hoàn tất",
        OrderStatus.Cancelled => "Đã hủy",
        OrderStatus.PartiallyRefunded => "Hoàn tiền một phần",
        OrderStatus.Refunded => "Đã hoàn tiền",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown order status.")
    };

    public static StatusTone StatusTone(OrderStatus status) => status switch
    {
        OrderStatus.Pending => Controls.StatusTone.Warning,
        OrderStatus.Processing => Controls.StatusTone.Info,
        OrderStatus.Completed => Controls.StatusTone.Success,
        OrderStatus.Cancelled => Controls.StatusTone.Neutral,
        OrderStatus.PartiallyRefunded => Controls.StatusTone.Accent,
        OrderStatus.Refunded => Controls.StatusTone.Danger,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown order status.")
    };

    public static string PaymentLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Tiền mặt",
        PaymentMethod.VietQr => "VietQR",
        PaymentMethod.Card => "Thẻ",
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Unknown payment method.")
    };

    /// <summary>
    /// Formats money as <c>15.500.000 ₫</c>. The value stays a <see cref="decimal"/> end to end, and a
    /// fractional part (e.g. 1.000,5) is kept rather than rounded away.
    /// </summary>
    public static string FormatMoney(decimal amount) =>
        amount.ToString("#,##0.##", Vietnamese) + " ₫";

    /// <summary>Local wall-clock time as <c>20/10/2026 15:30</c>; the API sends UTC.</summary>
    public static string FormatDateTime(DateTime value)
    {
        var local = value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime()
            : value.ToLocalTime();
        return local.ToString("dd/MM/yyyy HH:mm", Vietnamese);
    }

    public static string FormatOptionalDateTime(DateTime? value) =>
        value.HasValue ? FormatDateTime(value.Value) : "—";

    public static string CustomerDisplay(string? customerName) =>
        string.IsNullOrWhiteSpace(customerName) ? WalkInCustomer : customerName.Trim();
}
