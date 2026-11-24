using TechStore.Api.Catalog.Entities;
using TechStore.Api.Customers.Entities;
using TechStore.Api.Identity.Services;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Warranty.Entities;
using TechStore.Api.Warranty.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests.Warranty;

public sealed partial class WarrantyServiceTests
{
    private sealed class Fixture
    {
        public static readonly DateTime Now = new(2026, 11, 24, 5, 0, 0, DateTimeKind.Utc);
        public FakeInventoryAfterSaleRepository AfterSale { get; } = new();
        public FakeWarrantyCaseRepository Cases { get; }
        public FakeWarrantyInventoryService Inventory { get; } = new();
        public FakeUnitOfWork Work { get; } = new();
        public WarrantyCaseService Service { get; }
        public AuthenticatedUser Actor { get; } = new(Guid.NewGuid(), RoleType.SalesStaff, 0);
        public Customer Customer { get; } = new() { FullName = "Khách mua máy", Phone = "0901234567" };
        public SerialImei Device { get; }
        public Order Order { get; }
        public OrderItem Item { get; }
        public List<string> Calls { get; } = [];

        public Fixture(DateTime? now = null)
        {
            Cases = new FakeWarrantyCaseRepository(AfterSale);
            var product = new Product { Name = "Máy tính" };
            Device = new SerialImei
            {
                Imei = "123456789012345", SerialNumber = "SER-01", Status = SerialImeiStatus.Sold,
                Product = product, ProductId = product.Id,
                WarrantyStartAt = Now, WarrantyEndAt = Now.AddMonths(12)
            };
            Order = new Order
            {
                OrderCode = "ORD-202611-0010", Status = OrderStatus.Completed,
                CompletedAt = Now, Customer = Customer, CustomerId = Customer.Id
            };
            Item = new OrderItem
            {
                Order = Order, OrderId = Order.Id, ProductName = product.Name,
                Product = product, ProductId = product.Id, SerialImeiId = Device.Id
            };
            Order.Items.Add(Item);
            Device.SaleOrderItemId = Item.Id;
            Cases.Devices.Add(Device);
            Cases.Items.Add(Item);
            Cases.ActorNames.Add(Actor.Id, "Nhân viên tiếp nhận");
            AfterSale.Orders.Add(Order);
            Cases.OnLock = Calls.Add;
            AfterSale.OnLock = Calls.Add;
            Inventory.OnCall = Calls.Add;
            Service = new WarrantyCaseService(Cases, AfterSale, Inventory, Work, new FixedTimeProvider(now ?? Now));
        }

        public Task<WarrantyLookupResult> Lookup(string? orderCode = null, Guid? customerId = null) =>
            Service.LookupAsync(Device.Imei, orderCode, customerId, default);

        public ReceiveWarrantyRequest Request() =>
            new(Device.Imei, null, Customer.Id, "Không lên nguồn", Customer.FullName, Customer.Phone);

        public WarrantyCase AddClaim(WarrantyCaseStatus status)
        {
            var claim = new WarrantyCase
            {
                Id = Guid.NewGuid(), CaseCode = "BH-202611-0001", OrderId = Order.Id, OrderItemId = Item.Id,
                DeviceId = Device.Id, OpenedAt = Now, OpenedById = Actor.Id, Status = status,
                CustomerId = Order.CustomerId, WarrantyStartAt = Now, WarrantyEndAt = Now.AddMonths(12)
            };
            Cases.Add(claim);
            return claim;
        }
    }

    [Fact]
    public async Task Lookup_MatchesCustomerAtCoverageStart_AndReturnsCompletedSale()
    {
        var fixture = new Fixture();
        var lookup = (await fixture.Lookup(customerId: fixture.Customer.Id)).Data!;
        Assert.True(lookup.Eligible);
        Assert.Equal(fixture.Order.OrderCode, lookup.OrderCode);
        Assert.Equal(fixture.Order.CompletedAt, lookup.PurchasedAt);
        Assert.Equal(fixture.Customer.FullName, lookup.CustomerName);
        Assert.Equal(fixture.Customer.Phone, lookup.CustomerPhone);
        Assert.Equal(fixture.Device.WarrantyEndAt, lookup.WarrantyEndAt);
        Assert.Equal(false, lookup.IsWalkIn);
        Assert.Equal(0, fixture.Work.SaveChangesCount);
    }

    [Fact]
    public async Task Lookup_AtExclusiveEndAndBeforeCoverageStart_ReturnsExpired()
    {
        foreach (var now in new[] { Fixture.Now.AddMonths(12), Fixture.Now.AddTicks(-1) })
        {
            var fixture = new Fixture(now);
            var lookup = (await fixture.Lookup(customerId: fixture.Customer.Id)).Data!;
            Assert.False(lookup.Eligible);
            Assert.Contains(WarrantyReasonCodes.Expired, lookup.Reasons);
            Assert.Equal(fixture.Device.WarrantyEndAt, lookup.WarrantyEndAt);
        }
    }

    [Fact]
    public async Task Lookup_WalkInRequiresMatchingOrderCode_AndHidesProofAndHistoryUntilMatched()
    {
        var fixture = new Fixture();
        fixture.Order.CustomerId = null;
        fixture.Order.Customer = null;
        var previous = fixture.AddClaim(WarrantyCaseStatus.ReturnedToCustomer);

        foreach (var (code, reason) in new[] { (null, WarrantyReasonCodes.OrderCodeRequired), ("wrong", WarrantyReasonCodes.OrderMismatch) })
        {
            var lookup = (await fixture.Lookup(code, fixture.Customer.Id)).Data!;
            Assert.False(lookup.Eligible);
            Assert.Contains(reason, lookup.Reasons);
            Assert.Equal(fixture.Device.Id, lookup.DeviceId);
            Assert.Null(lookup.OrderCode);
            Assert.Null(lookup.PurchasedAt);
            Assert.Null(lookup.CustomerId);
            Assert.Null(lookup.CustomerName);
            Assert.Null(lookup.CustomerPhone);
            Assert.Null(lookup.IsWalkIn);
            Assert.Empty(lookup.History);
        }

        var verified = (await fixture.Lookup("  ord-202611-0010  ")).Data!;
        Assert.True(verified.Eligible);
        Assert.Equal(true, verified.IsWalkIn);
        Assert.Equal(previous.Id, Assert.Single(verified.History).Id);
    }

    [Fact]
    public async Task Lookup_WrongMemberCanSupplyCorrectOrderCode_AndOtherSalesStayPrivate()
    {
        var fixture = new Fixture();
        var prior = fixture.AddClaim(WarrantyCaseStatus.ReturnedToCustomer);
        prior.OrderItemId = Guid.NewGuid();
        var incorrect = (await fixture.Lookup(customerId: Guid.NewGuid())).Data!;
        Assert.Contains(WarrantyReasonCodes.CustomerMismatch, incorrect.Reasons);
        Assert.Null(incorrect.CustomerName);
        Assert.Empty(incorrect.History);

        var correctOrder = (await fixture.Lookup(fixture.Order.OrderCode, Guid.NewGuid())).Data!;
        Assert.True(correctOrder.Eligible);
        Assert.Empty(correctOrder.History);
    }

    [Fact]
    public async Task Lookup_MissingDeviceAndBlankIdentifier_ReturnDifferentResults()
    {
        var fixture = new Fixture();
        Assert.Null((await fixture.Service.LookupAsync("missing", null, null, default)).Data);
        var invalid = await fixture.Service.LookupAsync("   ", null, null, default);
        Assert.NotNull(invalid.Errors);
        Assert.Contains("code", invalid.Errors.Keys);
    }

    [Theory]
    [InlineData(SerialImeiStatus.InStock)]
    [InlineData(SerialImeiStatus.Reserved)]
    [InlineData(SerialImeiStatus.Returned)]
    [InlineData(SerialImeiStatus.Defective)]
    public async Task Lookup_UnsoldDevice_ReturnsNotSold(SerialImeiStatus status)
    {
        var fixture = new Fixture();
        fixture.Device.Status = status;
        Assert.Contains(WarrantyReasonCodes.NotSold, (await fixture.Lookup(customerId: fixture.Customer.Id)).Data!.Reasons);
    }

    [Fact]
    public async Task Lookup_TracksOpenCaseOpenReturnAndFullyReturnedLine()
    {
        var fixture = new Fixture();
        fixture.Order.Status = OrderStatus.PartiallyRefunded;
        fixture.Cases.ReturnedQuantities[fixture.Item.Id] = fixture.Item.Quantity;
        fixture.Cases.PendingReturns.Add(fixture.Item.Id);
        fixture.Device.Status = SerialImeiStatus.UnderRepair;
        fixture.AddClaim(WarrantyCaseStatus.Repairing);
        var lookup = (await fixture.Lookup(customerId: fixture.Customer.Id)).Data!;
        Assert.False(lookup.Eligible);
        Assert.Contains(WarrantyReasonCodes.OpenCase, lookup.Reasons);
        Assert.Contains(WarrantyReasonCodes.LineReturned, lookup.Reasons);
        Assert.Contains(WarrantyReasonCodes.PendingReturn, lookup.Reasons);
    }

    [Fact]
    public async Task Lookup_LegacySaleSkipsUndeliveredOrder_AndUncoveredOrderHidesProof()
    {
        var fixture = new Fixture();
        fixture.Device.SaleOrderItemId = null;
        fixture.AfterSale.Orders.Add(new Order
        {
            Status = OrderStatus.Cancelled,
            Items = [new OrderItem { SerialImeiId = fixture.Device.Id }]
        });
        Assert.True((await fixture.Lookup(customerId: fixture.Customer.Id)).Data!.Eligible);

        fixture.Order.Status = OrderStatus.Refunded;
        var lookup = (await fixture.Lookup(customerId: fixture.Customer.Id)).Data!;
        Assert.Contains(WarrantyReasonCodes.NoCoveringSale, lookup.Reasons);
        Assert.Null(lookup.OrderCode);
        Assert.Null(lookup.PurchasedAt);
    }
}
