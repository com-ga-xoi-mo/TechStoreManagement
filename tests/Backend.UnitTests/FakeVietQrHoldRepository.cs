using TechStore.Api.Orders.Entities;
using TechStore.Api.Sales.Entities;
using TechStore.Api.Sales.Repositories;
using TechStore.Shared.Enums;

namespace TechStore.UnitTests;

/// <summary>In-memory QR requests and bank events. Locks are no-ops; the real ones are exercised against PostgreSQL.</summary>
public class FakeVietQrHoldRepository(FakeOrderRepository? orders = null) : IVietQrHoldRepository
{
    public List<VietQrTransaction> Requests { get; } = [];
    public List<VietQrBankEvent> Events { get; } = [];

    public VietQrTransaction AddRequest(Order order, Guid shiftId, VietQrStatus status = VietQrStatus.Pending, DateTime? expiresAt = null)
    {
        var qr = new VietQrTransaction
        {
            OrderId = order.Id,
            Order = order,
            PosSessionId = shiftId,
            TransactionCode = VietQrCodes.Next(),
            BankBin = "970422",
            BankAccountNumber = "0123456789",
            Amount = order.TotalAmount,
            QrContent = "000201",
            Status = status,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddMinutes(15)
        };
        Requests.Add(qr);
        return qr;
    }

    public Task<VietQrHoldOwner?> FindOwnerAsync(Guid qrId, CancellationToken ct) =>
        Task.FromResult(Requests.Where(q => q.Id == qrId).Select(q => new VietQrHoldOwner(q.OrderId, q.PosSessionId)).FirstOrDefault());

    public Task<IReadOnlyList<VietQrTransaction>> LockPaymentsAsync(Guid orderId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<VietQrTransaction>>(Requests.Where(q => q.OrderId == orderId).ToList());

    public Task<VietQrTransaction?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Requests.FirstOrDefault(q => q.Id == id));

    public Task<VietQrTransaction?> GetLatestByOrderAsync(Guid orderId, CancellationToken ct) =>
        Task.FromResult(Requests.Where(q => q.OrderId == orderId).OrderByDescending(q => q.CreatedAt).FirstOrDefault());

    public Task<VietQrTransaction?> FindByTransferCodeAsync(string transactionCode, CancellationToken ct) =>
        Task.FromResult(Requests.FirstOrDefault(q => q.TransactionCode == transactionCode));

    public Task<(IReadOnlyList<VietQrTransaction> Items, int TotalCount)> SearchAsync(VietQrSearchCriteria criteria, CancellationToken ct)
    {
        var all = Requests
            .Where(q => criteria.Status is null || q.Status == criteria.Status)
            .Where(q => criteria.PosSessionId is null || q.PosSessionId == criteria.PosSessionId)
            .Where(q => criteria.CashierUserId is null || q.Order.CashierUserId == criteria.CashierUserId)
            .OrderByDescending(q => q.CreatedAt)
            .ToList();
        var page = all.Skip((criteria.Page - 1) * criteria.PageSize).Take(criteria.PageSize).ToList();
        return Task.FromResult<(IReadOnlyList<VietQrTransaction>, int)>((page, all.Count));
    }

    public Task<IReadOnlyList<VietQrTransaction>> ListPendingByShiftAsync(Guid shiftId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<VietQrTransaction>>(
            Requests.Where(q => q.PosSessionId == shiftId && q.Status == VietQrStatus.Pending).ToList());

    public Task<IReadOnlyList<Guid>> ListDueIdsAsync(DateTime nowUtc, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Guid>>(
            Requests.Where(q => q.Status == VietQrStatus.Pending && q.ExpiresAt <= nowUtc).Select(q => q.Id).Take(take).ToList());

    public Task<bool> ReferenceUsedAsync(string provider, string reference, Guid exceptQrId, CancellationToken ct) =>
        Task.FromResult(Requests.Any(q => q.Provider == provider && q.ProviderTransactionId == reference && q.Id != exceptQrId));

    public void Add(VietQrTransaction transaction)
    {
        // The real repository loads the order with the request; the fake looks it up the same way.
        if (transaction.Order == null && orders?.Orders.TryGetValue(transaction.OrderId, out var order) == true)
            transaction.Order = order;
        Requests.Add(transaction);
    }

    public Task<VietQrBankEvent?> FindEventAsync(string provider, string providerTransactionId, CancellationToken ct) =>
        Task.FromResult(Events.FirstOrDefault(e => e.Provider == provider && e.ProviderTransactionId == providerTransactionId));

    public Task<VietQrBankEvent?> GetEventForUpdateAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Events.FirstOrDefault(e => e.Id == id));

    public Task<(IReadOnlyList<VietQrBankEvent> Items, int TotalCount)> SearchEventsAsync(VietQrEventSearchCriteria criteria, CancellationToken ct)
    {
        var all = Events.Where(e => criteria.Status is null || e.Status == criteria.Status).OrderByDescending(e => e.ReceivedAt).ToList();
        var page = all.Skip((criteria.Page - 1) * criteria.PageSize).Take(criteria.PageSize).ToList();
        return Task.FromResult<(IReadOnlyList<VietQrBankEvent>, int)>((page, all.Count));
    }

    public void Add(VietQrBankEvent bankEvent) => Events.Add(bankEvent);
}

internal static class VietQrCodes
{
    private static int _counter;

    public static string Next() => "TS" + Interlocked.Increment(ref _counter).ToString("D10").Replace('0', 'A').Replace('1', 'B');
}
