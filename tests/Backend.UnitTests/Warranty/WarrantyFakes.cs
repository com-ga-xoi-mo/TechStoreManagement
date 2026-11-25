using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Repositories;
using TechStore.Api.Inventory.Services;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Warranty.Entities;
using TechStore.Api.Warranty.Repositories;

namespace TechStore.UnitTests.Warranty;

internal sealed class FakeWarrantyCaseRepository(IInventoryAfterSaleRepository sales) : IWarrantyCaseRepository
{
    public List<WarrantyCase> Cases { get; } = [];
    public List<WarrantyCaseEvent> Events { get; } = [];
    public List<SerialImei> Devices { get; } = [];
    public List<OrderItem> Items { get; } = [];
    public HashSet<Guid> PendingReturns { get; } = [];
    public Dictionary<Guid, int> ReturnedQuantities { get; } = [];
    public Dictionary<Guid, string> ActorNames { get; } = [];
    public Action<string>? OnLock { get; set; }

    public Task<WarrantyCase?> GetAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Cases.SingleOrDefault(c => c.Id == id));

    public async Task<WarrantyCase?> LockAsync(Guid id, CancellationToken ct)
    {
        OnLock?.Invoke("case");
        return await GetAsync(id, ct);
    }

    public async Task<WarrantyCaseDetails?> GetDetailAsync(Guid id, CancellationToken ct)
    {
        var claim = await GetAsync(id, ct);
        if (claim == null)
            return null;

        var device = Devices.Single(x => x.Id == claim.DeviceId);
        var replacement = Devices.SingleOrDefault(x => x.Id == claim.ReplacementDeviceId);
        return new WarrantyCaseDetails(claim, Items.Single(x => x.Id == claim.OrderItemId), device,
            replacement?.Imei ?? replacement?.SerialNumber, ActorNames);
    }

    public Task<SerialImei?> FindDeviceByCodeAsync(string code, CancellationToken ct) =>
        Task.FromResult(Devices.FirstOrDefault(d => d.Imei == code) ?? Devices.SingleOrDefault(d => d.SerialNumber == code));

    public async Task<OrderItem?> FindSaleAsync(SerialImei device, CancellationToken ct)
    {
        var itemId = device.SaleOrderItemId ?? await sales.FindLegacySaleAsync(device.Id, ct);
        return Items.SingleOrDefault(i => i.Id == itemId);
    }

    public Task<int> ReturnedQuantityAsync(Guid itemId, CancellationToken ct) =>
        Task.FromResult(ReturnedQuantities.GetValueOrDefault(itemId));

    public Task<bool> HasOpenReturnRequestAsync(Guid itemId, Guid deviceId, CancellationToken ct) =>
        Task.FromResult(PendingReturns.Contains(itemId) && !Cases.Any(c => c.OrderItemId == itemId
            && c.ReplacementDeviceId == deviceId && c.DeviceId != deviceId && c.Status == WarrantyCaseStatus.Replaced));

    public Task<string?> GetLastCaseCodeAsync(string prefix, CancellationToken ct) =>
        Task.FromResult(Cases.Where(c => c.CaseCode.StartsWith(prefix, StringComparison.Ordinal))
            .OrderByDescending(c => c.CaseCode.Length).ThenByDescending(c => c.CaseCode, StringComparer.Ordinal)
            .Select(c => c.CaseCode).FirstOrDefault());

    public Task<(IReadOnlyList<WarrantyCaseListRow> Items, int TotalCount)> SearchAsync(WarrantySearchCriteria criteria, CancellationToken ct)
    {
        var rows = from claim in Cases
                   join item in Items on claim.OrderItemId equals item.Id
                   join device in Devices on claim.DeviceId equals device.Id
                   where criteria.Status == null || claim.Status == criteria.Status
                   orderby claim.OpenedAt descending, claim.Id descending
                   select new WarrantyCaseListRow(claim, item.ProductName, device.Imei ?? device.SerialNumber,
                       item.Order.OrderCode, item.Order.Customer?.FullName);
        var matches = rows.Where(row => criteria.Query == null
            || new[] { row.Claim.CaseCode, row.Claim.ContactPhone, row.DeviceCode, row.OrderCode }
                .Any(text => text?.Contains(criteria.Query, StringComparison.OrdinalIgnoreCase) == true)).ToArray();
        return Task.FromResult<(IReadOnlyList<WarrantyCaseListRow>, int)>(
            (matches.Skip((criteria.Page - 1) * criteria.PageSize).Take(criteria.PageSize).ToArray(), matches.Length));
    }

    public Task<IReadOnlyList<WarrantyCase>> CasesForDeviceAsync(Guid deviceId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<WarrantyCase>>(Cases.Where(c => c.DeviceId == deviceId).OrderByDescending(c => c.OpenedAt).ToArray());

    public void Add(WarrantyCase claim) => Cases.Add(claim);

    public void AddEvent(WarrantyCaseEvent history)
    {
        Events.Add(history);
        Cases.Single(c => c.Id == history.CaseId).Events.Add(history);
    }
}

internal sealed class FakeWarrantyInventoryService : IInventoryAfterSaleService
{
    public sealed record Call(Guid OperationId, Guid CaseId, Guid OrderId, Guid OrderItemId,
        Guid DeviceId, WarrantyAction Action, Guid? ReplacementId, Guid ActorId);

    public List<Call> Calls { get; } = [];
    public InventoryOperationResult Result { get; set; } = new(true);
    public Action<string>? OnCall { get; set; }

    public Task<InventoryOperationResult> ApplyWarrantyAsync(Guid operationId, Guid caseId, Guid orderId,
        Guid orderItemId, Guid deviceId, WarrantyAction action, Guid? replacementId, Guid actorId, CancellationToken ct = default)
    {
        Calls.Add(new Call(operationId, caseId, orderId, orderItemId, deviceId, action, replacementId, actorId));
        OnCall?.Invoke("inventory");
        return Task.FromResult(Result);
    }

    public Task<InventoryOperationResult> CheckReleaseAsync(Guid orderId, IReadOnlyList<FulfillmentLine> lines, CancellationToken ct = default)
        => throw new NotSupportedException();
    public Task<InventoryOperationResult> CheckReturnAsync(Guid orderId, IReadOnlyList<FulfillmentLine> lines, CancellationToken ct = default)
        => throw new NotSupportedException();
    public Task<InventoryOperationResult> ReleaseAsync(Guid operationId, Guid orderId, IReadOnlyList<FulfillmentLine> lines, CancellationToken ct = default)
        => throw new NotSupportedException();
    public Task<InventoryOperationResult> ReceiveReturnAsync(Guid operationId, Guid orderId, IReadOnlyList<FulfillmentLine> lines, CancellationToken ct = default)
        => throw new NotSupportedException();
    public Task<InventoryOperationResult> InspectAsync(Guid inspectionId, Guid orderReturnId, FulfillmentLine line,
        ReturnDisposition disposition, Guid actorId, CancellationToken ct = default)
        => throw new NotSupportedException();
}
