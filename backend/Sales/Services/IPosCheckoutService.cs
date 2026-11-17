using TechStore.Api.Identity.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Sales.Services;

// Replayed is true when the checkout key was already stored: the existing order is returned and nothing is applied again.
public record PosCheckoutOutcome(OrderDto Order, bool Replayed);

// Cash checkout: order, payment, stock, warranty and shift cash in one transaction.
public interface IPosCheckoutService
{
    Task<PosResult<PosCheckoutOutcome>> CheckoutAsync(
        PosCheckoutRequest request,
        AuthenticatedUser actor,
        CancellationToken cancellationToken = default);
}
