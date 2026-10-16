using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using TechStore.Api.Common.Data;
using TechStore.Api.Customers;
using TechStore.Api.Customers.Entities;
using TechStore.Api.Customers.Services;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Orders.Repositories;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

public class CustomerServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
    private readonly FakeCustomerRepository _customers = new();
    private readonly FakeOrderRepository _orders = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly CustomerService _service;

    public CustomerServiceTests() => _service = new(_customers, _orders, _unitOfWork, new FixedTimeProvider(new DateTimeOffset(Now)));

    [Fact]
    public async Task SearchAsync_Should_FilterAndPageCustomersWithStableOrdering()
    {
        var first = AddCustomer("Nguyễn An", "0901", CustomerTier.Silver, false, Now.AddDays(-1));
        AddCustomer("An Bình", "0902", CustomerTier.Gold, true, Now);
        var third = AddCustomer("Nguyễn Vy", "0903", CustomerTier.Silver, false, Now.AddDays(-1));

        var result = await _service.SearchAsync(new CustomerSearchRequest { Q = "nguyễn", Tier = "silver", IsActive = false, Page = 1, PageSize = 1 });

        result.IsSuccess.Should().BeTrue();
        result.Data!.TotalCount.Should().Be(2);
        result.Data.TotalPages.Should().Be(2);
        result.Data.Items.Should().ContainSingle().Which.Id.Should().Be(new[] { first.Id, third.Id }.Min());
    }

    [Fact]
    public async Task SearchAsync_Should_RejectUnknownAndNumericTierNames()
    {
        foreach (var tier in new[] { "Unknown", "1" })
        {
            var result = await _service.SearchAsync(new CustomerSearchRequest { Tier = tier });
            result.ValidationErrors.Should().ContainKey("tier");
        }
    }

    [Fact]
    public async Task GetByIdAndLoyaltyHistory_Should_ReturnPersistedValuesAndUnknownId()
    {
        var customer = AddCustomer("Loyalty", "0909", CustomerTier.Gold, false, Now);
        customer.LoyaltyPoints = 1520;
        var entry = new CustomerLoyaltyPoint { CustomerId = customer.Id, PointsChange = 20, BalanceAfter = 1520, Reason = "Order completed", CreatedAt = Now };
        _customers.PointEntries.Add(entry);

        var profile = await _service.GetByIdAsync(customer.Id);
        var history = await _service.GetLoyaltyPointsAsync(customer.Id, null, null);
        var missing = await _service.GetLoyaltyPointsAsync(Guid.NewGuid(), null, null);

        profile.Data!.LoyaltyPoints.Should().Be(1520);
        profile.Data.Tier.Should().Be("Gold");
        profile.Data.IsActive.Should().BeFalse();
        history.Data!.Items.Should().ContainSingle().Which.PointsChange.Should().Be(20);
        history.Data.Items.Single().BalanceAfter.Should().Be(1520);
        missing.Found.Should().BeFalse();
    }

    [Fact]
    public async Task GetOrders_Should_ValidateCustomerAndQueryOnlyItsHistory()
    {
        var customer = AddCustomer("Orders", "0907");
        _orders.CustomSearchResult = (new[] { new Order { OrderCode = "ORD-1", CustomerId = customer.Id, TotalAmount = 1200m, CreatedAt = Now } }, 1);

        var result = await _service.GetOrdersAsync(customer.Id, 0, 500);
        var missing = await _service.GetOrdersAsync(Guid.NewGuid(), null, null);

        result.Data!.Page.Should().Be(1);
        result.Data.PageSize.Should().Be(100);
        result.Data.Items.Should().ContainSingle().Which.OrderCode.Should().Be("ORD-1");
        _orders.LastCriteria!.CustomerId.Should().Be(customer.Id);
        _orders.LastCriteria.Descending.Should().BeTrue();
        missing.Found.Should().BeFalse();
        _orders.SearchCallCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_Should_TrimFieldsAndKeepServerOwnedLoyaltyDefaults()
    {
        var result = await _service.CreateAsync(new CreateCustomerRequest { FullName = "  Vy Nguyen ", Phone = " 0900 ", Email = " vy@example.com ", Address = " HCMC " });

        result.IsSuccess.Should().BeTrue();
        result.Data!.FullName.Should().Be("Vy Nguyen");
        result.Data.Phone.Should().Be("0900");
        result.Data.Email.Should().Be("vy@example.com");
        result.Data.Address.Should().Be("HCMC");
        result.Data.LoyaltyPoints.Should().Be(0);
        result.Data.Tier.Should().Be("Standard");
        result.Data.IsActive.Should().BeTrue();
        _unitOfWork.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_Should_AggregateValidationErrorsWithoutSaving()
    {
        var result = await _service.CreateAsync(new CreateCustomerRequest { FullName = " ", Phone = " ", Email = "bad", DateOfBirth = DateOnly.FromDateTime(Now.AddDays(1)) });

        result.ValidationErrors.Should().ContainKeys("fullName", "phone", "email", "dateOfBirth");
        _unitOfWork.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateAndUpdateAsync_Should_ReturnConflictForExistingOrConcurrentPhone()
    {
        AddCustomer("Existing", "1234");
        var existing = await _service.CreateAsync(new CreateCustomerRequest { FullName = "Other", Phone = "1234" });
        existing.IsConflict.Should().BeTrue();

        var original = AddCustomer("Original", "5678");
        _unitOfWork.SaveExceptionQueue.Enqueue(new DuplicateKeyException("idx_customers_phone"));
        var concurrent = await _service.UpdateAsync(original.Id, new UpdateCustomerRequest { FullName = "Changed", Phone = "9999" });
        concurrent.IsConflict.Should().BeTrue();
        _unitOfWork.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_Should_ChangeContactOnlyAndReturnNotFoundForUnknownId()
    {
        var customer = AddCustomer("Before", "123");
        customer.LoyaltyPoints = 88;
        customer.Tier = CustomerTier.Silver;
        var result = await _service.UpdateAsync(customer.Id, new UpdateCustomerRequest { FullName = " After ", Phone = " 123 " });
        var missing = await _service.UpdateAsync(Guid.NewGuid(), new UpdateCustomerRequest { FullName = "Name", Phone = "321" });

        result.Data!.FullName.Should().Be("After");
        result.Data.LoyaltyPoints.Should().Be(88);
        result.Data.Tier.Should().Be("Silver");
        missing.Found.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivateAsync_Should_PreserveCustomerAndPointHistoryAndBeIdempotent()
    {
        var customer = AddCustomer("Keep", "333");
        _customers.PointEntries.Add(new CustomerLoyaltyPoint { CustomerId = customer.Id, PointsChange = 5, BalanceAfter = 5, Reason = "Existing" });

        (await _service.DeactivateAsync(customer.Id)).IsSuccess.Should().BeTrue();
        (await _service.DeactivateAsync(customer.Id)).IsSuccess.Should().BeTrue();

        _customers.Customers.Should().ContainKey(customer.Id);
        _customers.PointEntries.Should().ContainSingle();
        customer.IsActive.Should().BeFalse();
        _unitOfWork.SaveChangesCount.Should().Be(1);
        (await _service.DeactivateAsync(Guid.NewGuid())).Found.Should().BeFalse();
    }

    [Fact]
    public void CustomersController_Should_RequireAdminRole()
    {
        var attribute = typeof(CustomersController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single();
        attribute.Roles.Should().Be("Admin");
    }

    private Customer AddCustomer(string name, string phone, CustomerTier tier = CustomerTier.Standard, bool active = true, DateTime? createdAt = null)
    {
        var customer = new Customer { FullName = name, Phone = phone, Tier = tier, IsActive = active, CreatedAt = createdAt ?? Now };
        _customers.Customers.Add(customer.Id, customer);
        return customer;
    }
}
