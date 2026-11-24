using TechStore.Api.Orders.Entities;

namespace TechStore.Api.Orders.Repositories;

public interface IReturnRepository
{
    Task<ReturnCase?> FindAsync(Guid id, bool forUpdate, CancellationToken ct);
    Task<List<ReturnCase>> HistoryAsync(Guid orderId, CancellationToken ct);
    Task<List<OrderReturn>> SettledAsync(Guid orderId, CancellationToken ct);
    Task<bool> HasBankHoldAsync(Guid orderId, Guid except, CancellationToken ct);
    Task<List<ReturnCaseLine>> InspectionsAsync(CancellationToken ct);
    Task<ReturnCaseLine?> LockInspectionAsync(Guid lineId, CancellationToken ct);
    void Add(ReturnCase request);
    void AddSettlement(OrderReturn settlement);
}
