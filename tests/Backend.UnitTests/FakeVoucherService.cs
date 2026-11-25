using TechStore.Api.Customers.Services;
using TechStore.Api.Orders.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

/// <summary>Records the hold, consume and return of voucher uses a sale flow asks for. Management methods are unused here.</summary>
public class FakeVoucherService : IVoucherService
{
    public List<string> CallLog { get; } = [];
    public VoucherUseResult ReserveResult { get; set; } = new(VoucherUseOutcome.Applied);
    public VoucherUseResult RedeemResult { get; set; } = new(VoucherUseOutcome.Applied);
    public VoucherUseResult ReleaseResult { get; set; } = new(VoucherUseOutcome.Applied);

    public int ReserveCount => CallLog.Count(c => c == "reserve");
    public int RedeemCount => CallLog.Count(c => c == "redeem");
    public int ReleaseCount => CallLog.Count(c => c == "release");

    public Task<VoucherUseResult> ReserveAsync(Order order, CancellationToken cancellationToken = default)
    {
        CallLog.Add("reserve");
        return Task.FromResult(ReserveResult);
    }

    public Task<VoucherUseResult> RedeemAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        CallLog.Add("redeem");
        return Task.FromResult(RedeemResult);
    }

    public Task<VoucherUseResult> ReleaseAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        CallLog.Add("release");
        return Task.FromResult(ReleaseResult);
    }

    public Task<CustomerOperationResult<PagedResultDto<VoucherDto>>> SearchAsync(VoucherSearchRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<CustomerOperationResult<VoucherDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<CustomerOperationResult<VoucherDto>> CreateAsync(CreateVoucherRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<CustomerOperationResult<VoucherDto>> UpdateAsync(Guid id, UpdateVoucherRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<CustomerOperationResult<VoucherDto>> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

public class FakeRefundLoyalty : IRefundLoyaltyService
{
    public List<(Guid OrderId, Guid CustomerId, decimal NetAmount)> Earned { get; } = [];

    public Task StageSaleAsync(Guid orderId, Guid customerId, decimal netAmount, CancellationToken ct)
    {
        Earned.Add((orderId, customerId, netAmount));
        return Task.CompletedTask;
    }

    public Task StageReversalAsync(Guid refundId, Guid orderId, Guid customerId, decimal remainingNetAmount, CancellationToken ct) =>
        Task.CompletedTask;
}
