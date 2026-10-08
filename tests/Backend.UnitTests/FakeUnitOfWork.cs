using TechStore.Api.Common.Data;

namespace TechStore.UnitTests;

public class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCount { get; private set; }
    public int BeginTransactionCount { get; private set; }
    public int CommitCount { get; private set; }
    public int RollbackCount { get; private set; }

    public Exception? ExceptionToThrowOnSave { get; set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCount++;
        if (ExceptionToThrowOnSave != null)
        {
            throw ExceptionToThrowOnSave;
        }
        return Task.FromResult(1);
    }

    public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        BeginTransactionCount++;
        return Task.CompletedTask;
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        CommitCount++;
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        RollbackCount++;
        return Task.CompletedTask;
    }
}
