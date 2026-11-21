using TechStore.Client.Models.Catalog;
using TechStore.Client.Models.Orders;
using TechStore.Client.Models.Sales;
using TechStore.Client.Repositories.Sales;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Session;
using TechStore.Client.UnitTests.ViewModels.Orders;
using CatalogFactory = TechStore.Client.UnitTests.ViewModels.Orders.Catalog;
using OrderFactory = TechStore.Client.UnitTests.ViewModels.Orders.Orders;
using TechStore.Client.ViewModels.Sales;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.ViewModels.Sales;

internal sealed class FakePosRepository : IPosRepository
{
    public PosShift? Current { get; set; }
    public List<PosCheckoutRequest> Checkouts { get; } = [];
    public List<(Guid Id, decimal Counted, string? Note)> Closed { get; } = [];
    public List<decimal> Opened { get; } = [];
    public Func<PosCheckoutRequest, CancellationToken, Task<OrderDetail>>? OnCheckout { get; set; }
    public Exception? ThrowOnOpen { get; set; }
    public Exception? ThrowOnClose { get; set; }

    public Task<PosShift?> GetCurrentShiftAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);

    public Task<PosShift> OpenShiftAsync(decimal openingBalance, CancellationToken cancellationToken = default)
    {
        if (ThrowOnOpen is not null) return Task.FromException<PosShift>(ThrowOnOpen);
        Opened.Add(openingBalance);
        return Task.FromResult(Current = PosShifts.Open(openingBalance));
    }

    public Task<PosShift> CloseShiftAsync(Guid shiftId, decimal countedCash, string? note, CancellationToken cancellationToken = default)
    {
        if (ThrowOnClose is not null) return Task.FromException<PosShift>(ThrowOnClose);
        Closed.Add((shiftId, countedCash, note));
        return Task.FromResult(Current! with { Status = PosSessionStatus.Closed });
    }

    public Func<PosCheckoutRequest, Task<PosQuoteDto>> Price { get; set; } = null!;
    public Task<PosQuoteDto> QuoteAsync(PosCheckoutRequest request, CancellationToken cancellationToken = default) => Price(request);

    public Task<OrderDetail> CheckoutAsync(PosCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        Checkouts.Add(request);
        return OnCheckout is not null
            ? OnCheckout(request, cancellationToken)
            : Task.FromResult(OrderFactory.Detail(Guid.NewGuid(), OrderStatus.Completed));
    }
}

internal static class PosShifts
{
    public static PosShift Open(decimal opening = 1_000_000m, decimal cashSales = 0m) => new(
        Guid.NewGuid(), Guid.NewGuid(), "Thu ngân", PosSessionStatus.Open, opening, cashSales, opening + cashSales,
        null, null, null, new DateTime(2026, 11, 17, 1, 0, 0, DateTimeKind.Utc), null);
}

public sealed class PosViewModelTests
{
    private sealed class Rig
    {
        public FakePosRepository Pos { get; } = new();
        public FakeDeviceRepository Devices { get; } = new();
        public FakeProductRepository Products { get; } = new();
        public InMemorySessionStore Session { get; } = new();
        public PosDraftStore Drafts { get; }
        public PosViewModel ViewModel { get; }

        public Rig()
        {
            Pos.Price = request =>
            {
                var subtotal = request.Items!.Sum(i =>
                {
                    var product = Products.Details[i.ProductId!.Value];
                    var variant = product.Variants.FirstOrDefault(v => v.Id == i.VariantId);
                    return (variant?.Price ?? product.BasePrice) * i.Quantity!.Value;
                });
                var discount = request.OrderDiscountAmount ?? 0m;
                return Task.FromResult(new PosQuoteDto(subtotal, discount, 0, subtotal - discount));
            };
            Drafts = new PosDraftStore(Session);
            ViewModel = new PosViewModel(Pos, Devices, Products, Drafts);
        }

        /// <summary>A new page instance, as the shell creates when the cashier comes back to the POS route.</summary>
        public async Task<PosViewModel> Reopen()
        {
            ViewModel.Deactivate();
            var next = new PosViewModel(Pos, Devices, Products, Drafts);
            await next.ActivateAsync();
            return next;
        }

        public async Task Scan(string code)
        {
            ViewModel.ScanText = code;
            await ViewModel.ScanCommand.ExecuteAsync(null);
        }

        /// <summary>A serial-tracked phone with one in-stock device that can be scanned by its IMEI.</summary>
        public (ProductDetail Product, AvailableDevice Device) Phone(string imei, decimal price = 10_000_000m, ProductVariant? variant = null)
        {
            var product = Products.Add(CatalogFactory.Product("iPhone", serialTracked: true, basePrice: price, variants: variant is null ? null : [variant]));
            var device = CatalogFactory.Device(imei);
            Devices.Scans[imei] = new ScannedDevice(device, product.Id, variant?.Id);
            return (product, device);
        }

        public ProductDetail Accessory(string name = "Op lung", decimal price = 200_000m)
        {
            var product = Products.Add(CatalogFactory.Product(name, serialTracked: false, basePrice: price));
            Products.OnSearch = query => Task.FromResult(new ProductPage(
                Products.Details.Values.Where(p => p.Sku == query.ExactCode).Select(CatalogFactory.Summary).ToArray(), 1, 10,
                Products.Details.Values.Count(p => p.Sku == query.ExactCode), 1));
            return product;
        }
    }

    private static async Task<Rig> OpenRig(decimal opening = 1_000_000m)
    {
        var rig = new Rig();
        rig.Pos.Current = PosShifts.Open(opening);
        await rig.ViewModel.ActivateAsync();
        return rig;
    }

    [Fact]
    public async Task Scan_PartialSkuFromSearch_CannotAddAnItem()
    {
        var rig = await OpenRig();
        var item = rig.Accessory();
        rig.Products.OnSearch = _ => Task.FromResult(new ProductPage([CatalogFactory.Summary(item)], 1, 10, 1, 1));
        await rig.Scan(item.Sku[..^1]);
        Assert.Empty(rig.ViewModel.Lines);
    }

    [Fact]
    public async Task Pay_CurrentQuoteWithVat_DeterminesEligibilityChangeAndExpectedTotal()
    {
        var rig = await OpenRig();
        var item = rig.Accessory(price: 100_000m);
        var first = new TaskCompletionSource<PosQuoteDto>();
        rig.Pos.Price = _ => first.Task;
        await rig.Scan(item.Sku);
        rig.ViewModel.CashText = "100000";
        Assert.False(rig.ViewModel.CanPay);
        rig.Pos.Price = request => Task.FromResult(new PosQuoteDto(100_000m, request.OrderDiscountAmount!.Value, 9_500m, 104_500m));
        rig.ViewModel.DiscountText = "5000";
        first.SetResult(new(100_000m, 0m, 10_000m, 110_000m));
        Assert.False(rig.ViewModel.CanPay);
        rig.ViewModel.CashText = "120000";
        Assert.Equal(15_500m, rig.ViewModel.Change);
        await rig.ViewModel.PayCommand.ExecuteAsync(null);
        var sent = Assert.Single(rig.Pos.Checkouts);
        Assert.Equal(104_500m, sent.ExpectedTotal);
        Assert.Equal(5_000m, sent.OrderDiscountAmount);
    }

    // ---------- shift ----------

    [Fact]
    public async Task Activate_WithoutOpenShift_AsksToOpenOne()
    {
        // Arrange
        var rig = new Rig();

        // Act
        await rig.ViewModel.ActivateAsync();

        // Assert
        Assert.True(rig.ViewModel.IsContent);
        Assert.True(rig.ViewModel.NoShift);
        Assert.False(rig.ViewModel.CanEditCart);
        Assert.False(rig.ViewModel.ScanCommand.CanExecute(null));
    }

    [Fact]
    public async Task OpenShift_WithValidAmount_ShowsTheShiftAndEnablesSelling()
    {
        // Arrange
        var rig = new Rig();
        await rig.ViewModel.ActivateAsync();
        rig.ViewModel.OpeningText = "2.000.000";

        // Act
        await rig.ViewModel.OpenShiftCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal([2_000_000m], rig.Pos.Opened);
        Assert.True(rig.ViewModel.HasShift);
        Assert.True(rig.ViewModel.CanEditCart);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("1,005")]
    public async Task OpenShift_WithInvalidAmount_CannotBeSubmitted(string text)
    {
        // Arrange
        var rig = new Rig();
        await rig.ViewModel.ActivateAsync();

        // Act
        rig.ViewModel.OpeningText = text;

        // Assert
        Assert.False(rig.ViewModel.OpenShiftCommand.CanExecute(null));
    }

    [Fact]
    public async Task OpenShift_WhenServerRefuses_ShowsTheServerReason()
    {
        // Arrange
        var rig = new Rig();
        rig.Pos.ThrowOnOpen = OrderFactory.Http(409, "You already have an open shift.");
        await rig.ViewModel.ActivateAsync();
        rig.ViewModel.OpeningText = "0";

        // Act
        await rig.ViewModel.OpenShiftCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("You already have an open shift.", rig.ViewModel.ShiftError);
        Assert.True(rig.ViewModel.NoShift);
    }

    [Fact]
    public async Task CloseShift_WithDifferenceNeedsANoteAndShowsTheDifference()
    {
        // Arrange
        var rig = await OpenRig(opening: 1_000_000m);
        rig.ViewModel.BeginCloseShiftCommand.Execute(null);
        rig.ViewModel.CountedCashText = "950000";

        // Assert
        Assert.True(rig.ViewModel.IsClosingShift);
        Assert.True(rig.ViewModel.NeedsClosingNote);
        Assert.Contains("−50.000", rig.ViewModel.DiscrepancyText);
        Assert.False(rig.ViewModel.ConfirmCloseShiftCommand.CanExecute(null));

        // Act
        rig.ViewModel.ClosingNote = "Tra nham tien thua";

        // Assert
        Assert.True(rig.ViewModel.ConfirmCloseShiftCommand.CanExecute(null));
    }

    [Fact]
    public async Task CloseShift_WithExactCash_SendsNoNoteAndLeavesNoShift()
    {
        // Arrange
        var rig = await OpenRig(opening: 1_000_000m);
        var shiftId = rig.ViewModel.Shift!.Id;
        rig.ViewModel.BeginCloseShiftCommand.Execute(null);
        rig.ViewModel.CountedCashText = "1000000";
        rig.ViewModel.ClosingNote = "ignored";

        // Act
        await rig.ViewModel.ConfirmCloseShiftCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal([(shiftId, 1_000_000m, (string?)null)], rig.Pos.Closed);
        Assert.True(rig.ViewModel.NoShift);
        Assert.False(rig.ViewModel.IsClosingShift);
    }

    [Fact]
    public async Task CloseShift_WithItemsStillInTheCart_DoesNotStart()
    {
        // Arrange
        var rig = await OpenRig();
        rig.Phone("358912345678902");
        await rig.Scan("358912345678902");

        // Act
        rig.ViewModel.BeginCloseShiftCommand.Execute(null);

        // Assert
        Assert.False(rig.ViewModel.IsClosingShift);
    }

    [Fact]
    public async Task CloseShift_WhenServerRefuses_KeepsTheDialogOpenWithTheReason()
    {
        // Arrange
        var rig = await OpenRig();
        rig.Pos.ThrowOnClose = OrderFactory.Http(409, "The shift is already closed.");
        rig.ViewModel.BeginCloseShiftCommand.Execute(null);
        rig.ViewModel.CountedCashText = "1000000";

        // Act
        await rig.ViewModel.ConfirmCloseShiftCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("The shift is already closed.", rig.ViewModel.ShiftError);
        Assert.True(rig.ViewModel.IsClosingShift);
        Assert.True(rig.ViewModel.HasShift);
    }

    // ---------- scanning ----------

    [Fact]
    public async Task Scan_ImeiOfAnInStockDevice_AddsThatDeviceAtTheCatalogPrice()
    {
        // Arrange
        var rig = await OpenRig();
        var (product, device) = rig.Phone("358912345678902", price: 12_500_000m);

        // Act
        await rig.Scan("358912345678902");

        // Assert
        var line = Assert.Single(rig.ViewModel.Lines).Line;
        Assert.Equal(product.Id, line.ProductId);
        Assert.Equal(device.Id, line.SerialImeiId);
        Assert.Equal(12_500_000m, line.UnitPrice);
        Assert.Equal(string.Empty, rig.ViewModel.ScanText);
        Assert.False(rig.ViewModel.HasScanMessage);
        Assert.Equal(12_500_000m, rig.ViewModel.EstimatedTotal);
    }

    [Fact]
    public async Task Scan_ImeiOfAVariant_UsesTheVariantPriceAndSku()
    {
        // Arrange
        var rig = await OpenRig();
        var variant = CatalogFactory.Variant("256GB", 15_000_000m);
        rig.Phone("358912345678902", price: 10_000_000m, variant: variant);

        // Act
        await rig.Scan("358912345678902");

        // Assert
        var line = Assert.Single(rig.ViewModel.Lines).Line;
        Assert.Equal(variant.Id, line.VariantId);
        Assert.Equal(15_000_000m, line.UnitPrice);
    }

    [Fact]
    public async Task Scan_SameImeiTwice_IsRefusedAndTheCartKeepsOneLine()
    {
        // Arrange
        var rig = await OpenRig();
        rig.Phone("358912345678902");
        await rig.Scan("358912345678902");

        // Act
        await rig.Scan("358912345678902");

        // Assert
        Assert.Single(rig.ViewModel.Lines);
        Assert.Equal("Máy này đã có trong giỏ.", rig.ViewModel.ScanMessage);
    }

    [Fact]
    public async Task Scan_SkuOfAnAccessory_AddsOneAndAScanAgainRaisesTheQuantity()
    {
        // Arrange
        var rig = await OpenRig();
        var product = rig.Accessory(price: 200_000m);

        // Act
        await rig.Scan(product.Sku);
        await rig.Scan(product.Sku);

        // Assert
        var line = Assert.Single(rig.ViewModel.Lines).Line;
        Assert.Equal(2, line.Quantity);
        Assert.Equal(400_000m, rig.ViewModel.EstimatedTotal);
    }

    [Fact]
    public async Task Scan_SkuOfASerialTrackedProduct_AsksForTheImeiInstead()
    {
        // Arrange
        var rig = await OpenRig();
        var phone = CatalogFactory.Product("iPhone", serialTracked: true);
        rig.Products.Add(phone);
        rig.Products.OnSearch = _ => Task.FromResult(new ProductPage([CatalogFactory.Summary(phone)], 1, 10, 1, 1));

        // Act
        await rig.Scan(phone.Sku);

        // Assert
        Assert.Empty(rig.ViewModel.Lines);
        Assert.Contains("IMEI", rig.ViewModel.ScanMessage);
    }

    [Fact]
    public async Task Scan_ProductWithSeveralVariantsAndAGenericCode_AsksForTheVariantCode()
    {
        // Arrange
        var rig = await OpenRig();
        var product = rig.Products.Add(CatalogFactory.Product("Op lung", serialTracked: false,
            variants: [CatalogFactory.Variant("Den", 100_000m), CatalogFactory.Variant("Trang", 100_000m)]));
        rig.Products.OnSearch = _ => Task.FromResult(new ProductPage([CatalogFactory.Summary(product)], 1, 10, 1, 1));

        // Act
        await rig.Scan(product.Sku);

        // Assert
        Assert.Empty(rig.ViewModel.Lines);
        Assert.Contains("biến thể", rig.ViewModel.ScanMessage);
    }

    [Fact]
    public async Task Scan_UnknownCode_SaysNothingWasFound()
    {
        // Arrange
        var rig = await OpenRig();
        rig.Products.OnSearch = _ => Task.FromResult(new ProductPage([], 1, 10, 0, 0));

        // Act
        await rig.Scan("0000");

        // Assert
        Assert.Empty(rig.ViewModel.Lines);
        Assert.True(rig.ViewModel.HasScanMessage);
        Assert.Equal("0000", rig.ViewModel.ScanText);
    }

    [Fact]
    public async Task Scan_WhenLookupFails_ShowsAMessageInsteadOfThrowing()
    {
        // Arrange
        var rig = await OpenRig();
        rig.Devices.Throw = new ApiException(ApiErrorKind.Network, "offline");

        // Act
        await rig.Scan("358912345678902");

        // Assert
        Assert.True(rig.ViewModel.HasScanMessage);
        Assert.Empty(rig.ViewModel.Lines);
        Assert.False(rig.ViewModel.IsScanning);
    }

    // ---------- cart ----------

    [Fact]
    public async Task Cart_QuantityButtonsChangeAnAccessoryButNeverBelowOneOrOnADevice()
    {
        // Arrange
        var rig = await OpenRig();
        var product = rig.Accessory(price: 100_000m);
        rig.Phone("358912345678902");
        await rig.Scan(product.Sku);
        await rig.Scan("358912345678902");
        var accessory = rig.ViewModel.Lines[0];
        var device = rig.ViewModel.Lines[1];

        // Act
        rig.ViewModel.IncreaseQuantityCommand.Execute(accessory);
        rig.ViewModel.IncreaseQuantityCommand.Execute(rig.ViewModel.Lines[0]);
        rig.ViewModel.DecreaseQuantityCommand.Execute(rig.ViewModel.Lines[0]);
        rig.ViewModel.IncreaseQuantityCommand.Execute(device);

        // Assert
        Assert.Equal(2, rig.ViewModel.Lines[0].Line.Quantity);
        Assert.Equal(1, rig.ViewModel.Lines[1].Line.Quantity);
        rig.ViewModel.DecreaseQuantityCommand.Execute(rig.ViewModel.Lines[0]);
        rig.ViewModel.DecreaseQuantityCommand.Execute(rig.ViewModel.Lines[0]);
        Assert.Equal(1, rig.ViewModel.Lines[0].Line.Quantity);
    }

    [Fact]
    public async Task Cart_RemoveLine_DropsItAndIgnoresNonLineParameters()
    {
        // Arrange
        var rig = await OpenRig();
        rig.Phone("358912345678902");
        await rig.Scan("358912345678902");

        // Act
        rig.ViewModel.RemoveLineCommand.Execute("not a line");
        Assert.Single(rig.ViewModel.Lines);
        rig.ViewModel.RemoveLineCommand.Execute(rig.ViewModel.Lines[0]);

        // Assert
        Assert.Empty(rig.ViewModel.Lines);
        Assert.False(rig.ViewModel.HasLines);
    }

    // ---------- paying ----------

    [Theory]
    [InlineData("9999999", false)]
    [InlineData("10000000", true)]
    [InlineData("10.500.000", true)]
    [InlineData("abc", false)]
    [InlineData("", false)]
    public async Task Pay_IsEnabledOnlyWhenTheCashCoversTheTotal(string cash, bool expected)
    {
        // Arrange
        var rig = await OpenRig();
        rig.Phone("358912345678902", price: 10_000_000m);
        await rig.Scan("358912345678902");

        // Act
        rig.ViewModel.CashText = cash;

        // Assert
        Assert.Equal(expected, rig.ViewModel.CanPay);
        Assert.Equal(expected, rig.ViewModel.PayCommand.CanExecute(null));
    }

    [Fact]
    public async Task Pay_ShowsTheChangeFromTheCashReceived()
    {
        // Arrange
        var rig = await OpenRig();
        rig.Phone("358912345678902", price: 9_500_000m);
        await rig.Scan("358912345678902");

        // Act
        rig.ViewModel.CashText = "10000000";

        // Assert
        Assert.Equal(500_000m, rig.ViewModel.Change);
    }

    [Fact]
    public async Task Pay_Success_SendsOnlyIdsAndCashShowsTheReceiptAndEmptiesTheCart()
    {
        // Arrange
        var rig = await OpenRig();
        var shiftId = rig.ViewModel.Shift!.Id;
        var (product, device) = rig.Phone("358912345678902", price: 10_000_000m);
        await rig.Scan("358912345678902");
        rig.ViewModel.CashText = "10000000";

        // Act
        await rig.ViewModel.PayCommand.ExecuteAsync(null);

        // Assert
        var request = Assert.Single(rig.Pos.Checkouts);
        Assert.Equal(shiftId, request.PosSessionId);
        Assert.NotNull(request.CheckoutKey);
        Assert.Equal(10_000_000m, request.CashTendered);
        var item = Assert.Single(request.Items!);
        Assert.Equal(product.Id, item.ProductId);
        Assert.Equal(device.Id, item.SerialImeiId);
        Assert.True(rig.ViewModel.HasReceipt);
        Assert.Empty(rig.ViewModel.Lines);
        Assert.Equal(string.Empty, rig.ViewModel.CashText);
        Assert.False(rig.ViewModel.HasPayError);
    }

    [Fact]
    public async Task Pay_PressedTwiceWhileRunning_SendsOneRequest()
    {
        // Arrange
        var rig = await OpenRig();
        rig.Phone("358912345678902");
        await rig.Scan("358912345678902");
        rig.ViewModel.CashText = "99999999";
        var gate = new TaskCompletionSource<OrderDetail>();
        rig.Pos.OnCheckout = (_, _) => gate.Task;

        // Act
        var first = rig.ViewModel.PayCommand.ExecuteAsync(null);
        var secondAllowed = rig.ViewModel.PayCommand.CanExecute(null);
        gate.SetResult(OrderFactory.Detail(Guid.NewGuid(), OrderStatus.Completed));
        await first;

        // Assert
        Assert.False(secondAllowed);
        Assert.Single(rig.Pos.Checkouts);
    }

    [Fact]
    public async Task Pay_AfterALostConnection_KeepsTheCartLocksItAndRetriesWithTheSameKey()
    {
        // Arrange
        var rig = await OpenRig();
        rig.Phone("358912345678902");
        await rig.Scan("358912345678902");
        rig.ViewModel.CashText = "99999999";
        rig.Pos.OnCheckout = (_, _) => Task.FromException<OrderDetail>(new ApiException(ApiErrorKind.Timeout, "timeout"));

        // Act
        await rig.ViewModel.PayCommand.ExecuteAsync(null);

        // Assert: nothing is stored locally, the cart cannot change, and the message says why
        Assert.True(rig.ViewModel.AwaitingRetry);
        Assert.False(rig.ViewModel.CanEditCart);
        Assert.False(rig.ViewModel.ScanCommand.CanExecute(null));
        Assert.Single(rig.ViewModel.Lines);
        Assert.True(rig.ViewModel.HasPayError);
        var firstKey = rig.Pos.Checkouts[0].CheckoutKey;

        // Act: the retry reaches the server
        rig.Pos.OnCheckout = null;
        await rig.ViewModel.PayCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(firstKey, rig.Pos.Checkouts[1].CheckoutKey);
        Assert.False(rig.ViewModel.AwaitingRetry);
        Assert.True(rig.ViewModel.HasReceipt);
    }

    [Fact]
    public async Task Pay_RefusedByTheServer_ShowsTheReasonAndUsesANewKeyNextTime()
    {
        // Arrange
        var rig = await OpenRig();
        rig.Phone("358912345678902");
        await rig.Scan("358912345678902");
        rig.ViewModel.CashText = "99999999";
        rig.Pos.OnCheckout = (_, _) => Task.FromException<OrderDetail>(OrderFactory.Http(409, "Line 1: Device is not in stock."));

        // Act
        await rig.ViewModel.PayCommand.ExecuteAsync(null);
        await rig.ViewModel.PayCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Line 1: Device is not in stock.", rig.ViewModel.PayError);
        Assert.False(rig.ViewModel.AwaitingRetry);
        Assert.True(rig.ViewModel.CanEditCart);
        Assert.NotEqual(rig.Pos.Checkouts[0].CheckoutKey, rig.Pos.Checkouts[1].CheckoutKey);
        Assert.False(rig.ViewModel.HasReceipt);
    }

    [Fact]
    public async Task Pay_WithLineErrorsFromTheServer_MarksTheLines()
    {
        // Arrange
        var rig = await OpenRig();
        rig.Phone("358912345678902");
        await rig.Scan("358912345678902");
        rig.ViewModel.CashText = "99999999";
        rig.Pos.OnCheckout = (_, _) => Task.FromException<OrderDetail>(new ApiException(
            ApiErrorKind.Http, "HTTP 400", 400,
            fieldErrors: new Dictionary<string, IReadOnlyList<string>> { ["items[0].serialImeiId"] = ["Device is sold."] }));

        // Act
        await rig.ViewModel.PayCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Device is sold.", rig.ViewModel.Lines[0].Error);
        Assert.True(rig.ViewModel.HasPayError);
    }

    [Fact]
    public async Task Pay_AfterEditingTheCartFollowingARefusal_NeverReusesTheOldKey()
    {
        // Arrange
        var rig = await OpenRig();
        rig.Phone("358912345678902");
        rig.Phone("358912345678903");
        await rig.Scan("358912345678902");
        rig.ViewModel.CashText = "99999999";
        rig.Pos.OnCheckout = (_, _) => Task.FromException<OrderDetail>(OrderFactory.Http(409, "busy"));
        await rig.ViewModel.PayCommand.ExecuteAsync(null);

        // Act
        await rig.Scan("358912345678903");
        rig.Pos.OnCheckout = null;
        await rig.ViewModel.PayCommand.ExecuteAsync(null);

        // Assert
        Assert.NotEqual(rig.Pos.Checkouts[0].CheckoutKey, rig.Pos.Checkouts[1].CheckoutKey);
        Assert.Equal(2, rig.Pos.Checkouts[1].Items!.Count);
    }

    [Fact]
    public async Task Pay_Success_RefreshesTheShiftCashTotal()
    {
        // Arrange
        var rig = await OpenRig(opening: 1_000_000m);
        rig.Phone("358912345678902", price: 2_000_000m);
        await rig.Scan("358912345678902");
        rig.ViewModel.CashText = "2000000";
        rig.Pos.OnCheckout = (_, _) =>
        {
            rig.Pos.Current = rig.Pos.Current! with { CashSalesTotal = 2_000_000m, ExpectedCash = 3_000_000m };
            return Task.FromResult(OrderFactory.Detail(Guid.NewGuid(), OrderStatus.Completed));
        };

        // Act
        await rig.ViewModel.PayCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(3_000_000m, rig.ViewModel.Shift!.ExpectedCash);
    }

    // ---------- leaving and coming back ----------

    [Fact]
    public async Task Reopen_AfterLeavingWithACart_RestoresTheCartTheCashAndTheKey()
    {
        // Arrange
        var rig = await OpenRig();
        rig.Phone("358912345678902", price: 2_000_000m);
        await rig.Scan("358912345678902");
        rig.ViewModel.CashText = "3000000";
        rig.Pos.OnCheckout = (_, _) => Task.FromException<OrderDetail>(new ApiException(ApiErrorKind.Timeout, "timeout"));
        await rig.ViewModel.PayCommand.ExecuteAsync(null);
        var key = rig.Pos.Checkouts[0].CheckoutKey;

        // Act
        var next = await rig.Reopen();

        // Assert: the unknown outcome and its key survive, so the retry still cannot sell twice
        Assert.Single(next.Lines);
        Assert.Equal("3000000", next.CashText);
        Assert.True(next.AwaitingRetry);
        Assert.False(next.CanEditCart);
        rig.Pos.OnCheckout = null;
        await next.PayCommand.ExecuteAsync(null);
        Assert.Equal(key, rig.Pos.Checkouts[1].CheckoutKey);
        Assert.True(next.HasReceipt);
    }

    [Fact]
    public async Task Reopen_WhileClosingTheShift_RestoresThePanelAndWhatWasTyped()
    {
        // Arrange
        var rig = await OpenRig(opening: 1_000_000m);
        rig.ViewModel.BeginCloseShiftCommand.Execute(null);
        rig.ViewModel.CountedCashText = "990000";
        rig.ViewModel.ClosingNote = "Thieu 10k";

        // Act
        var next = await rig.Reopen();

        // Assert
        Assert.True(next.IsClosingShift);
        Assert.Equal("990000", next.CountedCashText);
        Assert.Equal("Thieu 10k", next.ClosingNote);
        Assert.False(next.ShowSale);
    }

    [Fact]
    public async Task Reopen_WithAnEmptyCart_StartsClean()
    {
        // Arrange
        var rig = await OpenRig();

        // Act
        var next = await rig.Reopen();

        // Assert
        Assert.False(next.HasLines);
        Assert.False(next.IsClosingShift);
        Assert.Equal(string.Empty, next.CashText);
    }

    [Fact]
    public async Task Reopen_OnAnotherLogin_DoesNotShowThePreviousCashiersCart()
    {
        // Arrange
        var rig = await OpenRig();
        rig.Phone("358912345678902");
        await rig.Scan("358912345678902");
        rig.ViewModel.Deactivate();
        rig.Session.SetSession("token", "refresh", "warehouse1", "Thu kho", "WarehouseStaff");

        // Act
        var next = new PosViewModel(rig.Pos, rig.Devices, rig.Products, rig.Drafts);
        await next.ActivateAsync();

        // Assert
        Assert.False(next.HasLines);
    }

    [Fact]
    public async Task Reopen_OnADifferentShift_DropsTheDraft()
    {
        // Arrange
        var rig = await OpenRig();
        rig.Phone("358912345678902");
        await rig.Scan("358912345678902");
        rig.ViewModel.Deactivate();
        rig.Pos.Current = PosShifts.Open(500_000m);

        // Act
        var next = new PosViewModel(rig.Pos, rig.Devices, rig.Products, rig.Drafts);
        await next.ActivateAsync();

        // Assert
        Assert.False(next.HasLines);
    }

    [Fact]
    public async Task Reopen_EachVisitKeepsTheCartUntilItIsSoldOrCleared()
    {
        // Arrange
        var rig = await OpenRig();
        rig.Phone("358912345678902");
        await rig.Scan("358912345678902");
        var first = await rig.Reopen();
        first.Deactivate();

        // Act
        var second = new PosViewModel(rig.Pos, rig.Devices, rig.Products, rig.Drafts);
        await second.ActivateAsync();

        // Assert: the second visit saw the cart saved by the first one, which restored it and saved it again on leaving
        Assert.Single(second.Lines);
        second.Deactivate();
        var third = new PosViewModel(rig.Pos, rig.Devices, rig.Products, rig.Drafts);
        await third.ActivateAsync();
        Assert.Single(third.Lines);
    }
}
