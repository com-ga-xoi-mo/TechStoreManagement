using TechStore.Client.Controls;
using TechStore.Client.Controls.Presentation;
using TechStore.Client.Models.Orders;
using TechStore.Shared.Enums;

namespace TechStore.Client.ViewModels.Orders;

/// <summary>One entry of the status filter. A <c>null</c> value means "all statuses".</summary>
public sealed record OrderStatusFilter(OrderStatus? Value, string Label)
{
    public static IReadOnlyList<OrderStatusFilter> All { get; } =
    [
        new(null, "Tất cả trạng thái"),
        .. Enum.GetValues<OrderStatus>().Select(status => new OrderStatusFilter(status, OrderPresentation.StatusLabel(status)))
    ];
}

public sealed record OrderSortOption(string Value, string Label)
{
    public static IReadOnlyList<OrderSortOption> All { get; } =
    [
        new("createdAt:desc", "Mới nhất"),
        new("createdAt:asc", "Cũ nhất"),
        new("totalAmount:desc", "Tổng tiền giảm dần"),
        new("totalAmount:asc", "Tổng tiền tăng dần")
    ];
}

/// <summary>A status change offered on one row; it carries the order so a single command can handle every button.</summary>
public sealed record OrderRowAction(OrderSummary Order, OrderStatusAction Action)
{
    public string Label => Action.Label;

    public bool IsDestructive => Action.IsDestructive;
}

/// <summary>Display-ready order row. All text is computed once so the view only binds plain values.</summary>
public sealed record OrderRow(
    OrderSummary Order,
    string Code,
    string Customer,
    string StatusLabel,
    StatusTone StatusTone,
    string Payment,
    string Total,
    string ItemCount,
    string CreatedAt,
    IReadOnlyList<OrderRowAction> Actions)
{
    public bool HasActions => Actions.Count > 0;

    /// <summary>Second line under the customer: how it was paid and how many lines it has.</summary>
    public string PaymentSummary => $"{Payment} · {ItemCount}";

    // The three possible buttons are fixed so the row template can give each its own style.
    public OrderRowAction? PayAction => Actions.FirstOrDefault(a => a.Action.Target == OrderStatus.Processing);
    public OrderRowAction? CompleteAction => Actions.FirstOrDefault(a => a.Action.Target == OrderStatus.Completed);
    public OrderRowAction? CancelAction => Actions.FirstOrDefault(a => a.Action.Target == OrderStatus.Cancelled);
    public bool HasPayAction => PayAction is not null;
    public bool HasCompleteAction => CompleteAction is not null;
    public bool HasCancelAction => CancelAction is not null;

    public static OrderRow From(OrderSummary order) => new(
        order,
        order.OrderCode,
        OrderPresentation.CustomerDisplay(order.CustomerName),
        OrderPresentation.StatusLabel(order.Status),
        OrderPresentation.StatusTone(order.Status),
        OrderPresentation.PaymentLabel(order.PaymentMethod),
        OrderPresentation.FormatMoney(order.TotalAmount),
        order.ItemCount == 1 ? "1 dòng" : $"{order.ItemCount} dòng",
        OrderPresentation.FormatDateTime(order.CreatedAt),
        OrderStatusActions.For(order.Status).Select(action => new OrderRowAction(order, action)).ToArray());
}
