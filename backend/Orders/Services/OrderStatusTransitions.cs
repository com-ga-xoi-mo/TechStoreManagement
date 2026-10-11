using TechStore.Shared.Enums;

namespace TechStore.Api.Orders.Services;

public static class OrderStatusTransitions
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Allowed = new()
    {
        [OrderStatus.Pending] = [OrderStatus.Processing, OrderStatus.Completed, OrderStatus.Cancelled],
        [OrderStatus.Processing] = [OrderStatus.Completed]
    };

    // Statuses a client may request through PATCH /api/orders/{id}/status.
    private static readonly OrderStatus[] RequestableTargets =
        [OrderStatus.Processing, OrderStatus.Completed, OrderStatus.Cancelled];

    public static bool IsAllowed(OrderStatus from, OrderStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static bool TryParseTarget(string? text, out OrderStatus target)
    {
        if (OrderEnumParser.TryParse<OrderStatus>(text, out target) && RequestableTargets.Contains(target))
        {
            return true;
        }

        target = default;
        return false;
    }
}
