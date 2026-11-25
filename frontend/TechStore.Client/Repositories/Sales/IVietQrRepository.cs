using TechStore.Client.Models.Sales;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Client.Repositories.Sales;

public interface IVietQrRepository
{
    /// <summary>POST /api/pos/vietqr. The same <c>CheckoutKey</c> returns the stored request (HTTP 200).</summary>
    Task<VietQrPayment> CreateAsync(CreateVietQrRequest request, CancellationToken cancellationToken = default);

    /// <summary>GET /api/pos/vietqr/{id}. Reading an overdue request expires it on the server.</summary>
    Task<VietQrPayment> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>GET /api/pos/vietqr. Salespeople only see their own; the list carries no QR image.</summary>
    Task<IReadOnlyList<VietQrPayment>> ListAsync(VietQrStatus? status, Guid? posSessionId, CancellationToken cancellationToken = default);

    /// <summary>POST /api/pos/vietqr/{id}/cancel. 409 reaches the caller as <c>ApiException</c> (already paid or ended).</summary>
    Task CancelAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>POST /api/pos/vietqr/{id}/confirm (Admin, Manager): the bank reference and the amount on the statement.</summary>
    Task<VietQrPayment> ConfirmAsync(Guid id, string referenceCode, decimal receivedAmount, CancellationToken cancellationToken = default);

    /// <summary>GET /api/payments/vietqr/events (Admin, Manager).</summary>
    Task<IReadOnlyList<VietQrBankEvent>> ListEventsAsync(VietQrEventStatus? status, CancellationToken cancellationToken = default);

    /// <summary>POST /api/payments/vietqr/events/{id}/resolve (Admin, Manager).</summary>
    Task<VietQrBankEvent> ResolveEventAsync(Guid id, string note, CancellationToken cancellationToken = default);
}
