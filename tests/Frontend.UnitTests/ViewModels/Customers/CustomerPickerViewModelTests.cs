using TechStore.Client.Models.Customers;
using TechStore.Client.Repositories.Customers;
using TechStore.Client.Services.Api;
using TechStore.Client.ViewModels.Customers;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.ViewModels.Customers;

public sealed class CustomerPickerViewModelTests
{
    [Fact]
    public async Task Search_AsksForActiveCustomersAndShowsResults()
    {
        // Arrange
        var repository = new FakeCustomers();
        repository.Found.Add(Summary("Nguyễn Văn A", "0901234567"));
        var picker = new CustomerPickerViewModel(repository, new CustomerFormViewModel(repository));

        // Act
        picker.SearchText = " 0901 ";
        await picker.SearchCommand.ExecuteAsync(null);

        // Assert
        var query = Assert.Single(repository.Queries);
        Assert.Equal("0901", query.Query);
        Assert.True(query.IsActive);
        Assert.Equal(CustomerPickerViewModel.ResultLimit, query.PageSize);
        Assert.Equal("Nguyễn Văn A", Assert.Single(picker.Results).FullName);
        Assert.False(picker.HasMessage);
    }

    [Fact]
    public async Task Search_TooShortTextSendsNothingAndClearsResults()
    {
        // Arrange
        var repository = new FakeCustomers();
        var picker = new CustomerPickerViewModel(repository, new CustomerFormViewModel(repository));

        // Act
        picker.SearchText = "0";
        await picker.SearchCommand.ExecuteAsync(null);

        // Assert
        Assert.Empty(repository.Queries);
        Assert.Empty(picker.Results);
    }

    [Fact]
    public async Task Search_NoMatchSaysSoAndErrorIsSafe()
    {
        // Arrange
        var repository = new FakeCustomers();
        var picker = new CustomerPickerViewModel(repository, new CustomerFormViewModel(repository));

        // Act
        picker.SearchText = "0999";
        await picker.SearchCommand.ExecuteAsync(null);
        var noMatch = picker.Message;
        repository.Failure = new ApiException(ApiErrorKind.Network, "stack trace secret");
        await picker.SearchCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains("Không tìm thấy", noMatch);
        Assert.Contains("kết nối", picker.Message);
        Assert.DoesNotContain("secret", picker.Message);
    }

    [Fact]
    public async Task SelectAndClear_SwitchBetweenCustomerAndWalkInGuest()
    {
        // Arrange
        var repository = new FakeCustomers();
        var customer = Summary("Trần Thị B", "0912000111", "Gold");
        repository.Found.Add(customer);
        var picker = new CustomerPickerViewModel(repository, new CustomerFormViewModel(repository));
        Assert.Equal("Khách lẻ", picker.SelectionLabel);
        picker.SearchText = "0912";
        await picker.SearchCommand.ExecuteAsync(null);

        // Act
        picker.SelectCommand.Execute(customer);
        var selectedId = picker.SelectedCustomerId;
        var selectedLabel = picker.SelectionLabel;
        var resultsAfterSelect = picker.Results.Count;
        picker.ClearCommand.Execute(null);

        // Assert
        Assert.Equal(customer.Id, selectedId);
        Assert.Contains("Trần Thị B", selectedLabel);
        Assert.Contains("0912000111", selectedLabel);
        Assert.Equal(0, resultsAfterSelect);
        Assert.Null(picker.SelectedCustomerId);
        Assert.False(picker.HasSelection);
        Assert.Equal("Khách lẻ", picker.SelectionLabel);
    }

    [Fact]
    public void Select_IgnoresAnythingThatIsNotACustomerRow()
    {
        // Arrange
        var repository = new FakeCustomers();
        var picker = new CustomerPickerViewModel(repository, new CustomerFormViewModel(repository));

        // Act
        picker.SelectCommand.Execute(picker);

        // Assert
        Assert.Null(picker.SelectedCustomerId);
    }

    [Fact]
    public async Task CreateNew_PrefillsPhoneThenSelectsTheSavedCustomer()
    {
        // Arrange
        var repository = new FakeCustomers();
        var picker = new CustomerPickerViewModel(repository, new CustomerFormViewModel(repository));
        picker.SearchText = "0987654321";

        // Act
        picker.StartCreateCommand.Execute(null);
        var prefilled = picker.Form.Phone;
        picker.Form.FullName = "Lê Văn C";
        await picker.SaveNewCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("0987654321", prefilled);
        Assert.Equal("0987654321", Assert.Single(repository.Created).Phone);
        Assert.Equal(repository.CreatedId, picker.SelectedCustomerId);
        Assert.Contains("Lê Văn C", picker.SelectionLabel);
        Assert.False(picker.IsCreating);
    }

    [Fact]
    public void CreateNew_PrefillsNameWhenSearchIsNotAPhone()
    {
        // Arrange
        var repository = new FakeCustomers();
        var picker = new CustomerPickerViewModel(repository, new CustomerFormViewModel(repository));
        picker.SearchText = "Phạm D";

        // Act
        picker.StartCreateCommand.Execute(null);

        // Assert
        Assert.Equal("Phạm D", picker.Form.FullName);
        Assert.Equal(string.Empty, picker.Form.Phone);
        Assert.True(picker.IsCreating);
    }

    [Fact]
    public async Task CreateNew_PhoneAlreadyUsedSearchesThatPhoneInstead()
    {
        // Arrange
        var repository = new FakeCustomers
        {
            CreateFailure = new ApiException(ApiErrorKind.Http, "duplicate", 409)
        };
        repository.Found.Add(Summary("Khách cũ", "0901111222"));
        var picker = new CustomerPickerViewModel(repository, new CustomerFormViewModel(repository));
        picker.StartCreateCommand.Execute(null);
        picker.Form.FullName = "Khách mới";
        picker.Form.Phone = "0901111222";

        // Act
        await picker.SaveNewCommand.ExecuteAsync(null);

        // Assert
        Assert.False(picker.IsCreating);
        Assert.Null(picker.SelectedCustomerId);
        Assert.Equal("0901111222", picker.SearchText);
        Assert.Equal("0901111222", repository.Queries.Last().Query);
        Assert.Equal("Khách cũ", Assert.Single(picker.Results).FullName);
        Assert.Contains("đã có khách hàng", picker.Message);
    }

    [Fact]
    public async Task CreateNew_ValidationErrorKeepsTheFormOpen()
    {
        // Arrange
        var repository = new FakeCustomers();
        var picker = new CustomerPickerViewModel(repository, new CustomerFormViewModel(repository));
        picker.StartCreateCommand.Execute(null);

        // Act
        await picker.SaveNewCommand.ExecuteAsync(null);

        // Assert
        Assert.True(picker.IsCreating);
        Assert.Empty(repository.Created);
        Assert.NotEmpty(picker.Form.FullNameError);
    }

    [Fact]
    public async Task Search_OlderResponseDoesNotReplaceNewerOne()
    {
        // Arrange
        var repository = new FakeCustomers();
        var slow = new TaskCompletionSource<CustomerPage<CustomerSummary>>();
        repository.Pending = slow.Task;
        var picker = new CustomerPickerViewModel(repository, new CustomerFormViewModel(repository));
        picker.SearchText = "0901";
        var first = picker.SearchCommand.ExecuteAsync(null);

        // Act
        repository.Pending = null;
        repository.Found.Add(Summary("Mới nhất", "0902000000"));
        picker.SearchText = "0902";
        await picker.SearchCommand.ExecuteAsync(null);
        slow.SetResult(new CustomerPage<CustomerSummary>([Summary("Cũ", "0901000000")], 1, 8, 1, 1));
        await first;

        // Assert
        Assert.Equal("Mới nhất", Assert.Single(picker.Results).FullName);
    }

    private static CustomerSummary Summary(string name, string phone, string tier = "Standard") =>
        new(Guid.NewGuid(), name, phone, 0, tier, true, DateTime.UtcNow, 0, 0m, null);

    private sealed class FakeCustomers : ICustomerRepository
    {
        public List<CustomerSummary> Found { get; } = [];
        public List<CustomerQuery> Queries { get; } = [];
        public List<CreateCustomerRequest> Created { get; } = [];
        public Guid CreatedId { get; } = Guid.NewGuid();
        public Exception? Failure { get; set; }
        public Exception? CreateFailure { get; init; }
        public Task<CustomerPage<CustomerSummary>>? Pending { get; set; }

        public Task<CustomerPage<CustomerSummary>> SearchAsync(CustomerQuery query, CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            if (Failure is not null) return Task.FromException<CustomerPage<CustomerSummary>>(Failure);
            if (Pending is not null) return Pending;
            var items = Found.Where(c => query.Query is null || c.Phone.Contains(query.Query) || c.FullName.Contains(query.Query)).ToList();
            return Task.FromResult(new CustomerPage<CustomerSummary>(items, 1, query.PageSize, items.Count, items.Count == 0 ? 0 : 1));
        }

        public Task<CustomerProfile> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default)
        {
            if (CreateFailure is not null) return Task.FromException<CustomerProfile>(CreateFailure);
            Created.Add(request);
            return Task.FromResult(new CustomerProfile(CreatedId, request.FullName!, request.Phone!, null, null, null, 0, "Standard", true, DateTime.UtcNow, null, 0, 0m, null));
        }

        public Task<CustomerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CustomerPage<CustomerLoyaltyTransaction>> GetLoyaltyHistoryAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CustomerPage<CustomerOrder>> GetOrderHistoryAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CustomerPage<CustomerPurchasedItem>> GetPurchasedItemsAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CustomerProfile> UpdateAsync(Guid customerId, UpdateCustomerRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeactivateAsync(Guid customerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ActivateAsync(Guid customerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
