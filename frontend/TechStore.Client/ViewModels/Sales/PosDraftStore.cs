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
    PosQuoteDto? Quote = null);

public interface IPosDraftStore
{
    void Save(PosDraft draft);

    /// <summary>Returns the saved draft once, and only for the same shift and the same login; otherwise <c>null</c>.</summary>
    PosDraft? Take(Guid shiftId);
}

/// <summary>
/// Keeps one draft in memory for the running app. It is tied to the session generation, which changes on every
/// login or logout, so another account never sees the previous cashier's cart. Tokens renew without changing it.
/// </summary>
public sealed class PosDraftStore(ISessionStore sessionStore) : IPosDraftStore
{
    private readonly object _gate = new();
    private PosDraft? _draft;
    private long _generation;

    public void Save(PosDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        lock (_gate)
        {
            _draft = draft;
            _generation = sessionStore.Current.Generation;
        }
    }

    public PosDraft? Take(Guid shiftId)
    {
        lock (_gate)
        {
            var draft = _draft;
            _draft = null;
            return draft is not null && draft.ShiftId == shiftId && _generation == sessionStore.Current.Generation
                ? draft
                : null;
        }
    }
}
