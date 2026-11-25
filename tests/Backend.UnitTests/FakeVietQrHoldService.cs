using TechStore.Api.Identity.Services;
using TechStore.Api.Sales.Entities;
using TechStore.Api.Sales.Services;
using TechStore.Shared.Enums;

namespace TechStore.UnitTests;

/// <summary>Stands in for the release logic: records the calls and, like the real one, moves an overdue request to Expired.</summary>
public class FakeVietQrHoldService(FakeVietQrHoldRepository requests) : IVietQrHoldService
{
    public List<Guid> Expired { get; } = [];

    public Task<PosResult<ClosedVietQrHold>> CloseAsync(Guid id, VietQrCloseReason reason, AuthenticatedUser actor, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<PosResult<ClosedVietQrHold>> ExpireDueAsync(Guid id, CancellationToken ct)
    {
        Expired.Add(id);
        var qr = requests.Requests.Single(q => q.Id == id);
        if (qr.Status == VietQrStatus.Pending)
        {
            qr.Status = VietQrStatus.Expired;
            qr.Order.Status = OrderStatus.Cancelled;
        }

        return Task.FromResult(PosResult<ClosedVietQrHold>.Success(new(qr.Id, qr.OrderId, qr.Status.ToString(), qr.Order.Status.ToString())));
    }
}
