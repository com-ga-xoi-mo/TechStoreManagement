using FluentAssertions;
using Microsoft.Extensions.Options;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Common.Data;
using TechStore.Api.Customers.Services;
using TechStore.Api.Identity.Services;
using TechStore.Api.Inventory.Services;
using TechStore.Api.Orders.Services;
using TechStore.Api.Sales;
using TechStore.Api.Sales.Entities;
using TechStore.Api.Sales.Services;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.UnitTests;

public class VietQrPaymentServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 25, 8, 0, 0, DateTimeKind.Utc);
    private static readonly AuthenticatedUser SalesA = new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"), RoleType.SalesStaff, 0);
    private static readonly AuthenticatedUser SalesB = new(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002"), RoleType.SalesStaff, 0);
    private static readonly AuthenticatedUser Manager = new(Guid.Parse("cccccccc-0000-0000-0000-000000000003"), RoleType.Manager, 0);

    private readonly FakeOrderRepository _orders = new();
    private readonly FakeProductRepository _products = new();
    private readonly FakeCustomerRepository _customers = new();
    private readonly FakePosSessionRepository _sessions = new();
    private readonly FakeVietQrHoldRepository _requests;
    private readonly FakeInventoryFulfillmentService _fulfillment = new();
    private readonly FakeVoucherService _vouchers = new();
    private readonly FakeVoucherRepository _voucherRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly VietQrOptions _bank = new() { BankBin = "970422", AccountNumber = "0123456789", AccountName = "CUA HANG TECHSTORE", HoldMinutes = 15 };
    private readonly FakeVietQrHoldService _holds;
    private readonly VietQrPaymentService _service;
    private readonly PosSession _shift;

    public VietQrPaymentServiceTests()
    {
        var time = new FixedTimeProvider(new DateTimeOffset(Now));
        var pricing = new PricingOptions { VatRatePercent = 0m };
        var orderService = new OrderService(_orders, _products, _customers, _fulfillment, _unitOfWork, time, Options.Create(pricing), _voucherRepository);
        _requests = new FakeVietQrHoldRepository(_orders);
        _holds = new FakeVietQrHoldService(_requests);
        _service = new VietQrPaymentService(orderService, _orders, _sessions, _requests, _holds, _fulfillment, _vouchers,
            _unitOfWork, Options.Create(_bank), time);
        _shift = _sessions.AddSession(SalesA.Id, opening: 1_000_000m);
    }

    [Fact]
    public async Task CreateAsync_ValidCart_HoldsStockAndStoresAPendingOrderAndQr()
    {
        // Arrange
        var request = RequestFor(AddProduct(price: 10_000_000m));

        // Act
        var result = await _service.CreateAsync(request, SalesA);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Replayed.Should().BeFalse();
        var payment = result.Data.Payment;
        payment.Status.Should().Be("Pending");
        payment.OrderStatus.Should().Be("Pending");
        payment.Amount.Should().Be(10_000_000m);
        payment.BankBin.Should().Be("970422");
        payment.BankAccountName.Should().Be("CUA HANG TECHSTORE");
        payment.CreatedAt.Should().Be(Now);
        payment.ExpiresAt.Should().Be(Now.AddMinutes(15));
        VietQrPayload.HasValidCrc(payment.QrContent).Should().BeTrue();
        payment.QrContent.Should().Contain(payment.TransactionCode);
        payment.QrImagePng.Should().NotBeNullOrEmpty();
        var order = _orders.AddedOrders.Should().ContainSingle().Subject;
        order.Status.Should().Be(OrderStatus.Pending);
        order.PaymentMethod.Should().Be(PaymentMethod.VietQr);
        order.PosSessionId.Should().Be(_shift.Id);
        order.CashierUserId.Should().Be(SalesA.Id);
        order.PaidAt.Should().BeNull();
        _fulfillment.CallLog.Should().Equal("reserve");
        _unitOfWork.CommitCount.Should().Be(1);
        _shift.VietQrSalesTotal.Should().Be(0m);
    }

    [Fact]
    public async Task CreateAsync_BankNotConfigured_ReturnsUnavailableAndTouchesNothing()
    {
        // Arrange
        _bank.AccountNumber = "";

        // Act
        var result = await _service.CreateAsync(RequestFor(AddProduct()), SalesA);

        // Assert
        result.Kind.Should().Be(PosResultKind.Unavailable);
        _orders.AddedOrders.Should().BeEmpty();
        _unitOfWork.BeginTransactionCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_BankBinIsNotSixDigits_ReturnsUnavailable()
    {
        // Arrange
        _bank.BankBin = "MB";

        // Act
        var result = await _service.CreateAsync(RequestFor(AddProduct()), SalesA);

        // Assert
        result.Kind.Should().Be(PosResultKind.Unavailable);
    }

    [Fact]
    public async Task CreateAsync_MissingKeyOrShift_ReturnsValidationErrors()
    {
        // Arrange
        var request = RequestFor(AddProduct());
        request.CheckoutKey = null;
        request.PosSessionId = Guid.Empty;

        // Act
        var result = await _service.CreateAsync(request, SalesA);

        // Assert
        result.ValidationErrors.Should().ContainKeys("checkoutKey", "posSessionId");
    }

    [Fact]
    public async Task CreateAsync_ClosedShift_ReturnsConflict()
    {
        // Arrange
        _shift.Status = PosSessionStatus.Closed;

        // Act
        var result = await _service.CreateAsync(RequestFor(AddProduct()), SalesA);

        // Assert
        result.Kind.Should().Be(PosResultKind.Conflict);
        _fulfillment.ReserveCallCount.Should().Be(0);
        _unitOfWork.RollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_AnotherCashiersShift_ReturnsNotFound()
    {
        // Act
        var result = await _service.CreateAsync(RequestFor(AddProduct()), SalesB);

        // Assert
        result.Kind.Should().Be(PosResultKind.NotFound);
        _orders.AddedOrders.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_DeviceNoLongerAvailable_ReturnsConflictAndStoresNoRequest()
    {
        // Arrange
        _fulfillment.ReserveResult = FulfillmentResult.Failed(new FulfillmentProblem(0, "Device is not in stock."));

        // Act
        var result = await _service.CreateAsync(RequestFor(AddProduct()), SalesA);

        // Assert
        result.Kind.Should().Be(PosResultKind.Conflict);
        result.ConflictReason.Should().Contain("Line 1");
        _requests.Requests.Should().BeEmpty();
        _unitOfWork.CommitCount.Should().Be(0);
        _unitOfWork.RollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_WithVoucher_HoldsOneUseForTheOrder()
    {
        // Arrange
        AddVoucher("SALE10", 1_000_000m);
        var request = RequestFor(AddProduct(price: 10_000_000m));
        request.VoucherCode = "SALE10";

        // Act
        var result = await _service.CreateAsync(request, SalesA);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Payment.Amount.Should().Be(9_000_000m);
        _vouchers.CallLog.Should().Equal("reserve");
    }

    [Fact]
    public async Task CreateAsync_VoucherHasNoUseLeft_ReturnsConflictAndHoldsNothing()
    {
        // Arrange
        AddVoucher("SALE10", 1_000_000m);
        _vouchers.ReserveResult = new VoucherUseResult(VoucherUseOutcome.Refused, "Voucher has no uses left.");
        var request = RequestFor(AddProduct(price: 10_000_000m));
        request.VoucherCode = "SALE10";

        // Act
        var result = await _service.CreateAsync(request, SalesA);

        // Assert
        result.Kind.Should().Be(PosResultKind.Conflict);
        result.ConflictReason.Should().Contain("no uses left");
        _requests.Requests.Should().BeEmpty();
        _unitOfWork.CommitCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_SameKeyTwice_ReturnsTheStoredRequestWithoutHoldingAgain()
    {
        // Arrange
        var request = RequestFor(AddProduct());

        // Act
        var first = await _service.CreateAsync(request, SalesA);
        var second = await _service.CreateAsync(request, SalesA);

        // Assert
        second.Data!.Replayed.Should().BeTrue();
        second.Data.Payment.Id.Should().Be(first.Data!.Payment.Id);
        _orders.AddedOrders.Should().ContainSingle();
        _requests.Requests.Should().ContainSingle();
        _fulfillment.ReserveCallCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_ExpectedTotalDiffersFromServerTotal_ReturnsConflict()
    {
        // Arrange
        var request = RequestFor(AddProduct(price: 10_000_000m));
        request.ExpectedTotal = 9_000_000m;

        // Act
        var result = await _service.CreateAsync(request, SalesA);

        // Assert
        result.Kind.Should().Be(PosResultKind.Conflict);
        _orders.AddedOrders.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_TransferCodeCollision_RetriesInANewTransaction()
    {
        // Arrange
        _unitOfWork.SaveExceptionQueue.Enqueue(new DuplicateKeyException("idx_vietqr_tx_code"));
        _unitOfWork.OnRollback = () =>
        {
            _orders.Orders.Clear();
            _orders.AddedOrders.Clear();
            _requests.Requests.Clear();
        };

        // Act
        var result = await _service.CreateAsync(RequestFor(AddProduct()), SalesA);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _unitOfWork.BeginTransactionCount.Should().Be(2);
        _unitOfWork.CommitCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_EmptyCart_ReturnsValidationErrorAndStoresNothing()
    {
        // Arrange
        var request = RequestFor(AddProduct());
        request.Items = [];

        // Act
        var result = await _service.CreateAsync(request, SalesA);

        // Assert
        result.Kind.Should().Be(PosResultKind.ValidationFailed);
        _unitOfWork.BeginTransactionCount.Should().Be(0);
    }

    [Fact]
    public async Task GetAsync_OtherSalesStaff_ReturnsNotFoundButManagerCanRead()
    {
        // Arrange
        var created = (await _service.CreateAsync(RequestFor(AddProduct()), SalesA)).Data!.Payment;

        // Act
        var asOther = await _service.GetAsync(created.Id, SalesB);
        var asManager = await _service.GetAsync(created.Id, Manager);

        // Assert
        asOther.Kind.Should().Be(PosResultKind.NotFound);
        asManager.IsSuccess.Should().BeTrue();
        asManager.Data!.SecondsLeft.Should().Be(900);
    }

    [Fact]
    public async Task GetAsync_PendingQrPastItsDeadline_IsExpiredByTheRead()
    {
        // Arrange
        var order = NewOrder();
        var qr = _requests.AddRequest(order, _shift.Id, expiresAt: Now.AddMinutes(-1));

        // Act
        var result = await _service.GetAsync(qr.Id, Manager);

        // Assert
        _holds.Expired.Should().Equal(qr.Id);
        result.Data!.Status.Should().Be("Expired");
        result.Data.OrderStatus.Should().Be("Cancelled");
        result.Data.SecondsLeft.Should().Be(0);
    }

    [Fact]
    public async Task SearchAsync_SalesStaffSeeOnlyTheirOwnRequests()
    {
        // Arrange
        await _service.CreateAsync(RequestFor(AddProduct()), SalesA);
        var other = _requests.AddRequest(NewOrder(SalesB.Id), Guid.NewGuid());

        // Act
        var asA = await _service.SearchAsync(new VietQrSearchRequest(), SalesA);
        var asManager = await _service.SearchAsync(new VietQrSearchRequest(), Manager);

        // Assert
        asA.Data!.TotalCount.Should().Be(1);
        asA.Data.Items.Should().NotContain(p => p.Id == other.Id);
        asManager.Data!.TotalCount.Should().Be(2);
        asA.Data.Items.Should().OnlyContain(p => p.QrImagePng == null);
    }

    [Fact]
    public async Task SearchAsync_UnknownStatus_ReturnsValidationError()
    {
        // Act
        var result = await _service.SearchAsync(new VietQrSearchRequest { Status = "Paused" }, Manager);

        // Assert
        result.ValidationErrors.Should().ContainKey("status");
    }

    private Product AddProduct(decimal price = 10_000_000m)
    {
        var product = new Product { Name = "iPhone", Sku = "IP15", BasePrice = price, CostPrice = price / 2, IsSerialTracked = true, IsActive = true };
        _products.Products[product.Id] = product;
        return product;
    }

    private CreateVietQrRequest RequestFor(Product product) => new()
    {
        PosSessionId = _shift.Id,
        CheckoutKey = Guid.NewGuid(),
        Items = [new CreateOrderItemRequest { ProductId = product.Id, SerialImeiId = Guid.NewGuid() }]
    };

    private void AddVoucher(string code, decimal value)
    {
        var voucher = new TechStore.Api.Customers.Entities.Voucher
        {
            Code = code, DiscountType = VoucherType.FixedAmount, DiscountValue = value, UsageLimit = 1, ExpiresAt = Now.AddDays(30)
        };
        _voucherRepository.Vouchers[voucher.Id] = voucher;
    }

    private static TechStore.Api.Orders.Entities.Order NewOrder(Guid? cashier = null) => new()
    {
        OrderCode = "ORD-" + Guid.NewGuid().ToString("N")[..6],
        CashierUserId = cashier ?? SalesA.Id,
        PaymentMethod = PaymentMethod.VietQr,
        Subtotal = 1_000_000m,
        TotalAmount = 1_000_000m,
        CreatedAt = Now.AddMinutes(-20)
    };
}
