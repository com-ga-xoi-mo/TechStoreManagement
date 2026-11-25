using TechStore.Client.Models.Customers;
using TechStore.Shared.DTOs;
using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Orders;

namespace TechStore.Client.ViewModels.Sales;

/// <summary>What the cashier had on the POS page when they left it, so coming back does not lose the sale.</summary>
public sealed record PosDraft(
    Guid ShiftId,
    IReadOnlyList<DraftLineItem> Lines,
    string CashText,
    Guid? CheckoutKey,
    bool AwaitingRetry,
    bool IsClosingShift,
    string CountedCashText,
    string ClosingNote,
    CustomerChoice? Customer = null,
    string DiscountText = "",
    PosQuoteDto? Quote = null,
    Guid? ExchangeRequestId = null,
    Guid? QrPaymentId = null);

public interface IPosDraftStore
{
    void Save(PosDraft draft);

    /// <summary>Returns the saved draft once, and only for the same shift and the same login; otherwise <c>null</c>.</summary>
    PosDraft? Take(Guid shiftId, Guid? exchangeRequestId = null);
}

/// <summary>
/// Keeps the regular sale and each exchange draft separate within a shift. The session generation changes on every
/// login or logout, so another account never sees the previous cashier's cart. Tokens renew without changing it.
/// </summary>
public sealed class PosDraftStore(ISessionStore sessionStore) : IPosDraftStore
{
    private readonly object _gate = new();
    private readonly Dictionary<(Guid Shift, Guid Exchange), PosDraft> _drafts = new();
    private long _generation;

    public void Save(PosDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        lock (_gate)
        {
            ResetForSession();
            _drafts[(draft.ShiftId, draft.ExchangeRequestId ?? Guid.Empty)] = draft;
        }
    }

    public PosDraft? Take(Guid shiftId, Guid? exchangeRequestId = null)
    {
        lock (_gate)
        {
            ResetForSession();
            return _drafts.Remove((shiftId, exchangeRequestId ?? Guid.Empty), out var draft) ? draft : null;
        }
    }

    private void ResetForSession()
    {
        if (_generation == sessionStore.Current.Generation) return;
        _drafts.Clear();
        _generation = sessionStore.Current.Generation;
    }
}
