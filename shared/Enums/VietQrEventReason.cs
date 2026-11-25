namespace TechStore.Shared.Enums;

/// <summary>Why a bank notification was kept as an event instead of, or besides, completing a sale.</summary>
public enum VietQrEventReason
{
    Applied,
    LateAfterExpiry,
    LateAfterCancel,
    LateAfterShiftClose,
    AmountMismatch,
    AccountMismatch,
    Unmatched,
    /// <summary>The bank reference was already used, or the order was already paid by another transfer.</summary>
    DuplicateReference,
    /// <summary>The transfer matched an open request but completing the sale was refused (stock, voucher).</summary>
    SettlementFailed
}

public enum VietQrEventStatus
{
    /// <summary>The money completed a sale; nothing to reconcile.</summary>
    Applied,
    NeedsReconciliation,
    Resolved
}
