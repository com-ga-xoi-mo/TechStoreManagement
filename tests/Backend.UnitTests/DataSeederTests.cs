using FluentAssertions;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Common.Data;
using TechStore.Api.Common.Storage;
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
        var seeder = new DataSeeder(repository, unitOfWork, new FakeObjectStorage());

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
        var seeder = new DataSeeder(repository, unitOfWork, new FakeObjectStorage());

        // Act
        var action = () => seeder.SeedAsync();

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("natural key conflict");
        unitOfWork.BeginTransactionCount.Should().Be(1);
        unitOfWork.SaveChangesCount.Should().Be(0);
        unitOfWork.CommitCount.Should().Be(0);
        unitOfWork.RollbackCount.Should().Be(1);
    }

    [Theory]
    [InlineData("asset")]
    [InlineData("storage")]
    [InlineData("save")]
    [InlineData("commit")]
    public async Task SeedAsync_WhenImageOrTransactionFails_CompensatesOnlyConfirmedRollback(string failure)
    {
        var first = DemoSeedManifest.Products[0];
        var second = DemoSeedManifest.Products[1];
        if (failure == "asset") second = second with { CategorySlug = "missing-resource" };
        var repository = new FakeDemoSeedRepository { Missing = [first, second] };
        var storage = new SeedTestStorage { FailSecondPut = failure == "storage" };
        var unitOfWork = new FakeUnitOfWork
        {
            OnRollback = repository.Staged.Clear,
            ExceptionToThrowOnSave = failure == "save" ? new IOException("save failed") : null,
            ExceptionToThrowOnCommit = failure == "commit" ? new IOException("commit acknowledgement lost") : null
        };
        var seeder = new DataSeeder(repository, unitOfWork, storage);

        await Assert.ThrowsAnyAsync<Exception>(() => seeder.SeedAsync());

        Assert.Equal(1, unitOfWork.RollbackCount);
        Assert.Empty(repository.Staged);
        Assert.Equal(failure == "asset" ? 1 : 2, storage.Attempted.Count);
        Assert.Contains("existing/object.png", storage.Objects);
        if (failure == "commit")
        {
            Assert.Empty(storage.Deleted);
            Assert.All(storage.Attempted, key => Assert.Contains(key, storage.Objects));
        }
        else
        {
            Assert.Equal(storage.Attempted, storage.Deleted);
            Assert.Equal("existing/object.png", Assert.Single(storage.Objects));
        }
    }

    private sealed class SeedTestStorage : IObjectStorage
    {
        public HashSet<string> Objects { get; } = ["existing/object.png"];
        public List<string> Attempted { get; } = [];
        public List<string> Deleted { get; } = [];
        public bool FailSecondPut { get; init; }

        public Task<string> PutAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken)
        {
            Attempted.Add(key);
            Objects.Add(key);
            if (FailSecondPut && Attempted.Count == 2)
                throw new ObjectStorageUnavailableException("Simulated failure after writing an object.");
            return Task.FromResult("/uploads/" + key);
        }

        public Task<bool> TryDeleteAsync(string key, CancellationToken cancellationToken)
        {
            Assert.False(cancellationToken.CanBeCanceled);
            Deleted.Add(key);
            return Task.FromResult(Objects.Remove(key));
        }
    }

    private sealed class FakeDemoSeedRepository : IDemoSeedRepository
    {
        public int LockCount { get; private set; }
        public int ReconcileCount { get; private set; }
        public Exception? ExceptionToThrow { get; init; }
        public IReadOnlyList<ProductSeed> Missing { get; init; } = [];
        public List<ProductImage> Staged { get; } = [];

        public Task AcquireSeedLockAsync(CancellationToken cancellationToken)
        {
            LockCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyDictionary<string, Product>> ReconcileAsync(CancellationToken cancellationToken)
        {
            ReconcileCount++;
            if (ExceptionToThrow is not null) throw ExceptionToThrow;
            return Task.FromResult<IReadOnlyDictionary<string, Product>>(
                Missing.ToDictionary(seed => seed.Sku, seed => new Product { Id = DemoSeedManifest.Id(seed.Sku) }));
        }

        public Task<IReadOnlyList<ProductSeed>> FindProductsMissingCoverAsync(
            IReadOnlyDictionary<string, Product> products, CancellationToken cancellationToken) => Task.FromResult(Missing);

        public void StageCover(Product product, ProductImage image) => Staged.Add(image);
    }
}
