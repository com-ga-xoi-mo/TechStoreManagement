using FluentAssertions;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Customers.Entities;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Orders.Repositories;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.UnitTests;

// UC-ORD-03: GetByIdAsync and UC-ORD-02: SearchAsync
public partial class OrderServiceTests
{
    [Fact]
    public async Task GetByIdAsync_UnknownOrder_ReturnsNotFound()
    {
        // Act
        var result = await _service.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Found.Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_CompletedOrder_ShowsImeiAndCustomer()
    {
        // Arrange
        var (order, item, _) = AddOrder(OrderStatus.Completed);
        order.Customer = new Customer { FullName = "Nguyen Van A", Phone = "0901234567" };
        order.CustomerId = order.Customer.Id;
        item.UnitCost = 21_500_000m;
        item.SerialImei = new SerialImei { Imei = "358912345678901", ProductId = item.ProductId };

        // Act
        var result = await _service.GetByIdAsync(order.Id);

        // Assert
        result.Found.Should().BeTrue();
        result.Data!.CustomerName.Should().Be("Nguyen Van A");
        result.Data.CustomerPhone.Should().Be("0901234567");
        var line = result.Data.Items.Should().ContainSingle().Subject;
        line.Imei.Should().Be("358912345678901");
        typeof(OrderItemDto).GetProperties().Select(p => p.Name).Should().NotContain("UnitCost");
    }

    [Fact]
    public async Task GetByIdAsync_Lines_AreOrderedByProductNameThenSku()
    {
        // Arrange
        var (order, item, _) = AddOrder(OrderStatus.Pending);
        item.ProductName = "Zebra";
        order.Items.Add(new OrderItem { OrderId = order.Id, ProductName = "Apple", Sku = "B", Quantity = 1 });
        order.Items.Add(new OrderItem { OrderId = order.Id, ProductName = "Apple", Sku = "A", Quantity = 1 });

        // Act
        var result = await _service.GetByIdAsync(order.Id);

        // Assert
        result.Data!.Items.Select(i => $"{i.ProductName}/{i.Sku}").Should().Equal("Apple/A", "Apple/B", "Zebra/IP15P");
    }

    [Fact]
    public async Task SearchAsync_Defaults_UseFirstPageNewestFirst()
    {
        // Act
        var result = await _service.SearchAsync(new OrderSearchRequest());

        // Assert
        result.IsSuccess.Should().BeTrue();
        var criteria = _orders.LastCriteria!;
        criteria.Page.Should().Be(1);
        criteria.PageSize.Should().Be(20);
        criteria.SortField.Should().Be(OrderSortField.CreatedAt);
        criteria.Descending.Should().BeTrue();
        criteria.Status.Should().BeNull();
        criteria.Keyword.Should().BeNull();
    }

    [Fact]
    public async Task SearchAsync_StatusFilter_IsParsedCaseInsensitively()
    {
        // Act
        await _service.SearchAsync(new OrderSearchRequest { Status = "completed" });

        // Assert
        _orders.LastCriteria!.Status.Should().Be(OrderStatus.Completed);
    }

    [Fact]
    public async Task SearchAsync_InvalidStatusAndSort_AreBothReported()
    {
        // Act
        var result = await _service.SearchAsync(new OrderSearchRequest { Status = "foo", Sort = "customer" });

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors!.Keys.Should().BeEquivalentTo("status", "sort");
        _orders.SearchCallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("createdAt:asc", OrderSortField.CreatedAt, false)]
    [InlineData("CreatedAt:DESC", OrderSortField.CreatedAt, true)]
    [InlineData("totalAmount:asc", OrderSortField.TotalAmount, false)]
    [InlineData("totalAmount:desc", OrderSortField.TotalAmount, true)]
    public async Task SearchAsync_ValidSort_IsPassedToTheRepository(string sort, OrderSortField field, bool descending)
    {
        // Act
        var result = await _service.SearchAsync(new OrderSearchRequest { Sort = sort });

        // Assert
        result.IsSuccess.Should().BeTrue();
        _orders.LastCriteria!.SortField.Should().Be(field);
        _orders.LastCriteria.Descending.Should().Be(descending);
    }

    [Theory]
    [InlineData("createdAt")]
    [InlineData("createdAt:sideways")]
    [InlineData("a:b:c")]
    public async Task SearchAsync_MalformedSort_IsRejected(string sort)
    {
        // Act
        var result = await _service.SearchAsync(new OrderSearchRequest { Sort = sort });

        // Assert
        result.ValidationErrors.Should().ContainKey("sort");
    }

    [Fact]
    public async Task SearchAsync_FromLaterThanTo_ReportsFrom()
    {
        // Act
        var result = await _service.SearchAsync(new OrderSearchRequest
        {
            From = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc),
            To = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        // Assert
        result.ValidationErrors.Should().ContainKey("from");
    }

    [Fact]
    public async Task SearchAsync_UnspecifiedDateKind_IsTreatedAsUtc()
    {
        // Act
        await _service.SearchAsync(new OrderSearchRequest { From = new DateTime(2026, 10, 1) });

        // Assert
        _orders.LastCriteria!.From!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task SearchAsync_OutOfRangePaging_IsNormalized()
    {
        // Act
        await _service.SearchAsync(new OrderSearchRequest { Page = 0, PageSize = 500 });

        // Assert
        _orders.LastCriteria!.Page.Should().Be(1);
        _orders.LastCriteria.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task SearchAsync_KeywordAndCustomer_AreTrimmedAndPassedThrough()
    {
        // Arrange
        var customerId = Guid.NewGuid();

        // Act
        await _service.SearchAsync(new OrderSearchRequest { Q = "  0901234567  ", CustomerId = customerId });

        // Assert
        _orders.LastCriteria!.Keyword.Should().Be("0901234567");
        _orders.LastCriteria.CustomerId.Should().Be(customerId);
    }

    [Fact]
    public async Task SearchAsync_PageBeyondTheEnd_ReturnsEmptyItemsWithCorrectTotals()
    {
        // Arrange
        _orders.CustomSearchResult = (new List<Order>(), 5);

        // Act
        var result = await _service.SearchAsync(new OrderSearchRequest { Page = 99, PageSize = 2 });

        // Assert
        result.Data!.Items.Should().BeEmpty();
        result.Data.TotalCount.Should().Be(5);
        result.Data.TotalPages.Should().Be(3);
        result.Data.Page.Should().Be(99);
    }

    [Fact]
    public async Task SearchAsync_Results_AreMappedToSummaries()
    {
        // Arrange
        var (order, _, product) = AddOrder(OrderStatus.Completed);
        product.Category = new Category { Name = "Smartphones" };
        product.CategoryId = product.Category.Id;
        order.Customer = new Customer { FullName = "Nguyen Van A" };
        order.CustomerId = order.Customer.Id;
        order.PaymentMethod = PaymentMethod.VietQr;

        // Act
        var result = await _service.SearchAsync(new OrderSearchRequest());

        // Assert
        var summary = result.Data!.Items.Should().ContainSingle().Subject;
        summary.OrderCode.Should().Be("ORD-202610-0001");
        summary.Status.Should().Be("Completed");
        summary.PaymentMethod.Should().Be("VietQr");
        summary.CustomerName.Should().Be("Nguyen Van A");
        summary.ItemCount.Should().Be(1);
        summary.TotalAmount.Should().Be(30_000_000m);
        summary.PrimaryProductName.Should().Be("iPhone 15 Pro");
        summary.PrimaryCategoryName.Should().Be("Smartphones");
    }
}
