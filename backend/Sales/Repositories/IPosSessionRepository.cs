using TechStore.Api.Sales.Entities;

namespace TechStore.Api.Sales.Repositories;

public interface IPosSessionRepository
{
    // Read-only: the user's Open shift (with the cashier), or null.
    Task<PosSession?> GetOpenByCashierAsync(Guid cashierUserId, CancellationToken cancellationToken = default);

    // Read-only shift with its cashier, or null.
    Task<PosSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    // Tracked shift locked with SELECT ... FOR UPDATE. Requires a transaction opened by the calling service.
    Task<PosSession?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    // Read-only page of shifts, newest first, with the cashier loaded.
    Task<(IReadOnlyList<PosSession> Items, int TotalCount)> SearchAsync(
        PosSessionSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    void Add(PosSession session);
}
