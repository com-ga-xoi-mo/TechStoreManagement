using FluentAssertions;
using TechStore.Api.Common.Data;
using Xunit;

namespace TechStore.UnitTests;

public class DataSeederTests
{
    [Fact]
    public async Task SeedAsync_Should_LockReconcileSaveAndCommitWithinOneUnitOfWork()
    {
        // Arrange
        var repository = new FakeDemoSeedRepository();
        var unitOfWork = new FakeUnitOfWork();
        var seeder = new DataSeeder(repository, unitOfWork);

        // Act
        await seeder.SeedAsync();

        // Assert
        repository.LockCount.Should().Be(1);
        repository.ReconcileCount.Should().Be(1);
        unitOfWork.BeginTransactionCount.Should().Be(1);
        unitOfWork.SaveChangesCount.Should().Be(1);
        unitOfWork.CommitCount.Should().Be(1);
        unitOfWork.RollbackCount.Should().Be(0);
    }

    [Fact]
    public async Task SeedAsync_WhenNaturalKeyConflictOccurs_ShouldRollback()
    {
        // Arrange
        var repository = new FakeDemoSeedRepository { ExceptionToThrow = new InvalidOperationException("natural key conflict") };
        var unitOfWork = new FakeUnitOfWork();
        var seeder = new DataSeeder(repository, unitOfWork);

        // Act
        var action = () => seeder.SeedAsync();

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("natural key conflict");
        unitOfWork.BeginTransactionCount.Should().Be(1);
        unitOfWork.SaveChangesCount.Should().Be(0);
        unitOfWork.CommitCount.Should().Be(0);
        unitOfWork.RollbackCount.Should().Be(1);
    }

    private sealed class FakeDemoSeedRepository : IDemoSeedRepository
    {
        public int LockCount { get; private set; }
        public int ReconcileCount { get; private set; }
        public Exception? ExceptionToThrow { get; init; }

        public Task AcquireSeedLockAsync(CancellationToken cancellationToken)
        {
            LockCount++;
            return Task.CompletedTask;
        }

        public Task ReconcileAsync(CancellationToken cancellationToken)
        {
            ReconcileCount++;
            if (ExceptionToThrow is not null) throw ExceptionToThrow;
            return Task.CompletedTask;
        }
    }
}
