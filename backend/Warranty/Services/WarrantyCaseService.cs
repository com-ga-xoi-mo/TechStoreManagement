using TechStore.Api.Common.Data;
using TechStore.Api.Identity.Services;
using TechStore.Api.Inventory.Repositories;
using TechStore.Api.Inventory.Services;
using TechStore.Api.Warranty.Entities;
using TechStore.Api.Warranty.Repositories;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Api.Warranty.Services;

public sealed record WarrantyResult(WarrantyCaseDto? Data, string? Conflict = null)
{
    public bool Created { get; init; }
    public bool IsReplay { get; init; }
    public IReadOnlyList<string>? Reasons { get; init; }
    public string? CurrentStatus { get; init; }
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    public static WarrantyResult Missing() => new(Data: null);
    public static WarrantyResult Rejected(string detail) => new(null, detail);
}

public interface IWarrantyCaseService
{
    Task<WarrantyLookupResult> LookupAsync(string? code, string? orderCode, Guid? customerId, CancellationToken ct);
    Task<WarrantyResult> GetAsync(Guid id, bool includeCost, CancellationToken ct);
    Task<WarrantySearchResult> SearchAsync(string? status, string? query, int page, int pageSize, bool includeCost, CancellationToken ct);
    Task<WarrantyResult> ReceiveAsync(Guid id, ReceiveWarrantyRequest request, AuthenticatedUser actor, CancellationToken ct);
    Task<WarrantyResult> InspectAsync(Guid id, InspectWarrantyRequest request, AuthenticatedUser actor, CancellationToken ct);
    Task<WarrantyResult> MarkRepairedAsync(Guid id, MarkWarrantyRepairedRequest request, AuthenticatedUser actor, CancellationToken ct);
    Task<WarrantyResult> RejectReplacementAsync(Guid id, RejectWarrantyReplacementRequest request, AuthenticatedUser actor, CancellationToken ct);
    Task<WarrantyResult> ReplaceAsync(Guid id, Guid replacementId, AuthenticatedUser actor, CancellationToken ct);
    Task<WarrantyResult> ReturnAsync(Guid id, AuthenticatedUser actor, CancellationToken ct);
}

public sealed partial class WarrantyCaseService(IWarrantyCaseRepository cases, IInventoryAfterSaleRepository sales,
    IInventoryAfterSaleService inventory, IUnitOfWork work, TimeProvider clock) : IWarrantyCaseService
{
    public async Task<WarrantyResult> GetAsync(Guid id, bool includeCost, CancellationToken ct) =>
        await cases.GetDetailAsync(id, ct) is { } details ? new(MapDetails(details, includeCost)) : WarrantyResult.Missing();

    private async Task<WarrantyResult> WriteAsync(Func<Task<WarrantyResult>> operation, CancellationToken ct, bool includeCost = false)
    {
        await work.BeginTransactionAsync(ct);
        try
        {
            var result = await operation();
            if (result.Data == null)
            {
                await work.RollbackAsync(CancellationToken.None);
                return result;
            }
            if (!result.IsReplay)
                await work.SaveChangesAsync(ct);
            var details = await cases.GetDetailAsync(result.Data.Id, ct)
                ?? throw new InvalidOperationException("The warranty case was not found inside its transaction.");
            await work.CommitAsync(ct);
            return result with { Data = MapDetails(details, includeCost) };
        }
        catch
        {
            await work.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    // PostgreSQL timestamptz keeps microseconds. Truncate before storing so the first response equals a replay read from the database.
    private static DateTime ToDatabaseTime(DateTime utc) => utc.AddTicks(-(utc.Ticks % 10));

    private static WarrantyResult Map(WarrantyCase c) => new(new WarrantyCaseDto
    {
        Id = c.Id,
        CaseCode = c.CaseCode,
        OrderId = c.OrderId,
        OrderItemId = c.OrderItemId,
        CustomerId = c.CustomerId,
        DeviceId = c.DeviceId,
        Status = c.Status.ToString(),
        OpenedAt = c.OpenedAt,
        OpenedById = c.OpenedById,
        WarrantyStartAt = c.WarrantyStartAt,
        WarrantyEndAt = c.WarrantyEndAt,
        ClosedAt = c.ClosedAt,
        ClosedById = c.ClosedById,
        ReplacementDeviceId = c.ReplacementDeviceId,
        MovementId = c.MovementId
    });

    private static WarrantyCaseDto MapDetails(WarrantyCaseDetails details, bool includeCost)
    {
        var claim = details.Claim;
        var sale = details.Sale;
        return Map(claim).Data! with
        {
            OrderCode = sale.Order.OrderCode,
            PurchasedAt = sale.Order.CompletedAt,
            CustomerName = sale.Order.Customer?.FullName,
            DeviceCode = details.Device.Imei ?? details.Device.SerialNumber,
            ProductId = sale.ProductId,
            VariantId = sale.VariantId,
            ProductName = sale.ProductName,
            VariantName = sale.VariantName,
            IssueDescription = claim.IssueDescription,
            ContactName = claim.ContactName,
            ContactPhone = claim.ContactPhone,
            InspectionNote = claim.InspectionNote,
            RejectionReason = claim.RejectionReason,
            ReplacementDeviceCode = details.ReplacementDeviceCode,
            ReplacementUnitCost = includeCost ? claim.ReplacementUnitCost : null,
            Events = claim.Events.OrderBy(e => e.CreatedAt)
                .ThenBy(e => e.ToStatus switch
                {
                    WarrantyCaseStatus.Received => 0,
                    WarrantyCaseStatus.PendingReplacement => 1,
                    _ => 2 + (int)e.ToStatus
                })
                .ThenBy(e => e.Id)
                .Select(e => new WarrantyCaseEventDto(e.Id, e.FromStatus?.ToString(), e.ToStatus.ToString(), e.ActorId,
                    details.ActorNames.GetValueOrDefault(e.ActorId, string.Empty), e.Note, e.CreatedAt)).ToArray()
        };
    }

    internal static bool CanViewReplacementCost(RoleType role) =>
        role is RoleType.Admin or RoleType.Manager or RoleType.WarehouseStaff;
}
