using TechStore.Api.Common.Data;
using System.Data;

namespace TechStore.UnitTests;

public class FakeUnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _dbContext;

    public FakeUnitOfWork(AppDbContext? dbContext = null)
    {
        _dbContext = dbContext!;
    }

    public int SaveChangesCount { get; private set; }
    public int BeginTransactionCount { get; private set; }
    public int CommitCount { get; private set; }
    public int RollbackCount { get; private set; }
    public IsolationLevel? LastIsolationLevel { get; private set; }

    public Exception? ExceptionToThrowOnSave { get; set; }

    // Exceptions thrown by successive saves, one per call, before ExceptionToThrowOnSave applies.
    public Queue<Exception> SaveExceptionQueue { get; } = new();

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCount++;
        if (SaveExceptionQueue.Count > 0)
        {
            throw SaveExceptionQueue.Dequeue();
        }
        if (ExceptionToThrowOnSave != null)
        {
            throw ExceptionToThrowOnSave;
        }

        if (_dbContext != null)
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return 1;
    }

    public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        BeginTransactionCount++;
        return Task.CompletedTask;
    }

    public Task BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
    {
        BeginTransactionCount++;
        LastIsolationLevel = isolationLevel;
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
