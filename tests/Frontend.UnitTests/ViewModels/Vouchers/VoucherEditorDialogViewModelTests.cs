using TechStore.Client.Models.Vouchers;
using TechStore.Client.Repositories.Vouchers;
using TechStore.Client.Services.Api;
using TechStore.Client.ViewModels.Vouchers;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.ViewModels.Vouchers;

public sealed class VoucherEditorDialogViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset NextYear = new(2027, 1, 15, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Initialize_New_ShowsEditableCreateForm()
    {
        // Arrange
        var viewModel = Create(new FakeVouchers());

        // Act
        viewModel.Initialize(null);

        // Assert
        Assert.False(viewModel.IsEdit);
        Assert.Equal("Tạo voucher", viewModel.Title);
        Assert.Equal("Lưu voucher", viewModel.SaveButtonText);
        Assert.True(viewModel.IsCodeEditable);
        Assert.True(viewModel.IsTypeEditable);
        Assert.True(viewModel.IsPercentage);
        Assert.True(viewModel.ShowMaxDiscount);
        Assert.Equal(string.Empty, viewModel.Code);
        Assert.Null(viewModel.ExpiresAt);
    }

    [Fact]
    public void Initialize_Edit_FillsFieldsAndLocksCodeAndType()
    {
        // Arrange
        var viewModel = Create(new FakeVouchers());
        var voucher = Voucher(type: "Percentage", value: 12.5m, minOrder: 2000000m, maxDiscount: 150000m, limit: 50, used: 3, reserved: 1);

        // Act
        viewModel.Initialize(voucher);

        // Assert
        Assert.True(viewModel.IsEdit);
        Assert.Equal("Sửa voucher SALE10", viewModel.Title);
        Assert.Equal("Lưu thay đổi", viewModel.SaveButtonText);
        Assert.False(viewModel.IsCodeEditable);
        Assert.False(viewModel.IsTypeEditable);
        Assert.Equal("SALE10", viewModel.Code);
        Assert.Equal(0, viewModel.DiscountTypeIndex);
        Assert.Equal("12,5", viewModel.DiscountValue);
        Assert.Equal("2000000", viewModel.MinOrderAmount);
        Assert.Equal("150000", viewModel.MaxDiscountAmount);
        Assert.Equal("50", viewModel.UsageLimit);
        Assert.Equal(voucher.ExpiresAt.ToLocalTime().Date, viewModel.ExpiresAt!.Value.Date);
    }

    [Fact]
    public void Initialize_FixedVoucher_HidesTheCap()
    {
        // Arrange
        var viewModel = Create(new FakeVouchers());

        // Act
        viewModel.Initialize(Voucher(type: "FixedAmount", value: 500000m));

        // Assert
        Assert.Equal(1, viewModel.DiscountTypeIndex);
        Assert.False(viewModel.IsPercentage);
        Assert.False(viewModel.ShowMaxDiscount);
        Assert.Equal(string.Empty, viewModel.MaxDiscountAmount);
    }

    [Fact]
    public async Task Save_ValidPercentageVoucher_CreatesItWithCleanedValues()
    {
        // Arrange
        var repository = new FakeVouchers();
        var viewModel = Create(repository);
        viewModel.Initialize(null);
        Fill(viewModel, code: "  sale10 ", value: "10", min: "2.000.000", max: "150.000", limit: "50", description: " Giảm 10% ");

        // Act
        await viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        var request = Assert.Single(repository.Created);
        Assert.Equal("SALE10", request.Code);
        Assert.Equal("Percentage", request.DiscountType);
        Assert.Equal(10m, request.DiscountValue);
        Assert.Equal(2000000m, request.MinOrderAmount);
        Assert.Equal(150000m, request.MaxDiscountAmount);
        Assert.Equal(50, request.UsageLimit);
        Assert.Equal("Giảm 10%", request.Description);
        Assert.Equal(EndOfLocalDay(NextYear), request.ExpiresAt);
        Assert.NotNull(viewModel.SavedVoucher);
        Assert.False(viewModel.IsSaving);
        Assert.False(viewModel.HasFieldErrors);
    }

    [Fact]
    public async Task Save_FixedVoucher_DropsTheCapAndAcceptsBlankMinimum()
    {
        // Arrange
        var repository = new FakeVouchers();
        var viewModel = Create(repository);
        viewModel.Initialize(null);
        viewModel.DiscountTypeIndex = 1;
        Fill(viewModel, code: "TET-2027", value: "500.000", min: "", max: "999999", limit: "10", description: "  ");

        // Act
        await viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        var request = Assert.Single(repository.Created);
        Assert.Equal("FixedAmount", request.DiscountType);
        Assert.Equal(500000m, request.DiscountValue);
        Assert.Equal(0m, request.MinOrderAmount);
        Assert.Null(request.MaxDiscountAmount);
        Assert.Null(request.Description);
    }

    [Theory]
    [InlineData("12,5", 12.5)]
    [InlineData("12.5", 12.5)]
    [InlineData("100", 100)]
    [InlineData("0,01", 0.01)]
    public async Task Save_PercentageAcceptsCommaOrDotAsDecimalSeparator(string typed, double expected)
    {
        // Arrange
        var repository = new FakeVouchers();
        var viewModel = Create(repository);
        viewModel.Initialize(null);
        Fill(viewModel, value: typed);

        // Act
        await viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal((decimal)expected, Assert.Single(repository.Created).DiscountValue);
    }

    [Theory]
    [InlineData("", "Vui lòng nhập mã voucher.")]
    [InlineData("ab", "Mã gồm 3–32 ký tự")]
    [InlineData("sale 10", "Mã gồm 3–32 ký tự")]
    [InlineData("sale!", "Mã gồm 3–32 ký tự")]
    public async Task Save_BadCode_ShowsErrorAndSendsNothing(string code, string fragment)
    {
        // Arrange
        var repository = new FakeVouchers();
        var viewModel = Create(repository);
        viewModel.Initialize(null);
        Fill(viewModel, code: code);

        // Act
        await viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains(fragment, viewModel.CodeError);
        Assert.Empty(repository.Created);
        Assert.True(viewModel.HasFieldErrors);
    }

    [Fact]
    public async Task Save_CodeOfThirtyTwoCharacters_IsAccepted_AndThirtyThreeIsNot()
    {
        // Arrange
        var repository = new FakeVouchers();
        var viewModel = Create(repository);
        viewModel.Initialize(null);
        Fill(viewModel, code: new string('A', 33));
        await viewModel.SaveCommand.ExecuteAsync(null);
        Assert.NotEmpty(viewModel.CodeError);

        // Act
        viewModel.Code = new string('A', 32);
        await viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(string.Empty, viewModel.CodeError);
        Assert.Single(repository.Created);
    }

    [Theory]
    [InlineData(0, "", "Vui lòng nhập giá trị giảm.")]
    [InlineData(0, "abc", "Giá trị giảm không hợp lệ.")]
    [InlineData(0, "0", "lớn hơn 0")]
    [InlineData(0, "-5", "lớn hơn 0")]
    [InlineData(0, "100,01", "không vượt quá 100")]
    [InlineData(0, "120", "không vượt quá 100")]
    [InlineData(0, "10,123", "tối đa 2 chữ số thập phân")]
    [InlineData(1, "0", "lớn hơn 0")]
    [InlineData(1, "1000,5", "số nguyên")]
    public async Task Save_BadDiscountValue_ShowsErrorAndSendsNothing(int typeIndex, string value, string fragment)
    {
        // Arrange
        var repository = new FakeVouchers();
        var viewModel = Create(repository);
        viewModel.Initialize(null);
        viewModel.DiscountTypeIndex = typeIndex;
        Fill(viewModel, value: value);

        // Act
        await viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains(fragment, viewModel.DiscountValueError);
        Assert.Empty(repository.Created);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    public async Task Save_BadMinimumOrMaximum_ShowsErrors(string typed)
    {
        // Arrange
        var repository = new FakeVouchers();
        var viewModel = Create(repository);
        viewModel.Initialize(null);
        Fill(viewModel, min: typed, max: typed);

        // Act
        await viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.NotEmpty(viewModel.MinOrderAmountError);
        Assert.NotEmpty(viewModel.MaxDiscountAmountError);
        Assert.Empty(repository.Created);
    }

    [Theory]
    [InlineData("", "Vui lòng nhập số lượt dùng.")]
    [InlineData("0", "ít nhất 1")]
    [InlineData("abc", "số nguyên")]
    [InlineData("2147483648", "số nguyên")]
    public async Task Save_BadUsageLimit_ShowsErrorAndSendsNothing(string limit, string fragment)
    {
        // Arrange
        var repository = new FakeVouchers();
        var viewModel = Create(repository);
        viewModel.Initialize(null);
        Fill(viewModel, limit: limit);

        // Act
        await viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains(fragment, viewModel.UsageLimitError);
        Assert.Empty(repository.Created);
    }

    [Fact]
    public async Task Save_MissingOrPastExpiry_ShowsError()
    {
        // Arrange
        var repository = new FakeVouchers();
        var viewModel = Create(repository);
        viewModel.Initialize(null);
        Fill(viewModel);

        // Act
        viewModel.ExpiresAt = null;
        await viewModel.SaveCommand.ExecuteAsync(null);
        var missing = viewModel.ExpiresAtError;
        viewModel.ExpiresAt = new DateTimeOffset(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);
        await viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains("chọn ngày hết hạn", missing);
        Assert.Contains("tương lai", viewModel.ExpiresAtError);
        Assert.Empty(repository.Created);
    }

    [Fact]
    public async Task Save_DescriptionOver500Characters_IsRejected()
    {
        // Arrange
        var repository = new FakeVouchers();
        var viewModel = Create(repository);
        viewModel.Initialize(null);
        Fill(viewModel, description: new string('x', 501));

        // Act
        await viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains("500", viewModel.DescriptionError);
        Assert.Empty(repository.Created);
    }

    [Fact]
    public async Task Save_Edit_UpdatesWithoutCodeOrType()
    {
        // Arrange
        var repository = new FakeVouchers();
        var viewModel = Create(repository);
        var voucher = Voucher(type: "Percentage", value: 10m, limit: 50, used: 3, reserved: 1);
        viewModel.Initialize(voucher);
        viewModel.DiscountValue = "15";
        viewModel.UsageLimit = "80";
        viewModel.ExpiresAt = NextYear;

        // Act
        await viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        var (id, request) = Assert.Single(repository.Updated);
        Assert.Equal(voucher.Id, id);
        Assert.Equal(15m, request.DiscountValue);
        Assert.Equal(80, request.UsageLimit);
        Assert.Equal(EndOfLocalDay(NextYear), request.ExpiresAt);
        Assert.Empty(repository.Created);
        Assert.NotNull(viewModel.SavedVoucher);
    }

    [Fact]
    public async Task Save_Edit_LimitBelowUsesInPlace_IsRejectedLocally()
    {
        // Arrange
        var repository = new FakeVouchers();
        var viewModel = Create(repository);
        viewModel.Initialize(Voucher(limit: 50, used: 3, reserved: 2, expiresAt: Now.UtcDateTime.AddDays(30)));
        viewModel.UsageLimit = "4";

        // Act
        await viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains("5", viewModel.UsageLimitError);
        Assert.Empty(repository.Updated);
    }

    [Fact]
    public async Task Save_Edit_LimitEqualToUsesInPlace_IsAllowed()
    {
        // Arrange
        var repository = new FakeVouchers();
        var viewModel = Create(repository);
        viewModel.Initialize(Voucher(limit: 50, used: 3, reserved: 2));
        viewModel.UsageLimit = "5";

        // Act
        await viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(repository.Updated);
    }

    [Fact]
    public async Task Save_Edit_ExpiredVoucherNeedsANewFutureDate()
    {
        // Arrange
        var repository = new FakeVouchers();
        var viewModel = Create(repository);
        viewModel.Initialize(Voucher(expiresAt: Now.UtcDateTime.AddDays(-5)));

        // Act
        await viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains("tương lai", viewModel.ExpiresAtError);
        Assert.Empty(repository.Updated);
    }

    [Fact]
    public async Task Save_Conflict_ShowsDuplicateCodeAndKeepsTheDialogOpen()
    {
        // Arrange
        var repository = new FakeVouchers { Failure = new ApiException(ApiErrorKind.Http, "x", 409) };
        var viewModel = Create(repository);
        viewModel.Initialize(null);
        Fill(viewModel);

        // Act
        await viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains("đã tồn tại", viewModel.CodeError);
        Assert.Null(viewModel.SavedVoucher);
        Assert.False(viewModel.IsSaving);
    }

    [Fact]
    public async Task Save_ServerFieldErrors_ShowBesideTheirFields()
    {
        // Arrange
        var errors = new Dictionary<string, IReadOnlyList<string>>
        {
            ["discountValue"] = ["A percentage discount cannot exceed 100."],
            ["minOrderAmount"] = ["min bad"],
            ["maxDiscountAmount"] = ["max bad"],
            ["usageLimit"] = ["limit bad"],
            ["expiresAt"] = ["expiry bad"],
            ["description"] = ["desc bad"],
            ["code"] = ["code bad"],
            ["somethingElse"] = ["other bad"]
        };
        var repository = new FakeVouchers { Failure = new ApiException(ApiErrorKind.Http, "x", 400, fieldErrors: errors) };
        var viewModel = Create(repository);
        viewModel.Initialize(null);
        Fill(viewModel);

        // Act
        await viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("A percentage discount cannot exceed 100.", viewModel.DiscountValueError);
        Assert.Equal("min bad", viewModel.MinOrderAmountError);
        Assert.Equal("max bad", viewModel.MaxDiscountAmountError);
        Assert.Equal("limit bad", viewModel.UsageLimitError);
        Assert.Equal("expiry bad", viewModel.ExpiresAtError);
        Assert.Equal("desc bad", viewModel.DescriptionError);
        Assert.Equal("code bad", viewModel.CodeError);
        Assert.Equal("other bad", viewModel.FormError);
        Assert.Null(viewModel.SavedVoucher);
    }

    [Theory]
    [InlineData(ApiErrorKind.Http, 401, "hết hạn")]
    [InlineData(ApiErrorKind.Http, 403, "không có quyền")]
    [InlineData(ApiErrorKind.Http, 404, "không còn")]
    [InlineData(ApiErrorKind.Network, null, "kết nối")]
    [InlineData(ApiErrorKind.Http, 500, "Không thể lưu voucher")]
    public async Task Save_OtherFailures_ShowASafeFormError(ApiErrorKind kind, int? status, string fragment)
    {
        // Arrange
        var repository = new FakeVouchers { Failure = new ApiException(kind, "private details", status) };
        var viewModel = Create(repository);
        viewModel.Initialize(null);
        Fill(viewModel);

        // Act
        await viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains(fragment, viewModel.FormError, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private", viewModel.FormError, StringComparison.OrdinalIgnoreCase);
        Assert.Null(viewModel.SavedVoucher);
    }

    [Fact]
    public async Task Save_UnexpectedException_ShowsASafeFormError()
    {
        // Arrange
        var repository = new FakeVouchers { Failure = new InvalidOperationException("secret") };
        var viewModel = Create(repository);
        viewModel.Initialize(null);
        Fill(viewModel);

        // Act
        await viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains("Không thể lưu voucher", viewModel.FormError);
        Assert.DoesNotContain("secret", viewModel.FormError);
    }

    [Fact]
    public async Task Save_WhileSaving_CannotBeSubmittedAgain()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        var repository = new FakeVouchers { Gate = gate.Task };
        var viewModel = Create(repository);
        viewModel.Initialize(null);
        Fill(viewModel);

        // Act
        var first = viewModel.SaveCommand.ExecuteAsync(null);
        var canSubmitAgain = viewModel.SaveCommand.CanExecute(null);
        var label = viewModel.SaveButtonText;
        gate.SetResult();
        await first;

        // Assert
        Assert.False(canSubmitAgain);
        Assert.Equal("Đang lưu...", label);
        Assert.Single(repository.Created);
        Assert.True(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task Save_Cancelled_PropagatesAndDoesNotShowAnError()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        var repository = new FakeVouchers { Gate = gate.Task };
        var viewModel = Create(repository);
        viewModel.Initialize(null);
        Fill(viewModel);

        // Act
        var save = viewModel.SaveCommand.ExecuteAsync(null);
        viewModel.SaveCommand.Cancel();
        gate.SetResult();
        try { await save; } catch (OperationCanceledException) { }

        // Assert
        Assert.Equal(string.Empty, viewModel.FormError);
        Assert.Null(viewModel.SavedVoucher);
        Assert.False(viewModel.IsSaving);
    }

    [Fact]
    public async Task Initialize_AgainClearsErrorsAndTheSavedVoucher()
    {
        // Arrange
        var repository = new FakeVouchers();
        var viewModel = Create(repository);
        viewModel.Initialize(null);
        Fill(viewModel, code: "");
        await viewModel.SaveCommand.ExecuteAsync(null);
        Assert.NotEmpty(viewModel.CodeError);
        Fill(viewModel);
        await viewModel.SaveCommand.ExecuteAsync(null);
        Assert.NotNull(viewModel.SavedVoucher);

        // Act
        viewModel.Initialize(null);

        // Assert
        Assert.Equal(string.Empty, viewModel.CodeError);
        Assert.Null(viewModel.SavedVoucher);
        Assert.Equal(string.Empty, viewModel.Code);
    }

    [Fact]
    public async Task Save_Success_AsksTheDialogToCloseOnce()
    {
        // Arrange
        var viewModel = Create(new FakeVouchers());
        viewModel.Initialize(null);
        Fill(viewModel);
        var closed = 0;
        viewModel.RequestClose += (_, _) => closed++;

        // Act
        await viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, closed);
    }

    [Fact]
    public async Task Save_Failure_KeepsTheDialogOpen()
    {
        // Arrange
        var viewModel = Create(new FakeVouchers { Failure = new ApiException(ApiErrorKind.Http, "x", 409) });
        viewModel.Initialize(null);
        Fill(viewModel);
        var closed = 0;
        viewModel.RequestClose += (_, _) => closed++;

        // Act
        await viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(0, closed);
    }

    [Fact]
    public void Cancel_AsksTheDialogToCloseWithoutASavedVoucher()
    {
        // Arrange
        var viewModel = Create(new FakeVouchers());
        viewModel.Initialize(null);
        var closed = 0;
        viewModel.RequestClose += (_, _) => closed++;

        // Act
        viewModel.CancelCommand.Execute(null);

        // Assert
        Assert.Equal(1, closed);
        Assert.Null(viewModel.SavedVoucher);
    }

    private static VoucherEditorDialogViewModel Create(FakeVouchers repository) => new(repository, new FixedClock(Now));

    private static void Fill(
        VoucherEditorDialogViewModel viewModel,
        string code = "SALE10",
        string value = "10",
        string min = "",
        string max = "",
        string limit = "50",
        string description = "",
        DateTimeOffset? expiry = null)
    {
        viewModel.Code = code;
        viewModel.DiscountValue = value;
        viewModel.MinOrderAmount = min;
        viewModel.MaxDiscountAmount = max;
        viewModel.UsageLimit = limit;
        viewModel.Description = description;
        viewModel.ExpiresAt = expiry ?? NextYear;
    }

    private static DateTime EndOfLocalDay(DateTimeOffset date) =>
        new DateTime(date.Year, date.Month, date.Day, 23, 59, 59, DateTimeKind.Local).ToUniversalTime();

    private static VoucherItem Voucher(
        string type = "Percentage",
        decimal value = 10m,
        decimal minOrder = 0m,
        decimal? maxDiscount = null,
        int limit = 50,
        int used = 0,
        int reserved = 0,
        DateTime? expiresAt = null) => new(
            Guid.Parse("55555555-5555-5555-5555-555555555555"), "SALE10", "Giảm giá thử", type, value, minOrder, maxDiscount,
            limit, used, reserved, expiresAt ?? Now.UtcDateTime.AddDays(30), true, Now.UtcDateTime.AddDays(-10));

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeVouchers : IVoucherRepository
    {
        public List<CreateVoucherRequest> Created { get; } = [];
        public List<(Guid Id, UpdateVoucherRequest Request)> Updated { get; } = [];
        public Exception? Failure { get; set; }
        public Task? Gate { get; set; }

        public async Task<VoucherItem> CreateAsync(CreateVoucherRequest request, CancellationToken cancellationToken = default)
        {
            Created.Add(request);
            if (Gate is not null) await Gate.WaitAsync(cancellationToken);
            if (Failure is not null) throw Failure;
            return new(Guid.NewGuid(), request.Code!, request.Description, request.DiscountType!, request.DiscountValue!.Value,
                request.MinOrderAmount ?? 0m, request.MaxDiscountAmount, request.UsageLimit!.Value, 0, 0,
                request.ExpiresAt!.Value, true, DateTime.UtcNow);
        }

        public async Task<VoucherItem> UpdateAsync(Guid voucherId, UpdateVoucherRequest request, CancellationToken cancellationToken = default)
        {
            Updated.Add((voucherId, request));
            if (Gate is not null) await Gate.WaitAsync(cancellationToken);
            if (Failure is not null) throw Failure;
            return Voucher() with { Id = voucherId };
        }

        public Task<VoucherPage> SearchAsync(VoucherQuery query, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<VoucherItem> SetActiveAsync(Guid voucherId, bool isActive, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
