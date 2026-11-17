using TechStore.Api.Sales.Entities;
using TechStore.Api.Sales.Repositories;
using TechStore.Shared.Enums;

namespace TechStore.UnitTests;

public class FakePosSessionRepository : IPosSessionRepository
{
    public Dictionary<Guid, PosSession> Sessions { get; } = new();
    public List<PosSession> AddedSessions { get; } = [];
    public int GetForUpdateCallCount { get; private set; }

    public PosSession AddSession(Guid cashierUserId, decimal opening = 1_000_000m, PosSessionStatus status = PosSessionStatus.Open)
    {
        var session = new PosSession { CashierUserId = cashierUserId, OpeningBalance = opening, Status = status };
        Sessions[session.Id] = session;
        return session;
    }

    public Task<PosSession?> GetOpenByCashierAsync(Guid cashierUserId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Sessions.Values.FirstOrDefault(s => s.CashierUserId == cashierUserId && s.Status == PosSessionStatus.Open));

    public Task<PosSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Sessions.TryGetValue(id, out var session);
        return Task.FromResult(session);
    }

    public Task<PosSession?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetForUpdateCallCount++;
        Sessions.TryGetValue(id, out var session);
        return Task.FromResult(session);
    }

    public Task<(IReadOnlyList<PosSession> Items, int TotalCount)> SearchAsync(
        PosSessionSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var all = Sessions.Values
            .Where(s => criteria.Status is null || s.Status == criteria.Status)
            .Where(s => criteria.CashierUserId is null || s.CashierUserId == criteria.CashierUserId)
            .OrderByDescending(s => s.OpenedAt)
            .ToList();
        var page = all.Skip((criteria.Page - 1) * criteria.PageSize).Take(criteria.PageSize).ToList();
        return Task.FromResult<(IReadOnlyList<PosSession> Items, int TotalCount)>((page, all.Count));
    }

    public void Add(PosSession session)
    {
        AddedSessions.Add(session);
        Sessions[session.Id] = session;
    }
}
