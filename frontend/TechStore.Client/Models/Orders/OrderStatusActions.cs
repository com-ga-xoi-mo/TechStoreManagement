using TechStore.Shared.Enums;

namespace TechStore.Client.Models.Orders;

/// <summary>A status change offered on an order row.</summary>
public sealed record OrderStatusAction(OrderStatus Target, string Label, bool IsDestructive = false);

/// <summary>
/// Which status changes to offer for a given status. It only hides buttons that cannot work;
/// the server stays the authority on whether a transition is valid.
/// </summary>
public static class OrderStatusActions
{
    private static readonly OrderStatusAction[] None = [];

    private static readonly OrderStatusAction[] FromPending =
    [
        new(OrderStatus.Processing, "Xác nhận thanh toán"),
        new(OrderStatus.Completed, "Hoàn tất"),
        new(OrderStatus.Cancelled, "Hủy đơn", IsDestructive: true)
    ];

    private static readonly OrderStatusAction[] FromProcessing =
    [
        new(OrderStatus.Completed, "Hoàn tất")
    ];

    public static IReadOnlyList<OrderStatusAction> For(OrderStatus status) => status switch
    {
        OrderStatus.Pending => FromPending,
        OrderStatus.Processing => FromProcessing,
        _ => None
    };
}
