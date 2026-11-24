using TechStore.Api.Identity.Services;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Orders.Repositories;
using TechStore.Shared.Enums;

namespace TechStore.Api.Orders.Services;

// Called inside checkout after the shift lock. An ordinary checkout key has no return case.
public interface IReturnExchangeService
{
    Task<string?> LinkCheckoutAsync(Guid checkoutKey, Order replacement, AuthenticatedUser actor, CancellationToken ct);
}

public sealed class ReturnExchangeService(IReturnRepository returns, IOrderRepository orders) : IReturnExchangeService
{
    public async Task<string?> LinkCheckoutAsync(Guid checkoutKey, Order replacement, AuthenticatedUser actor, CancellationToken ct)
    {
        var request = await returns.FindAsync(checkoutKey, true, ct);
        if (request == null) return null;
        var original = await orders.GetWithItemsAsync(request.OrderId, ct);
        if (original == null || actor.Role == RoleType.SalesStaff && original.CashierUserId != actor.Id ||
            original.CustomerId != replacement.CustomerId || request.Status != ReturnStatus.Refunded ||
            request.ReplacementOrderId.HasValue && request.ReplacementOrderId != replacement.Id)
            return "Phiếu không thể dùng cho giao dịch đổi hàng này.";
        request.ReplacementOrderId = replacement.Id;
        return null;
    }
}
