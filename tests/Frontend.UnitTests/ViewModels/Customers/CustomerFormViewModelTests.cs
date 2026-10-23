using TechStore.Client.Models.Customers;
using TechStore.Client.Repositories.Customers;
using TechStore.Client.Services.Api;
using TechStore.Client.ViewModels.Customers;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.ViewModels.Customers;

public sealed class CustomerFormViewModelTests
{
    [Fact]
    public void Initialize_ExistingCustomer_ExposesReadOnlyMembershipPresentation()
    {
        var profile = Profile() with { FullName = "Trần Minh Hoàng", LoyaltyPoints = 1520, Tier = "Gold", DateOfBirth = new DateOnly(2005, 7, 17) };
        var vm = new CustomerFormViewModel(new FakeCustomers(), profile);

        Assert.True(vm.IsEdit);
        Assert.Equal("Chỉnh sửa thông tin khách hàng", vm.Title);
        Assert.Equal("Cập nhật thông tin định danh cho Trần Minh Hoàng", vm.Subtitle);
        Assert.Equal("Hạng Vàng", vm.TierLabel);
        Assert.Equal("1.520 điểm", vm.LoyaltyPointsLabel);
        Assert.Equal("17/07/2005", vm.DateOfBirth);
        Assert.True(vm.ShowMembershipSummary);
    }

    [Fact]
    public void Initialize_NewCustomer_HidesMembershipPresentation()
    {
        var vm = new CustomerFormViewModel(new FakeCustomers());

        Assert.False(vm.IsEdit);
        Assert.Equal("Thêm khách hàng", vm.Title);
        Assert.Equal("Tạo hồ sơ khách hàng mới cho TechStore", vm.Subtitle);
        Assert.False(vm.ShowMembershipSummary);
    }

    [Fact]
    public async Task SaveAsync_CreatesCustomerWithTrimmedFields()
    {
        var repository = new FakeCustomers();
        var vm = new CustomerFormViewModel(repository) { FullName = " Vy Nguyen ", Phone = " 0901 ", Email = " ", DateOfBirth = "2000-01-02", Address = " HCMC " };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(vm.SavedProfile);
        Assert.Equal("Vy Nguyen", repository.Created!.FullName);
        Assert.Equal("0901", repository.Created.Phone);
        Assert.Null(repository.Created.Email);
        Assert.Equal(new DateOnly(2000, 1, 2), repository.Created.DateOfBirth);
        Assert.Equal("HCMC", repository.Created.Address);
    }

    [Fact]
    public async Task SaveAsync_UpdatesExistingProfile()
    {
        var profile = Profile();
        var repository = new FakeCustomers();
        var vm = new CustomerFormViewModel(repository, profile) { FullName = "Updated", Phone = "0902" };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(vm.IsEdit);
        Assert.Equal(profile.Id, repository.UpdatedId);
        Assert.Equal("Updated", repository.Updated!.FullName);
        Assert.NotNull(vm.SavedProfile);
    }

    [Fact]
    public async Task SaveAsync_ValidatesRequiredFieldsAndDateBeforeRequest()
    {
        var repository = new FakeCustomers();
        var vm = new CustomerFormViewModel(repository) { DateOfBirth = "tomorrow" };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Vui lòng nhập họ tên.", vm.FullNameError);
        Assert.Equal("Vui lòng nhập số điện thoại.", vm.PhoneError);
        Assert.NotEmpty(vm.DateOfBirthError);
        Assert.Equal(0, repository.WriteCount);
    }

    [Fact]
    public async Task SaveAsync_ShowsServerFieldErrorsAndRetainsInput()
    {
        var repository = new FakeCustomers
        {
            WriteException = new ApiException(ApiErrorKind.Http, "bad request", 400,
                fieldErrors: new Dictionary<string, IReadOnlyList<string>> { ["FullName"] = ["Tên không hợp lệ"] })
        };
        var vm = new CustomerFormViewModel(repository) { FullName = "Input", Phone = "0901" };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Tên không hợp lệ", vm.FullNameError);
        Assert.Equal("Input", vm.FullName);
        Assert.Null(vm.SavedProfile);
    }

    [Fact]
    public async Task SaveAsync_MapsDuplicatePhoneConflictToPhoneField()
    {
        var repository = new FakeCustomers { WriteException = new ApiException(ApiErrorKind.Http, "conflict", 409) };
        var vm = new CustomerFormViewModel(repository) { FullName = "Vy", Phone = "0901" };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Contains("đã được sử dụng", vm.PhoneError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveAsync_PropagatesCancellation()
    {
        var repository = new FakeCustomers { WriteException = new OperationCanceledException() };
        var vm = new CustomerFormViewModel(repository) { FullName = "Vy", Phone = "0901" };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => vm.SaveCommand.ExecuteAsync(cts.Token));
    }

    private static CustomerProfile Profile() => new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Vy", "0901", null, null, null, 0, "Standard", true, DateTime.UtcNow, null, 0, 0, null);

    private sealed class FakeCustomers : ICustomerRepository
    {
        public CreateCustomerRequest? Created { get; private set; }
        public UpdateCustomerRequest? Updated { get; private set; }
        public Guid? UpdatedId { get; private set; }
        public Exception? WriteException { get; init; }
        public int WriteCount { get; private set; }
        public Task<CustomerPage<CustomerSummary>> SearchAsync(CustomerQuery query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CustomerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CustomerPage<CustomerLoyaltyTransaction>> GetLoyaltyHistoryAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CustomerPage<CustomerOrder>> GetOrderHistoryAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CustomerPage<CustomerPurchasedItem>> GetPurchasedItemsAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CustomerProfile> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default) { WriteCount++; if (WriteException is not null) return Task.FromException<CustomerProfile>(WriteException); Created = request; return Task.FromResult(Profile()); }
        public Task<CustomerProfile> UpdateAsync(Guid customerId, UpdateCustomerRequest request, CancellationToken cancellationToken = default) { WriteCount++; if (WriteException is not null) return Task.FromException<CustomerProfile>(WriteException); UpdatedId = customerId; Updated = request; return Task.FromResult(Profile()); }
        public Task DeactivateAsync(Guid customerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task ActivateAsync(Guid customerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
