using TechStore.Client.Models.Customers;
using TechStore.Client.Models.Loyalty;
using TechStore.Client.Repositories.Customers;
using TechStore.Client.Repositories.Loyalty;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Customers;
using TechStore.Client.ViewModels.Pages;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.ViewModels.Customers;

public sealed class CustomerDetailLoyaltySummaryTests
{
    private static readonly Guid Id = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task LoadAsync_SilverCustomer_ShowsDiscountNextTierAndProgress()
    {
        // Arrange
        var loyalty = new FakeLoyalty { Summary = new(Id, 700, "Silver", 2, "Gold", 800) };
        var viewModel = new CustomerDetailViewModel(new FakeCustomers(), loyalty: loyalty);

        // Act
        await viewModel.LoadAsync(Id);

        // Assert
        Assert.True(viewModel.HasLoyaltySummary);
        Assert.Equal("Giảm 2% cho mọi đơn", viewModel.TierDiscountLabel);
        Assert.Equal("Còn 800 điểm để lên hạng Vàng", viewModel.NextTierLabel);
        Assert.Equal(0.2, viewModel.TierProgress, 3);
        Assert.Equal([Id], loyalty.Requested);
        Assert.Equal(PageContentState.Content, viewModel.ContentState);
    }

    [Fact]
    public async Task LoadAsync_TopTier_SaysTheTopTierIsReached()
    {
        // Arrange
        var loyalty = new FakeLoyalty { Summary = new(Id, 7000, "Platinum", 10, null, null) };
        var viewModel = new CustomerDetailViewModel(new FakeCustomers(), loyalty: loyalty);

        // Act
        await viewModel.LoadAsync(Id);

        // Assert
        Assert.Equal("Giảm 10% cho mọi đơn", viewModel.TierDiscountLabel);
        Assert.Equal("Đã đạt hạng cao nhất", viewModel.NextTierLabel);
        Assert.Equal(1.0, viewModel.TierProgress);
    }

    [Fact]
    public async Task LoadAsync_StandardCustomer_HasNoTierDiscount()
    {
        // Arrange
        var loyalty = new FakeLoyalty { Summary = new(Id, 120, "Standard", 0, "Silver", 380) };
        var viewModel = new CustomerDetailViewModel(new FakeCustomers(), loyalty: loyalty);

        // Act
        await viewModel.LoadAsync(Id);

        // Assert
        Assert.Equal("Chưa có ưu đãi hạng", viewModel.TierDiscountLabel);
        Assert.Equal("Còn 380 điểm để lên hạng Bạc", viewModel.NextTierLabel);
    }

    [Fact]
    public async Task LoadAsync_SalesSession_LoadsTheSummaryButNoHistory()
    {
        // Arrange
        var customers = new FakeCustomers();
        var loyalty = new FakeLoyalty { Summary = new(Id, 700, "Silver", 2, "Gold", 800) };
        var session = new InMemorySessionStore();
        session.SetSession("token", "refresh", "cashier1", "Thu ngân", "SalesStaff");
        var viewModel = new CustomerDetailViewModel(customers, session, loyalty);

        // Act
        await viewModel.LoadAsync(Id);

        // Assert
        Assert.True(viewModel.HasLoyaltySummary);
        Assert.Equal([Id], loyalty.Requested);
        Assert.Equal(0, customers.HistoryCalls);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(401)]
    public async Task LoadAsync_SummaryFails_ProfileStillShowsWithoutTheCard(int status)
    {
        // Arrange
        var loyalty = new FakeLoyalty { Failure = new ApiException(ApiErrorKind.Http, "private", status) };
        var viewModel = new CustomerDetailViewModel(new FakeCustomers(), loyalty: loyalty);

        // Act
        await viewModel.LoadAsync(Id);

        // Assert
        Assert.Equal("Vy", viewModel.Profile!.FullName);
        Assert.False(viewModel.HasLoyaltySummary);
        Assert.Equal(PageContentState.Content, viewModel.ContentState);
        Assert.Equal(string.Empty, viewModel.ErrorMessage);
    }

    [Fact]
    public async Task LoadAsync_SummaryNotFound_HidesTheCard()
    {
        // Arrange
        var viewModel = new CustomerDetailViewModel(new FakeCustomers(), loyalty: new FakeLoyalty { Summary = null });

        // Act
        await viewModel.LoadAsync(Id);

        // Assert
        Assert.False(viewModel.HasLoyaltySummary);
        Assert.Equal(PageContentState.Content, viewModel.ContentState);
    }

    [Fact]
    public async Task LoadAsync_AnotherCustomer_DoesNotKeepThePreviousSummary()
    {
        // Arrange
        var loyalty = new FakeLoyalty { Summary = new(Id, 700, "Silver", 2, "Gold", 800) };
        var viewModel = new CustomerDetailViewModel(new FakeCustomers(), loyalty: loyalty);
        await viewModel.LoadAsync(Id);
        Assert.True(viewModel.HasLoyaltySummary);
        loyalty.Failure = new ApiException(ApiErrorKind.Network, "down");

        // Act
        await viewModel.LoadAsync(Id);

        // Assert
        Assert.False(viewModel.HasLoyaltySummary);
        Assert.Equal(string.Empty, viewModel.NextTierLabel);
    }

    [Fact]
    public async Task LoadAsync_WithoutALoyaltyRepository_WorksAsBefore()
    {
        // Arrange
        var viewModel = new CustomerDetailViewModel(new FakeCustomers());

        // Act
        await viewModel.LoadAsync(Id);

        // Assert
        Assert.False(viewModel.HasLoyaltySummary);
        Assert.Equal("Vy", viewModel.Profile!.FullName);
    }

    [Fact]
    public async Task LoadAsync_Cancelled_PropagatesFromTheSummaryToo()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var loyalty = new FakeLoyalty { CancelOnRequest = cancellation };
        var viewModel = new CustomerDetailViewModel(new FakeCustomers(), loyalty: loyalty);

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => viewModel.LoadAsync(Id, cancellation.Token));
    }

    private sealed class FakeLoyalty : ILoyaltyRepository
    {
        public LoyaltySummary? Summary { get; set; }
        public Exception? Failure { get; set; }
        public CancellationTokenSource? CancelOnRequest { get; init; }
        public List<Guid> Requested { get; } = [];

        public Task<LoyaltySummary?> GetSummaryAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            Requested.Add(customerId);
            if (CancelOnRequest is not null)
            {
                CancelOnRequest.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            }

            return Failure is not null ? Task.FromException<LoyaltySummary?>(Failure) : Task.FromResult(Summary);
        }
    }

    private sealed class FakeCustomers : ICustomerRepository
    {
        public int HistoryCalls { get; private set; }

        public Task<CustomerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<CustomerProfile?>(new(Id, "Vy", "0901", null, null, null, 700, "Silver", true,
                DateTime.UtcNow.AddMonths(-3), null, 2, 100m, null));

        public Task<CustomerPage<CustomerLoyaltyTransaction>> GetLoyaltyHistoryAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            HistoryCalls++;
            return Task.FromResult(new CustomerPage<CustomerLoyaltyTransaction>([], 1, pageSize, 0, 0));
        }

        public Task<CustomerPage<CustomerOrder>> GetOrderHistoryAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            HistoryCalls++;
            return Task.FromResult(new CustomerPage<CustomerOrder>([], 1, pageSize, 0, 0));
        }

        public Task<CustomerPage<CustomerPurchasedItem>> GetPurchasedItemsAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            HistoryCalls++;
            return Task.FromResult(new CustomerPage<CustomerPurchasedItem>([], 1, pageSize, 0, 0));
        }

        public Task<CustomerPage<CustomerSummary>> SearchAsync(CustomerQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CustomerProfile> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CustomerProfile> UpdateAsync(Guid customerId, UpdateCustomerRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeactivateAsync(Guid customerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ActivateAsync(Guid customerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
