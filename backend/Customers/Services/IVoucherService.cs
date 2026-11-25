using TechStore.Api.Orders.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Customers.Services;

public enum VoucherUseOutcome
{
    /// <summary>The use was held, consumed or returned and the change is staged.</summary>
    Applied,
    /// <summary>The use was already in that state; nothing changed.</summary>
    Unchanged,
    /// <summary>The voucher or the order's use does not exist.</summary>
    NotFound,
    /// <summary>The voucher cannot be used now (see the reason) or the step is not allowed from the current state.</summary>
    Refused
}

public sealed record VoucherUseResult(VoucherUseOutcome Outcome, string? Reason = null);

public interface IVoucherService
{
    Task<CustomerOperationResult<PagedResultDto<VoucherDto>>> SearchAsync(VoucherSearchRequest request, CancellationToken cancellationToken = default);
    Task<CustomerOperationResult<VoucherDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CustomerOperationResult<VoucherDto>> CreateAsync(CreateVoucherRequest request, CancellationToken cancellationToken = default);
    Task<CustomerOperationResult<VoucherDto>> UpdateAsync(Guid id, UpdateVoucherRequest request, CancellationToken cancellationToken = default);
    Task<CustomerOperationResult<VoucherDto>> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);

    // The three methods below belong to a sale in progress. They only stage changes in the caller's unit of work:
    // the caller opens the transaction (the voucher row is locked with FOR UPDATE), holds the order lock so one order is
    // never handled twice at once, and saves once.

    /// <summary>Holds one use of <c>order.VoucherId</c> when payment starts. Re-checks eligibility against <c>order.Subtotal</c>.</summary>
    Task<VoucherUseResult> ReserveAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>Consumes the held use when money is received.</summary>
    Task<VoucherUseResult> RedeemAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Returns the held use when the sale is cancelled or the hold expires before payment. A consumed use is never returned.</summary>
    Task<VoucherUseResult> ReleaseAsync(Guid orderId, CancellationToken cancellationToken = default);
}
