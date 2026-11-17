using TechStore.Client.Models.Orders;
using TechStore.Client.Models.Sales;
using TechStore.Shared.Requests;

namespace TechStore.Client.Repositories.Sales;

public interface IPosRepository
{
    /// <summary>GET /api/pos/shifts/current. Returns <c>null</c> when the user has no open shift (HTTP 404).</summary>
    Task<PosShift?> GetCurrentShiftAsync(CancellationToken cancellationToken = default);

    /// <summary>POST /api/pos/shifts. 400 and 409 reach the caller as <c>ApiException</c>.</summary>
    Task<PosShift> OpenShiftAsync(decimal openingBalance, CancellationToken cancellationToken = default);

    /// <summary>POST /api/pos/shifts/{id}/close. 400, 404 and 409 reach the caller as <c>ApiException</c>.</summary>
    Task<PosShift> CloseShiftAsync(Guid shiftId, decimal countedCash, string? note, CancellationToken cancellationToken = default);

    /// <summary>POST /api/pos/checkout. Sending the same <c>CheckoutKey</c> again returns the stored order.</summary>
    Task<OrderDetail> CheckoutAsync(PosCheckoutRequest request, CancellationToken cancellationToken = default);
}
