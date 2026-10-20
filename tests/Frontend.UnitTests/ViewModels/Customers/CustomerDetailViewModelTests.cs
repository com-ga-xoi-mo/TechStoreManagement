using TechStore.Client.Models.Customers;
using TechStore.Client.Repositories.Customers;
using TechStore.Client.Services.Api;
using TechStore.Client.ViewModels.Customers;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.ViewModels.Customers;

public sealed class CustomerDetailViewModelTests
{
    private static readonly Guid Id = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task LoadAsync_MapsProfileAndAllReadOnlyHistoryTabs()
    {
        var repository = new FakeCustomers();
        var vm = new CustomerDetailViewModel(repository);

        await vm.LoadAsync(Id);

        Assert.Equal("Vy", vm.Profile!.FullName);
        Assert.Equal(1, Assert.Single(vm.PointHistory!.Items).PointsChange);
        Assert.Equal("ORD-1", Assert.Single(vm.OrderHistory!.Items).OrderCode);
        Assert.Equal("123456789012345", Assert.Single(vm.PurchasedItems!.Items).Imei);
        Assert.Equal(0, repository.WriteCount);
    }

    [Fact]
    public async Task NextAndPreviousHistoryCommands_RequestTheCorrectPageAndPreserveEmptyResults()
    {
        var repository = new FakeCustomers();
        var vm = new CustomerDetailViewModel(repository);
        await vm.LoadAsync(Id);

        await vm.NextPointsPageCommand.ExecuteAsync(null);
        await vm.NextOrdersPageCommand.ExecuteAsync(null);
        await vm.NextDevicesPageCommand.ExecuteAsync(null);

        Assert.Equal(2, repository.PointPages.Last());
        Assert.Equal(2, repository.OrderPages.Last());
        Assert.Equal(2, repository.DevicePages.Last());
        Assert.Empty(vm.PurchasedItems!.Items);
        await vm.PreviousDevicesPageCommand.ExecuteAsync(null);
        Assert.Equal(1, repository.DevicePages.Last());
    }

    [Fact]
    public async Task LoadAsync_UsesSafeFailureMessageAndRetryCanRecover()
    {
        var repository = new FakeCustomers { Fail = true };
        var vm = new CustomerDetailViewModel(repository);
        await vm.LoadAsync(Id);
        Assert.Contains("Không thể tải", vm.ErrorMessage);
        Assert.DoesNotContain("secret", vm.ErrorMessage);

        repository.Fail = false;
        await vm.LoadAsync(Id);
        Assert.Equal("Vy", vm.Profile!.FullName);
    }

    [Fact]
    public async Task ActivityActions_AreStateSpecificAndReactivationPreservesProfile()
    {
        var inactive = Profile() with { IsActive = false, LoyaltyPoints = 25 };
        var repository = new FakeCustomers { ProfileValue = inactive };
        var vm = new CustomerDetailViewModel(repository);
        await vm.LoadAsync(Id);

        Assert.True(vm.CanActivate);
        Assert.False(vm.CanDeactivate);

        await vm.ActivateCustomerCommand.ExecuteAsync(null);

        Assert.Equal(1, repository.ActivateCount);
        Assert.True(vm.Profile!.IsActive);
        Assert.Equal(25, vm.Profile.LoyaltyPoints);
        Assert.False(vm.CanActivate);
        Assert.True(vm.CanDeactivate);
        Assert.True(vm.ActivityChanged);
        Assert.Equal(string.Empty, vm.ActivityErrorMessage);
    }

    [Fact]
    public async Task ReactivationFailure_KeepsInactiveProfileAndShowsSafeError()
    {
        var repository = new FakeCustomers
        {
            ProfileValue = Profile() with { IsActive = false },
            ActivateException = new ApiException(ApiErrorKind.Network, "private transport detail")
        };
        var vm = new CustomerDetailViewModel(repository);
        await vm.LoadAsync(Id);

        await vm.ActivateCustomerCommand.ExecuteAsync(null);

        Assert.False(vm.Profile!.IsActive);
        Assert.False(vm.ActivityChanged);
        Assert.Contains("kết nối", vm.ActivityErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private", vm.ActivityErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeCustomers : ICustomerRepository
    {
        public bool Fail { get; set; }
        public int WriteCount { get; private set; }
        public int ActivateCount { get; private set; }
        public Exception? ActivateException { get; init; }
        public CustomerProfile ProfileValue { get; set; } = Profile();
        public List<int> PointPages { get; } = [];
        public List<int> OrderPages { get; } = [];
        public List<int> DevicePages { get; } = [];
        public Task<CustomerPage<CustomerSummary>> SearchAsync(CustomerQuery query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CustomerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Read<CustomerProfile?>(() => ProfileValue);
        public Task<CustomerPage<CustomerLoyaltyTransaction>> GetLoyaltyHistoryAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default) { PointPages.Add(page); return Read(() => Page([new CustomerLoyaltyTransaction(Guid.NewGuid(), 1, 1, "Completed order", null, DateTime.UtcNow)], page)); }
        public Task<CustomerPage<CustomerOrder>> GetOrderHistoryAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default) { OrderPages.Add(page); return Read(() => Page([new CustomerOrder(Guid.NewGuid(), "ORD-1", "Completed", "Cash", 10, 1, DateTime.UtcNow, null, DateTime.UtcNow)], page)); }
        public Task<CustomerPage<CustomerPurchasedItem>> GetPurchasedItemsAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default) { DevicePages.Add(page); return Read(() => Page(page == 2 ? Array.Empty<CustomerPurchasedItem>() : [new CustomerPurchasedItem(Guid.NewGuid(), Guid.NewGuid(), "ORD-1", DateTime.UtcNow, Guid.NewGuid(), null, "Phone", null, "SKU", null, null, "123456789012345", 1, 10)], page)); }
        public Task<CustomerProfile> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default) { WriteCount++; throw new NotImplementedException(); }
        public Task<CustomerProfile> UpdateAsync(Guid customerId, UpdateCustomerRequest request, CancellationToken cancellationToken = default) { WriteCount++; throw new NotImplementedException(); }
        public Task DeactivateAsync(Guid customerId, CancellationToken cancellationToken = default) { WriteCount++; throw new NotImplementedException(); }
        public Task ActivateAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteCount++;
            ActivateCount++;
            if (ActivateException is not null) return Task.FromException(ActivateException);
            ProfileValue = ProfileValue with { IsActive = true };
            return Task.CompletedTask;
        }
        private Task<T> Read<T>(Func<T> result) => Fail ? Task.FromException<T>(new ApiException(ApiErrorKind.Network, "secret server detail")) : Task.FromResult(result());
        private static CustomerPage<T> Page<T>(IReadOnlyList<T> items, int page) => new(items, page, 10, 11, 2);
    }

    private static CustomerProfile Profile() => new(Id, "Vy", "0901", null, null, null, 10, "Silver", true, DateTime.UtcNow, null, 1, 10, DateTime.UtcNow);
}
