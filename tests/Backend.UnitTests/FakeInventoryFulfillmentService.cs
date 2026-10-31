using TechStore.Api.Inventory.Services;

namespace TechStore.UnitTests;

public class FakeInventoryFulfillmentService : IInventoryFulfillmentService
{
    // Order in which operations were called ("check", "reserve", "confirm").
    public List<string> CallLog { get; } = [];

    public int CheckCallCount => CallLog.Count(c => c == "check");
    public int ReserveCallCount => CallLog.Count(c => c == "reserve");
    public int ConfirmCallCount => CallLog.Count(c => c == "confirm");

    public IReadOnlyList<FulfillmentLine>? LastCheckedLines { get; private set; }
    public IReadOnlyList<FulfillmentLine>? LastReservedLines { get; private set; }
    public IReadOnlyList<FulfillmentLine>? LastConfirmedLines { get; private set; }
    public DateTime? LastCompletedAt { get; private set; }
    public Guid? LastPerformedBy { get; private set; }

    public List<FulfillmentProblem> CheckProblems { get; } = [];
    public FulfillmentResult ReserveResult { get; set; } = FulfillmentResult.Success();

    // When set, ConfirmSaleAsync fails with it; otherwise it succeeds and returns CostsByOrderItem.
    public FulfillmentProblem? ConfirmConflict { get; set; }
    public Dictionary<Guid, decimal?> CostsByOrderItem { get; } = new();

    public Task<FulfillmentCheckResult> CheckAvailabilityAsync(
        IReadOnlyList<FulfillmentLine> lines,
        CancellationToken cancellationToken = default)
    {
        CallLog.Add("check");
        LastCheckedLines = lines;
        return Task.FromResult(new FulfillmentCheckResult(CheckProblems.ToList()));
    }

    public Task<FulfillmentResult> ReserveAsync(
        IReadOnlyList<FulfillmentLine> lines,
        CancellationToken cancellationToken = default)
    {
        CallLog.Add("reserve");
        LastReservedLines = lines;
        return Task.FromResult(ReserveResult);
    }

    public Task<SaleConfirmationResult> ConfirmSaleAsync(
        IReadOnlyList<FulfillmentLine> lines,
        DateTime completedAt,
        Guid performedByUserId,
        CancellationToken cancellationToken = default)
    {
        CallLog.Add("confirm");
        LastConfirmedLines = lines;
        LastCompletedAt = completedAt;
        LastPerformedBy = performedByUserId;

        return Task.FromResult(ConfirmConflict != null
            ? SaleConfirmationResult.Failed(ConfirmConflict)
            : SaleConfirmationResult.Success(new Dictionary<Guid, decimal?>(CostsByOrderItem)));
    }
}
