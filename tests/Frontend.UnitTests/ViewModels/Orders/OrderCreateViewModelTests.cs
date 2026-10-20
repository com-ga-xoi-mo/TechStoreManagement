using TechStore.Client.Models.Catalog;
using TechStore.Client.Models.Orders;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Dialog;
using TechStore.Client.Services.Navigation;
using TechStore.Client.ViewModels.Orders;
using TechStore.Shared.Enums;

namespace TechStore.Client.UnitTests.ViewModels.Orders;

public sealed class OrderCreateViewModelTests
{
    private sealed class Rig
    {
        public FakeOrderRepository Orders { get; } = new();
        public FakeDeviceRepository Devices { get; } = new();
        public FakeProductRepository Products { get; } = new();
        public FakeNavigation Navigation { get; } = new();
        public FakeDialogService Dialogs { get; } = new();
        public OrderCreateViewModel ViewModel { get; }

        public Rig() =>
            ViewModel = new OrderCreateViewModel(Orders, Devices, Products, Navigation, Dialogs);

        public async Task Select(ProductDetail product) =>
            await ViewModel.SelectProductCommand.ExecuteAsync(Catalog.Summary(product));

        public void Stock(ProductDetail product, ProductVariant? variant, params AvailableDevice[] devices) =>
            Devices.Pages[(product.Id, variant?.Id)] = new AvailableDevicePage(devices, devices.Length);

        /// <summary>Picks a serial-tracked product, its variant and one device, then adds the line.</summary>
        public async Task AddDevice(ProductDetail product, ProductVariant? variant, AvailableDevice device)
        {
            await Select(product);
            if (variant is not null) ViewModel.SelectedVariant = variant;
            ViewModel.SelectedDevice = ViewModel.Devices.Single(d => d.Id == device.Id);
            ViewModel.AddLineCommand.Execute(null);
        }

        public async Task AddQuantity(ProductDetail product, string quantity, string discount = "")
        {
            await Select(product);
            ViewModel.QuantityText = quantity;
            ViewModel.DiscountText = discount;
            ViewModel.AddLineCommand.Execute(null);
        }
    }

    private static async Task<Rig> CreateRig()
    {
        var rig = new Rig();
        await rig.ViewModel.ActivateAsync();
        return rig;
    }

    // ---------- finding a product ----------

    [Fact]
    public async Task SearchProducts_AsksOnlyForActiveProductsTrimmedAndCapped()
    {
        // Arrange
        var rig = await CreateRig();
        rig.Products.Add(Catalog.Product("iPhone 15 Pro Max"));
        rig.ViewModel.ProductSearchText = "  iphone  ";

        // Act
        await rig.ViewModel.SearchProductsCommand.ExecuteAsync(null);

        // Assert
        var query = Assert.Single(rig.Products.Queries);
        Assert.Equal("iphone", query.Query);
        Assert.True(query.IsActive);
        Assert.Equal(10, query.PageSize);
        Assert.Single(rig.ViewModel.ProductResults);
        Assert.False(rig.ViewModel.HasProductSearchMessage);
        Assert.False(rig.ViewModel.IsSearchingProducts);
    }

    [Fact]
    public async Task SearchProducts_WithNoMatch_ExplainsInsteadOfStayingBlank()
    {
        // Arrange
        var rig = await CreateRig();

        // Act
        await rig.ViewModel.SearchProductsCommand.ExecuteAsync(null);

        // Assert
        Assert.Empty(rig.ViewModel.ProductResults);
        Assert.True(rig.ViewModel.HasProductSearchMessage);
    }

    [Fact]
    public async Task SearchProducts_WhenMoreMatchThanShown_SaysSo()
    {
        // Arrange
        var rig = await CreateRig();
        rig.Products.OnSearch = query => Task.FromResult(new ProductPage(
            [Catalog.Summary(Catalog.Product())], 1, query.PageSize, 37, 4));

        // Act
        await rig.ViewModel.SearchProductsCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains("1 / 37", rig.ViewModel.ProductSearchMessage);
    }

    [Fact]
    public async Task SearchProducts_WhenTheApiFails_ClearsResultsAndReports()
    {
        // Arrange
        var rig = await CreateRig();
        rig.Products.OnSearch = _ => Task.FromException<ProductPage>(new ApiException(ApiErrorKind.Network, "down"));

        // Act
        await rig.ViewModel.SearchProductsCommand.ExecuteAsync(null);

        // Assert
        Assert.Empty(rig.ViewModel.ProductResults);
        Assert.Contains("Không thể tải", rig.ViewModel.ProductSearchMessage);
        Assert.False(rig.ViewModel.IsSearchingProducts);
    }

    // ---------- choosing a product ----------

    [Fact]
    public async Task SelectingAProductWithoutIdentifiers_ShowsQuantityAndNoDevicePicker()
    {
        // Arrange
        var rig = await CreateRig();
        var headphones = rig.Products.Add(Catalog.Product("Sony WH-1000XM5", serialTracked: false, basePrice: 500_000m));

        // Act
        await rig.Select(headphones);

        // Assert
        Assert.True(rig.ViewModel.HasProduct);
        Assert.True(rig.ViewModel.ShowQuantity);
        Assert.False(rig.ViewModel.ShowDevicePicker);
        Assert.False(rig.ViewModel.ShowVariantPicker);
        Assert.Empty(rig.Devices.Calls);
    }

    [Fact]
    public async Task SelectingASerialProductWithoutVariants_LoadsItsDevicesAtOnce()
    {
        // Arrange
        var rig = await CreateRig();
        var laptop = rig.Products.Add(Catalog.Product("Dell XPS 15"));
        rig.Stock(laptop, null, Catalog.Device("111111111111111"), Catalog.Device("222222222222222"));

        // Act
        await rig.Select(laptop);

        // Assert
        Assert.Equal([(laptop.Id, (Guid?)null)], rig.Devices.Calls);
        Assert.True(rig.ViewModel.ShowDevicePicker);
        Assert.False(rig.ViewModel.ShowQuantity);
        Assert.Equal(2, rig.ViewModel.Devices.Count);
    }

    [Fact]
    public async Task SerialProductWithSeveralVariants_NeedsAVariantBeforeDevicesAppear()
    {
        // Arrange
        var rig = await CreateRig();
        var small = Catalog.Variant("256GB", 30_000_000m);
        var large = Catalog.Variant("512GB", 35_000_000m);
        var phone = rig.Products.Add(Catalog.Product(variants: [small, large]));
        rig.Stock(phone, small, Catalog.Device("111111111111111"));
        rig.Stock(phone, large, Catalog.Device("222222222222222"), Catalog.Device("333333333333333"));
        await rig.Select(phone);

        // Assert before choosing
        Assert.True(rig.ViewModel.ShowVariantPicker);
        Assert.False(rig.ViewModel.ShowDevicePicker);
        Assert.Empty(rig.Devices.Calls);

        // Act
        rig.ViewModel.SelectedVariant = rig.ViewModel.Variants.Single(v => v.VariantName == "512GB");

        // Assert after choosing
        Assert.True(rig.ViewModel.ShowDevicePicker);
        Assert.Equal([(phone.Id, (Guid?)large.Id)], rig.Devices.Calls);
        Assert.Equal(2, rig.ViewModel.Devices.Count);
    }

    [Fact]
    public async Task OnlyOneActiveVariant_IsPickedAutomatically()
    {
        // Arrange
        var rig = await CreateRig();
        var only = Catalog.Variant("256GB", 30_000_000m);
        var retired = Catalog.Variant("128GB", 25_000_000m, active: false);
        var phone = rig.Products.Add(Catalog.Product(variants: [only, retired]));
        rig.Stock(phone, only, Catalog.Device("111111111111111"));

        // Act
        await rig.Select(phone);

        // Assert
        Assert.Equal(only.Id, rig.ViewModel.SelectedVariant?.Id);
        Assert.Equal([only.Id], rig.ViewModel.Variants.Select(v => v.Id));
        Assert.Single(rig.ViewModel.Devices);
    }

    [Fact]
    public async Task ProductWithOnlyRetiredVariants_CannotBeChosen()
    {
        // Arrange
        var rig = await CreateRig();
        var phone = rig.Products.Add(Catalog.Product(variants: [Catalog.Variant("128GB", 25_000_000m, active: false)]));

        // Act
        await rig.Select(phone);

        // Assert
        Assert.False(rig.ViewModel.HasProduct);
        Assert.Contains("biến thể", rig.ViewModel.LineError);
    }

    [Fact]
    public async Task InactiveOrMissingProduct_CannotBeChosen()
    {
        // Arrange
        var rig = await CreateRig();
        var inactive = rig.Products.Add(Catalog.Product(active: false));

        // Act / Assert: inactive
        await rig.Select(inactive);
        Assert.False(rig.ViewModel.HasProduct);
        Assert.Contains("ngừng kinh doanh", rig.ViewModel.LineError);

        // Act / Assert: gone
        await rig.ViewModel.SelectProductCommand.ExecuteAsync(Catalog.Summary(Catalog.Product()));
        Assert.False(rig.ViewModel.HasProduct);
        Assert.Contains("không còn tồn tại", rig.ViewModel.LineError);
    }

    [Fact]
    public async Task NoDevicesInStock_SaysSoAndRefusesTheLine()
    {
        // Arrange
        var rig = await CreateRig();
        var phone = rig.Products.Add(Catalog.Product());
        await rig.Select(phone);

        // Act
        rig.ViewModel.AddLineCommand.Execute(null);

        // Assert
        Assert.Contains("Không còn máy", rig.ViewModel.DeviceNotice);
        Assert.True(rig.ViewModel.HasLineError);
        Assert.Empty(rig.ViewModel.Lines);
    }

    [Fact]
    public async Task DeviceListCappedByTheApi_IsFlagged()
    {
        // Arrange
        var rig = await CreateRig();
        var phone = rig.Products.Add(Catalog.Product());
        rig.Devices.Pages[(phone.Id, null)] = new AvailableDevicePage([Catalog.Device("111111111111111")], 250);

        // Act
        await rig.Select(phone);

        // Assert
        Assert.Contains("1 / 250", rig.ViewModel.DeviceNotice);
    }

    [Fact]
    public async Task DeviceLookupFailure_ReportsWithoutBlockingTheRest()
    {
        // Arrange
        var rig = await CreateRig();
        var phone = rig.Products.Add(Catalog.Product());
        rig.Devices.Throw = new ApiException(ApiErrorKind.Network, "down");

        // Act
        await rig.Select(phone);

        // Assert
        Assert.Contains("Không thể tải", rig.ViewModel.DeviceNotice);
        Assert.Empty(rig.ViewModel.Devices);
        Assert.False(rig.ViewModel.IsLoadingDevices);
    }

    // ---------- the draft ----------

    [Fact]
    public async Task AddingASerialLine_UsesTheVariantPriceQuantityOneAndTheChosenDevice()
    {
        // Arrange
        var rig = await CreateRig();
        var variant = Catalog.Variant("256GB", 30_000_000m);
        var phone = rig.Products.Add(Catalog.Product(basePrice: 28_000_000m, variants: [variant, Catalog.Variant("512GB", 35_000_000m)]));
        var device = Catalog.Device("358912345678902");
        rig.Stock(phone, variant, device);

        // Act
        await rig.AddDevice(phone, variant, device);

        // Assert
        var item = Assert.Single(rig.ViewModel.Lines);
        Assert.Equal(30_000_000m, item.Line.UnitPrice);
        Assert.Equal(1, item.Line.Quantity);
        Assert.Equal(device.Id, item.Line.SerialImeiId);
        Assert.Equal("358912345678902", item.Line.Imei);
        Assert.Equal(variant.Id, item.Line.VariantId);
        Assert.Equal("iPhone 15 Pro Max · 256GB", item.Title);
        Assert.Contains("358912345678902", item.Detail);
        Assert.Equal("30.000.000 ₫", item.Total);
        Assert.False(rig.ViewModel.HasProduct);
        Assert.True(rig.ViewModel.CanSubmit);
    }

    [Fact]
    public async Task ADeviceAlreadyInTheDraft_IsNoLongerOffered()
    {
        // Arrange
        var rig = await CreateRig();
        var phone = rig.Products.Add(Catalog.Product());
        var first = Catalog.Device("111111111111111");
        var second = Catalog.Device("222222222222222");
        rig.Stock(phone, null, first, second);
        await rig.AddDevice(phone, null, first);

        // Act
        await rig.Select(phone);

        // Assert
        Assert.Equal([second.Id], rig.ViewModel.Devices.Select(d => d.Id));
    }

    [Fact]
    public async Task WhenEveryDeviceIsAlreadyInTheDraft_TheNoticeSaysSo_AndRemovingFreesOne()
    {
        // Arrange
        var rig = await CreateRig();
        var phone = rig.Products.Add(Catalog.Product());
        var only = Catalog.Device("111111111111111");
        rig.Stock(phone, null, only);
        await rig.AddDevice(phone, null, only);
        await rig.Select(phone);

        // Assert full
        Assert.Empty(rig.ViewModel.Devices);
        Assert.Contains("đã được thêm", rig.ViewModel.DeviceNotice);

        // Act
        rig.ViewModel.RemoveLineCommand.Execute(rig.ViewModel.Lines.Single());

        // Assert freed
        Assert.Equal([only.Id], rig.ViewModel.Devices.Select(d => d.Id));
        Assert.False(rig.ViewModel.HasDeviceNotice);
        Assert.Empty(rig.ViewModel.Lines);
    }

    [Fact]
    public async Task EstimatedTotal_SumsPriceTimesQuantityMinusDiscount()
    {
        // Arrange
        var rig = await CreateRig();
        var headphones = rig.Products.Add(Catalog.Product("Sony WH-1000XM5", serialTracked: false, basePrice: 500_000m));
        var phone = rig.Products.Add(Catalog.Product());
        var device = Catalog.Device("111111111111111");
        rig.Stock(phone, null, device);

        // Act
        await rig.AddQuantity(headphones, "2", "100.000");
        await rig.AddDevice(phone, null, device);

        // Assert: (2 x 500.000 - 100.000) + 30.000.000
        Assert.Equal(30_900_000m, rig.ViewModel.EstimatedTotal);
        Assert.Equal("30.900.000 ₫", rig.ViewModel.EstimatedTotalText);
        Assert.Equal("2 dòng", rig.ViewModel.LineCountText);
    }

    [Fact]
    public async Task AddingTheSameNonTrackedProductTwice_MergesIntoOneLine()
    {
        // Arrange
        var rig = await CreateRig();
        var cable = rig.Products.Add(Catalog.Product("Cáp USB-C", serialTracked: false, basePrice: 100_000m));

        // Act
        await rig.AddQuantity(cable, "2", "10000");
        await rig.AddQuantity(cable, "3", "5000");

        // Assert
        var line = Assert.Single(rig.ViewModel.Lines).Line;
        Assert.Equal(5, line.Quantity);
        Assert.Equal(15_000m, line.DiscountAmount);
        Assert.Equal(485_000m, rig.ViewModel.EstimatedTotal);
    }

    [Fact]
    public async Task MergeThatExceedsTheQuantityLimit_IsRefused()
    {
        // Arrange
        var rig = await CreateRig();
        var cable = rig.Products.Add(Catalog.Product("Cáp USB-C", serialTracked: false, basePrice: 1_000m));
        await rig.AddQuantity(cable, "9000");

        // Act
        await rig.AddQuantity(cable, "2000");

        // Assert
        Assert.Equal(9000, Assert.Single(rig.ViewModel.Lines).Line.Quantity);
        Assert.True(rig.ViewModel.HasLineError);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("10001")]
    public async Task InvalidQuantity_IsRefusedAtTheEditor(string quantity)
    {
        // Arrange
        var rig = await CreateRig();
        var cable = rig.Products.Add(Catalog.Product("Cáp USB-C", serialTracked: false, basePrice: 100_000m));

        // Act
        await rig.AddQuantity(cable, quantity);

        // Assert
        Assert.Empty(rig.ViewModel.Lines);
        Assert.Contains("Số lượng", rig.ViewModel.LineError);
        Assert.False(rig.ViewModel.CanSubmit);
    }

    [Theory]
    [InlineData("-1", "không âm")]
    [InlineData("abc", "không âm")]
    [InlineData("300.001", "không được lớn hơn")]
    public async Task InvalidDiscount_IsRefusedAtTheEditor(string discount, string expected)
    {
        // Arrange: 3 x 100.000 = 300.000
        var rig = await CreateRig();
        var cable = rig.Products.Add(Catalog.Product("Cáp USB-C", serialTracked: false, basePrice: 100_000m));

        // Act
        await rig.AddQuantity(cable, "3", discount);

        // Assert
        Assert.Empty(rig.ViewModel.Lines);
        Assert.Contains(expected, rig.ViewModel.LineError);
    }

    [Fact]
    public async Task DiscountEqualToTheLineAmount_IsAllowed()
    {
        // Arrange
        var rig = await CreateRig();
        var cable = rig.Products.Add(Catalog.Product("Cáp USB-C", serialTracked: false, basePrice: 100_000m));

        // Act
        await rig.AddQuantity(cable, "3", "300000");

        // Assert
        Assert.Equal(0m, rig.ViewModel.EstimatedTotal);
        Assert.Single(rig.ViewModel.Lines);
    }

    [Fact]
    public async Task AddingWithoutAProductOrVariant_ExplainsWhatIsMissing()
    {
        // Arrange
        var rig = await CreateRig();

        // Act / Assert: nothing chosen
        rig.ViewModel.AddLineCommand.Execute(null);
        Assert.Contains("chọn một sản phẩm", rig.ViewModel.LineError);

        // Act / Assert: variant missing
        var phone = rig.Products.Add(Catalog.Product(variants: [Catalog.Variant("256GB", 1m), Catalog.Variant("512GB", 2m)]));
        await rig.Select(phone);
        rig.ViewModel.AddLineCommand.Execute(null);
        Assert.Contains("biến thể", rig.ViewModel.LineError);
        Assert.Empty(rig.ViewModel.Lines);
    }

    [Fact]
    public async Task ADraftHoldsAtMostOneHundredLines()
    {
        // Arrange
        var rig = await CreateRig();
        for (var i = 0; i < OrderCreateViewModel.MaxLines; i++)
        {
            var product = rig.Products.Add(Catalog.Product($"Item {i}", serialTracked: false, basePrice: 1_000m));
            await rig.AddQuantity(product, "1");
        }

        Assert.Equal(OrderCreateViewModel.MaxLines, rig.ViewModel.Lines.Count);

        // Act
        await rig.AddQuantity(rig.Products.Add(Catalog.Product("One too many", serialTracked: false)), "1");

        // Assert
        Assert.Equal(OrderCreateViewModel.MaxLines, rig.ViewModel.Lines.Count);
        Assert.Contains("100", rig.ViewModel.LineError);
    }

    [Fact]
    public async Task RemovingALine_UpdatesTheTotalAndDisablesSubmitWhenEmpty()
    {
        // Arrange
        var rig = await CreateRig();
        var cable = rig.Products.Add(Catalog.Product("Cáp USB-C", serialTracked: false, basePrice: 100_000m));
        await rig.AddQuantity(cable, "2");
        Assert.Equal(200_000m, rig.ViewModel.EstimatedTotal);

        // Act
        rig.ViewModel.RemoveLineCommand.Execute(rig.ViewModel.Lines.Single());

        // Assert
        Assert.Equal(0m, rig.ViewModel.EstimatedTotal);
        Assert.False(rig.ViewModel.HasLines);
        Assert.False(rig.ViewModel.CanSubmit);
        Assert.False(rig.ViewModel.SaveDraftCommand.CanExecute(null));
    }

    // ---------- submitting ----------

    private static async Task<(Rig Rig, ProductDetail Phone, AvailableDevice Device)> RigWithOneDevice()
    {
        var rig = await CreateRig();
        var phone = rig.Products.Add(Catalog.Product());
        var device = Catalog.Device("358912345678902");
        rig.Stock(phone, null, device);
        await rig.AddDevice(phone, null, device);
        return (rig, phone, device);
    }

    [Fact]
    public async Task EmptyDraft_CannotBeSubmitted()
    {
        // Arrange
        var rig = await CreateRig();

        // Act
        await rig.ViewModel.SaveDraftCommand.ExecuteAsync(null);
        await rig.ViewModel.PayAndCompleteCommand.ExecuteAsync(null);

        // Assert
        Assert.Empty(rig.Orders.Created);
        Assert.False(rig.ViewModel.CanSubmit);
    }

    [Fact]
    public async Task SaveDraft_PostsTheRequestThenReturnsToTheListAndTellsTheUser()
    {
        // Arrange
        var (rig, phone, device) = await RigWithOneDevice();
        rig.ViewModel.Notes = "  Khách lấy hộp  ";
        rig.Orders.OnCreate = (_, _) => Task.FromResult(Orders.Detail(Guid.NewGuid(), code: "ORD-202610-0042"));

        // Act
        await rig.ViewModel.SaveDraftCommand.ExecuteAsync(null);

        // Assert
        var request = Assert.Single(rig.Orders.Created);
        Assert.Equal("Cash", request.PaymentMethod);
        Assert.Equal("Khách lấy hộp", request.Notes);
        Assert.Null(request.CustomerId);
        var item = Assert.Single(request.Items!);
        Assert.Equal(phone.Id, item.ProductId);
        Assert.Equal(device.Id, item.SerialImeiId);
        Assert.Equal(1, item.Quantity);
        Assert.Equal(0m, item.DiscountAmount);
        Assert.Empty(rig.Orders.Updates);
        Assert.Equal(AppRoute.Root(ShellDestination.Orders), Assert.Single(rig.Navigation.Routes));
        var notice = Assert.Single(rig.Dialogs.Requests);
        Assert.Contains("ORD-202610-0042", notice.Message);
        Assert.Contains("chờ xử lý", notice.Message);
        Assert.False(rig.ViewModel.IsSubmitting);
    }

    [Fact]
    public async Task PayAndComplete_CreatesThenCompletesThenReturnsToTheList()
    {
        // Arrange
        var (rig, _, _) = await RigWithOneDevice();
        rig.ViewModel.SelectedPayment = rig.ViewModel.PaymentOptions.Single(p => p.Method == PaymentMethod.VietQr);
        Guid createdId = default;
        rig.Orders.OnCreate = (_, _) =>
        {
            createdId = Guid.NewGuid();
            return Task.FromResult(Orders.Detail(createdId, code: "ORD-202610-0043"));
        };

        // Act
        await rig.ViewModel.PayAndCompleteCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("VietQr", Assert.Single(rig.Orders.Created).PaymentMethod);
        Assert.Equal((createdId, OrderStatus.Completed), Assert.Single(rig.Orders.Updates));
        Assert.Equal(AppRoute.Root(ShellDestination.Orders), Assert.Single(rig.Navigation.Routes));
        Assert.Contains("đã hoàn tất", Assert.Single(rig.Dialogs.Requests).Message);
    }

    [Fact]
    public async Task ValidationErrors_LandOnTheirLineOrTheGeneralArea_AndTheDraftIsKept()
    {
        // Arrange
        var (rig, phone, _) = await RigWithOneDevice();
        var second = Catalog.Device("999999999999999");
        rig.Stock(phone, null, second);
        await rig.AddDevice(phone, null, second);
        rig.Orders.OnCreate = (_, _) => Task.FromException<OrderDetail>(new ApiException(
            ApiErrorKind.Http, "400", 400, fieldErrors: new Dictionary<string, IReadOnlyList<string>>
            {
                ["items[1].serialImeiId"] = ["Device is Sold."],
                ["customerId"] = ["Customer was not found."]
            }));

        // Act
        await rig.ViewModel.SaveDraftCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(2, rig.ViewModel.Lines.Count);
        Assert.Null(rig.ViewModel.Lines[0].Error);
        Assert.Equal("Device is Sold.", rig.ViewModel.Lines[1].Error);
        Assert.True(rig.ViewModel.Lines[1].HasError);
        Assert.Equal("Customer was not found.", rig.ViewModel.GeneralError);
        Assert.Empty(rig.Navigation.Routes);
        Assert.True(rig.ViewModel.CanSubmit);
    }

    [Fact]
    public async Task LineErrorKeys_AreMatchedCaseInsensitively_AndOutOfRangeIndexesGoToTheGeneralArea()
    {
        // Arrange
        var (rig, _, _) = await RigWithOneDevice();
        rig.Orders.OnCreate = (_, _) => Task.FromException<OrderDetail>(new ApiException(
            ApiErrorKind.Http, "400", 400, fieldErrors: new Dictionary<string, IReadOnlyList<string>>
            {
                ["Items[0].Quantity"] = ["Quantity must be 1."],
                ["items[7].productId"] = ["Product is required."]
            }));

        // Act
        await rig.ViewModel.SaveDraftCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Quantity must be 1.", rig.ViewModel.Lines[0].Error);
        Assert.Equal("Product is required.", rig.ViewModel.GeneralError);
    }

    [Fact]
    public async Task ErrorsOnlyOnLines_GetAGeneralPointerToThem()
    {
        // Arrange
        var (rig, _, _) = await RigWithOneDevice();
        rig.Orders.OnCreate = (_, _) => Task.FromException<OrderDetail>(new ApiException(
            ApiErrorKind.Http, "400", 400, fieldErrors: new Dictionary<string, IReadOnlyList<string>>
            {
                ["items[0].serialImeiId"] = ["Device is Sold."]
            }));

        // Act
        await rig.ViewModel.SaveDraftCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains("từng dòng", rig.ViewModel.GeneralError);
    }

    [Fact]
    public async Task ErrorsFromAPreviousAttempt_AreClearedWhenSubmittingAgain()
    {
        // Arrange
        var (rig, _, _) = await RigWithOneDevice();
        var attempts = 0;
        rig.Orders.OnCreate = (_, _) => ++attempts == 1
            ? Task.FromException<OrderDetail>(new ApiException(ApiErrorKind.Http, "400", 400,
                fieldErrors: new Dictionary<string, IReadOnlyList<string>> { ["items[0].serialImeiId"] = ["Device is Sold."] }))
            : Task.FromResult(Orders.Detail(Guid.NewGuid()));
        await rig.ViewModel.SaveDraftCommand.ExecuteAsync(null);
        Assert.True(rig.ViewModel.Lines[0].HasError);

        // Act
        await rig.ViewModel.SaveDraftCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(2, rig.Orders.Created.Count);
        Assert.False(rig.ViewModel.HasGeneralError);
        Assert.Single(rig.Navigation.Routes);
    }

    [Fact]
    public async Task ConflictOnCreate_ShowsTheServerReasonAndKeepsTheDraft()
    {
        // Arrange
        var (rig, _, _) = await RigWithOneDevice();
        rig.Orders.OnCreate = (_, _) => Task.FromException<OrderDetail>(Orders.Http(409, "Device 358912345678902 is no longer available."));

        // Act
        await rig.ViewModel.SaveDraftCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Device 358912345678902 is no longer available.", rig.ViewModel.GeneralError);
        Assert.Single(rig.ViewModel.Lines);
        Assert.True(rig.ViewModel.CanSubmit);
        Assert.Empty(rig.Navigation.Routes);
    }

    [Fact]
    public async Task NetworkFailureOnCreate_KeepsTheDraftAndAllowsARetry()
    {
        // Arrange
        var (rig, _, _) = await RigWithOneDevice();
        rig.Orders.OnCreate = (_, _) => Task.FromException<OrderDetail>(new ApiException(ApiErrorKind.Network, "down"));

        // Act
        await rig.ViewModel.SaveDraftCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains("kết nối", rig.ViewModel.GeneralError);
        Assert.Single(rig.Orders.Created);
        Assert.True(rig.ViewModel.CanSubmit);
        Assert.False(rig.ViewModel.IsSubmitting);
    }

    [Fact]
    public async Task WhenCompletionFailsAfterCreate_TheOrderIsReportedAndSubmitStaysLocked()
    {
        // Arrange
        var (rig, _, _) = await RigWithOneDevice();
        rig.Orders.OnCreate = (_, _) => Task.FromResult(Orders.Detail(Guid.NewGuid(), code: "ORD-202610-0050"));
        rig.Orders.OnUpdate = (_, _, _) => Task.FromException<OrderDetail>(Orders.Http(409, "Device 358912345678902 is no longer available."));

        // Act
        await rig.ViewModel.PayAndCompleteCommand.ExecuteAsync(null);

        // Assert
        Assert.True(rig.ViewModel.HasPartialFailure);
        Assert.Contains("ORD-202610-0050", rig.ViewModel.PartialFailureMessage);
        Assert.Contains("is no longer available", rig.ViewModel.PartialFailureMessage);
        Assert.Equal("ORD-202610-0050", rig.ViewModel.CreatedOrderCode);
        Assert.False(rig.ViewModel.CanSubmit);
        Assert.Empty(rig.Navigation.Routes);
        Assert.Empty(rig.Dialogs.Requests);

        // Submitting again must not create a second order.
        await rig.ViewModel.SaveDraftCommand.ExecuteAsync(null);
        await rig.ViewModel.PayAndCompleteCommand.ExecuteAsync(null);
        Assert.Single(rig.Orders.Created);
    }

    [Fact]
    public async Task AfterAPartialFailure_TheDraftIsFrozenAndBackToListLeaves()
    {
        // Arrange
        var (rig, _, _) = await RigWithOneDevice();
        rig.Orders.OnUpdate = (_, _, _) => Task.FromException<OrderDetail>(Orders.Http(409));
        await rig.ViewModel.PayAndCompleteCommand.ExecuteAsync(null);
        var other = rig.Products.Add(Catalog.Product("Cáp", serialTracked: false, basePrice: 1_000m));

        // Act
        await rig.AddQuantity(other, "1");
        rig.ViewModel.RemoveLineCommand.Execute(rig.ViewModel.Lines.Single());
        rig.ViewModel.BackToListCommand.Execute(null);

        // Assert
        Assert.Single(rig.ViewModel.Lines);
        Assert.Contains("đã được tạo", rig.ViewModel.LineError);
        Assert.Equal(AppRoute.Root(ShellDestination.Orders), Assert.Single(rig.Navigation.Routes));
    }

    [Fact]
    public async Task OverlongNotes_AreRefusedBeforeAnythingIsSent()
    {
        // Arrange
        var (rig, _, _) = await RigWithOneDevice();
        rig.ViewModel.Notes = new string('x', OrderCreateViewModel.MaxNotesLength + 1);

        // Act
        await rig.ViewModel.SaveDraftCommand.ExecuteAsync(null);

        // Assert
        Assert.Empty(rig.Orders.Created);
        Assert.Contains("500", rig.ViewModel.GeneralError);
    }

    [Fact]
    public async Task WhileASubmitIsInFlight_AnotherCannotStart()
    {
        // Arrange
        var (rig, _, _) = await RigWithOneDevice();
        var pending = new TaskCompletionSource<OrderDetail>();
        rig.Orders.OnCreate = (_, _) => pending.Task;

        // Act
        var first = rig.ViewModel.SaveDraftCommand.ExecuteAsync(null);
        Assert.True(rig.ViewModel.IsSubmitting);
        Assert.False(rig.ViewModel.SaveDraftCommand.CanExecute(null));
        await rig.ViewModel.PayAndCompleteCommand.ExecuteAsync(null);
        pending.SetResult(Orders.Detail(Guid.NewGuid()));
        await first;

        // Assert
        Assert.Single(rig.Orders.Created);
        Assert.False(rig.ViewModel.IsSubmitting);
    }

    [Fact]
    public async Task ABrokenCompletionNotice_DoesNotUndoASavedOrder()
    {
        // Arrange
        var (rig, _, _) = await RigWithOneDevice();
        rig.Dialogs.Throw = new DialogUnavailableException("not ready");

        // Act
        await rig.ViewModel.SaveDraftCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(rig.Orders.Created);
        Assert.Single(rig.Navigation.Routes);
        Assert.False(rig.ViewModel.HasGeneralError);
    }
}
