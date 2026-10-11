using TechStore.Api.Catalog.Entities;
using TechStore.Api.Customers.Entities;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Orders.Services;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

// Shared fixture for the OrderService tests (Create / Query / Transitions partial files).
public partial class OrderServiceTests
{
    private static readonly Guid ActingUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime Now = new(2026, 10, 11, 8, 0, 0, DateTimeKind.Utc);

    private readonly FakeOrderRepository _orders = new();
    private readonly FakeProductRepository _products = new();
    private readonly FakeCustomerRepository _customers = new();
    private readonly FakeInventoryFulfillmentService _fulfillment = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly OrderService _service;

    public OrderServiceTests()
    {
        _service = new OrderService(
            _orders,
            _products,
            _customers,
            _fulfillment,
            _unitOfWork,
            new FixedTimeProvider(new DateTimeOffset(Now)));
    }

    private Product AddProduct(
        bool serialTracked = true,
        decimal basePrice = 30_000_000m,
        decimal costPrice = 22_000_000m,
        bool active = true,
        string name = "iPhone 15 Pro",
        string sku = "IP15P")
    {
        var product = new Product
        {
            Name = name,
            Sku = sku,
            BasePrice = basePrice,
            CostPrice = costPrice,
            IsSerialTracked = serialTracked,
            IsActive = active
        };
        _products.Products[product.Id] = product;
        return product;
    }

    private static ProductVariant AddVariant(
        Product product,
        decimal price,
        string sku = "IP15P-256",
        bool active = true,
        string name = "256GB")
    {
        var variant = new ProductVariant
        {
            ProductId = product.Id,
            Product = product,
            VariantName = name,
            Sku = sku,
            Price = price,
            IsActive = active
        };
        product.Variants.Add(variant);
        return variant;
    }

    private Customer AddCustomer(bool active = true, string name = "Nguyen Van A", string phone = "0901234567")
    {
        var customer = new Customer { FullName = name, Phone = phone, IsActive = active };
        _customers.Customers[customer.Id] = customer;
        return customer;
    }

    private static CreateOrderItemRequest TrackedItem(Product product, ProductVariant? variant = null) => new()
    {
        ProductId = product.Id,
        VariantId = variant?.Id,
        SerialImeiId = Guid.NewGuid()
    };

    private static CreateOrderRequest RequestOf(params CreateOrderItemRequest[] items) => new() { Items = items.ToList() };

    private static UpdateOrderStatusRequest Target(string? status) => new() { TargetStatus = status };

    private (Order Order, OrderItem Item, Product Product) AddOrder(
        OrderStatus status,
        bool productActive = true,
        DateTime? paidAt = null,
        decimal costPrice = 22_000_000m)
    {
        var product = new Product
        {
            Name = "iPhone 15 Pro",
            Sku = "IP15P",
            BasePrice = 30_000_000m,
            CostPrice = costPrice,
            IsSerialTracked = true,
            IsActive = productActive
        };
        var order = new Order
        {
            OrderCode = "ORD-202610-0001",
            Status = status,
            PaidAt = paidAt,
            CashierUserId = ActingUserId,
            Subtotal = 30_000_000m,
            TotalAmount = 30_000_000m,
            CreatedAt = Now.AddHours(-1)
        };
        var item = new OrderItem
        {
            OrderId = order.Id,
            Order = order,
            ProductId = product.Id,
            Product = product,
            SerialImeiId = Guid.NewGuid(),
            ProductName = product.Name,
            Sku = product.Sku,
            UnitPrice = 30_000_000m,
            Quantity = 1,
            TotalPrice = 30_000_000m
        };
        order.Items.Add(item);
        _orders.Orders[order.Id] = order;
        return (order, item, product);
    }
}
