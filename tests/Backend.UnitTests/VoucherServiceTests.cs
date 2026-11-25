using FluentAssertions;
using TechStore.Api.Common.Data;
using TechStore.Api.Customers.Entities;
using TechStore.Api.Customers.Services;
using TechStore.Api.Orders.Entities;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

// M4 Vy: voucher management plus the hold / consume / return lifecycle of one use per order.
public class VoucherServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 23, 3, 0, 0, DateTimeKind.Utc);

    private readonly FakeVoucherRepository _vouchers = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly VoucherService _service;

    public VoucherServiceTests()
    {
        _service = new VoucherService(_vouchers, _unitOfWork, new FixedTimeProvider(new DateTimeOffset(Now)));
    }

    // ---------------- management ----------------

    [Fact]
    public async Task CreateAsync_ValidPercentageVoucher_StoresUpperCasedCodeAndSaves()
    {
        // Arrange
        var request = ValidCreate();
        request.Code = "  sale10 ";

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Code.Should().Be("SALE10");
        result.Data.DiscountType.Should().Be("Percentage");
        result.Data.DiscountValue.Should().Be(10m);
        result.Data.UsageLimit.Should().Be(50);
        result.Data.UsedCount.Should().Be(0);
        result.Data.ReservedCount.Should().Be(0);
        result.Data.IsActive.Should().BeTrue();
        _vouchers.Vouchers.Values.Should().ContainSingle(v => v.Code == "SALE10");
        _unitOfWork.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_FixedVoucher_IgnoresMaxDiscount()
    {
        // Arrange
        var request = ValidCreate();
        request.DiscountType = "FixedAmount";
        request.DiscountValue = 200_000m;
        request.MaxDiscountAmount = 50_000m;

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.MaxDiscountAmount.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_UsageLimitOmitted_UsesTheDefaultLimit()
    {
        // Arrange
        var request = ValidCreate();
        request.UsageLimit = null;

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        result.Data!.UsageLimit.Should().Be(100);
    }

    public static TheoryData<string, Action<CreateVoucherRequest>> InvalidCreates => new()
    {
        { "code", r => r.Code = null },
        { "code", r => r.Code = "   " },
        { "code", r => r.Code = "sale 10" },
        { "code", r => r.Code = "ab" },
        { "discountType", r => r.DiscountType = null },
        { "discountType", r => r.DiscountType = "Half" },
        { "discountValue", r => r.DiscountValue = null },
        { "discountValue", r => r.DiscountValue = 0m },
        { "discountValue", r => r.DiscountValue = -5m },
        { "discountValue", r => r.DiscountValue = 120m },
        { "discountValue", r => { r.DiscountType = "FixedAmount"; r.DiscountValue = 1000.5m; } },
        { "minOrderAmount", r => r.MinOrderAmount = -1m },
        { "maxDiscountAmount", r => r.MaxDiscountAmount = -1m },
        { "usageLimit", r => r.UsageLimit = 0 },
        { "expiresAt", r => r.ExpiresAt = null },
        { "expiresAt", r => r.ExpiresAt = Now.AddMinutes(-1) },
        { "description", r => r.Description = new string('x', 501) }
    };

    [Theory]
    [MemberData(nameof(InvalidCreates))]
    public async Task CreateAsync_InvalidInput_ReturnsErrorUnderTheField(string field, Action<CreateVoucherRequest> mutate)
    {
        // Arrange
        var request = ValidCreate();
        mutate(request);

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey(field);
        _vouchers.Vouchers.Should().BeEmpty();
        _unitOfWork.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_CodeAlreadyExists_ReturnsConflict()
    {
        // Arrange
        AddVoucher("SALE10");
        var request = ValidCreate();
        request.Code = "sale10";

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        result.IsConflict.Should().BeTrue();
        _unitOfWork.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_CodeTakenByARacingRequest_ReturnsConflict()
    {
        // Arrange: the code check passes but the unique index rejects the save.
        _unitOfWork.ExceptionToThrowOnSave = new DuplicateKeyException("idx_vouchers_code");

        // Act
        var result = await _service.CreateAsync(ValidCreate());

        // Assert
        result.IsConflict.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_ValidChanges_UpdatesEditableFieldsOnly()
    {
        // Arrange
        var voucher = AddVoucher("SALE10");
        var request = new UpdateVoucherRequest
        {
            Description = "New text",
            DiscountValue = 15m,
            MinOrderAmount = 500_000m,
            MaxDiscountAmount = 300_000m,
            UsageLimit = 200,
            ExpiresAt = Now.AddDays(60)
        };

        // Act
        var result = await _service.UpdateAsync(voucher.Id, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        voucher.Code.Should().Be("SALE10");
        voucher.DiscountType.Should().Be(VoucherType.Percentage);
        voucher.DiscountValue.Should().Be(15m);
        voucher.MinOrderAmount.Should().Be(500_000m);
        voucher.MaxDiscountAmount.Should().Be(300_000m);
        voucher.UsageLimit.Should().Be(200);
        voucher.Description.Should().Be("New text");
        voucher.UpdatedAt.Should().Be(Now);
        _unitOfWork.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_LimitBelowConsumedPlusHeld_IsInvalid()
    {
        // Arrange: 3 used and 2 held means at least 5 are in play.
        var voucher = AddVoucher("SALE10");
        voucher.UsageLimit = 10;
        voucher.UsedCount = 3;
        voucher.ReservedCount = 2;
        var request = ValidUpdate();
        request.UsageLimit = 4;

        // Act
        var result = await _service.UpdateAsync(voucher.Id, request);

        // Assert
        result.ValidationErrors.Should().ContainKey("usageLimit");
        voucher.UsageLimit.Should().Be(10);
    }

    [Fact]
    public async Task UpdateAsync_LimitEqualToConsumedPlusHeld_IsAllowed()
    {
        // Arrange
        var voucher = AddVoucher("SALE10");
        voucher.UsedCount = 3;
        voucher.ReservedCount = 2;
        var request = ValidUpdate();
        request.UsageLimit = 5;

        // Act
        var result = await _service.UpdateAsync(voucher.Id, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_PercentageAbove100_IsInvalid()
    {
        // Arrange
        var voucher = AddVoucher("SALE10");
        var request = ValidUpdate();
        request.DiscountValue = 101m;

        // Act
        var result = await _service.UpdateAsync(voucher.Id, request);

        // Assert
        result.ValidationErrors.Should().ContainKey("discountValue");
    }

    [Fact]
    public async Task UpdateAsync_UnknownVoucher_ReturnsNotFound()
    {
        // Act
        var result = await _service.UpdateAsync(Guid.NewGuid(), ValidUpdate());

        // Assert
        result.Found.Should().BeFalse();
    }

    [Fact]
    public async Task SetActiveAsync_TogglesTheFlagAndSaves()
    {
        // Arrange
        var voucher = AddVoucher("SALE10");

        // Act
        var off = await _service.SetActiveAsync(voucher.Id, false);
        var on = await _service.SetActiveAsync(voucher.Id, true);

        // Assert
        off.Data!.IsActive.Should().BeFalse();
        on.Data!.IsActive.Should().BeTrue();
        _unitOfWork.SaveChangesCount.Should().Be(2);
    }

    [Fact]
    public async Task SetActiveAsync_UnknownVoucher_ReturnsNotFound()
    {
        // Act
        var result = await _service.SetActiveAsync(Guid.NewGuid(), false);

        // Assert
        result.Found.Should().BeFalse();
    }

    [Fact]
    public async Task SearchAsync_FiltersByTextAndActiveState_AndPages()
    {
        // Arrange
        AddVoucher("SALE10");
        AddVoucher("SALE20");
        AddVoucher("TET2027").IsActive = false;

        // Act
        var bySale = await _service.SearchAsync(new VoucherSearchRequest { Q = "sale", PageSize = 1 });
        var inactive = await _service.SearchAsync(new VoucherSearchRequest { IsActive = false });

        // Assert
        bySale.Data!.TotalCount.Should().Be(2);
        bySale.Data.Items.Should().HaveCount(1);
        bySale.Data.TotalPages.Should().Be(2);
        inactive.Data!.Items.Should().ContainSingle().Which.Code.Should().Be("TET2027");
    }

    [Fact]
    public async Task GetByIdAsync_KnownAndUnknown()
    {
        // Arrange
        var voucher = AddVoucher("SALE10");

        // Act
        var found = await _service.GetByIdAsync(voucher.Id);
        var missing = await _service.GetByIdAsync(Guid.NewGuid());

        // Assert
        found.Data!.Code.Should().Be("SALE10");
        missing.Found.Should().BeFalse();
    }

    // ---------------- hold / consume / return ----------------

    [Fact]
    public async Task ReserveAsync_EligibleVoucher_HoldsOneUseUnderRowLockWithoutSaving()
    {
        // Arrange
        var voucher = AddVoucher("SALE10");
        var order = OrderWith(voucher);

        // Act
        var result = await _service.ReserveAsync(order);

        // Assert
        result.Outcome.Should().Be(VoucherUseOutcome.Applied);
        voucher.ReservedCount.Should().Be(1);
        voucher.UsedCount.Should().Be(0);
        var row = _vouchers.Redemptions.Should().ContainSingle().Subject;
        row.OrderId.Should().Be(order.Id);
        row.VoucherId.Should().Be(voucher.Id);
        row.Status.Should().Be(VoucherRedemptionStatus.Reserved);
        _vouchers.ForUpdateCalls.Should().Be(1);
        // The caller owns the transaction and the save.
        _unitOfWork.SaveChangesCount.Should().Be(0);
        _unitOfWork.BeginTransactionCount.Should().Be(0);
        _unitOfWork.CommitCount.Should().Be(0);
    }

    [Fact]
    public async Task ReserveAsync_SameOrderTwice_HoldsOnlyOneUse()
    {
        // Arrange
        var voucher = AddVoucher("SALE10");
        var order = OrderWith(voucher);
        await _service.ReserveAsync(order);

        // Act
        var second = await _service.ReserveAsync(order);

        // Assert
        second.Outcome.Should().Be(VoucherUseOutcome.Unchanged);
        voucher.ReservedCount.Should().Be(1);
        _vouchers.Redemptions.Should().HaveCount(1);
    }

    [Fact]
    public async Task ReserveAsync_LastUseAlreadyConsumed_IsRefused()
    {
        // Arrange
        var voucher = AddVoucher("SALE10");
        voucher.UsageLimit = 1;
        voucher.UsedCount = 1;

        // Act
        var result = await _service.ReserveAsync(OrderWith(voucher));

        // Assert
        result.Outcome.Should().Be(VoucherUseOutcome.Refused);
        voucher.ReservedCount.Should().Be(0);
        _vouchers.Redemptions.Should().BeEmpty();
    }

    [Fact]
    public async Task ReserveAsync_LastUseHeldByAnotherOrder_IsRefused()
    {
        // Arrange
        var voucher = AddVoucher("SALE10");
        voucher.UsageLimit = 1;
        await _service.ReserveAsync(OrderWith(voucher));

        // Act
        var second = await _service.ReserveAsync(OrderWith(voucher));

        // Assert
        second.Outcome.Should().Be(VoucherUseOutcome.Refused);
        voucher.ReservedCount.Should().Be(1);
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("expired")]
    [InlineData("below-minimum")]
    public async Task ReserveAsync_VoucherNoLongerEligible_IsRefused(string reason)
    {
        // Arrange
        var voucher = AddVoucher("SALE10");
        if (reason == "inactive") voucher.IsActive = false;
        if (reason == "expired") voucher.ExpiresAt = Now;
        if (reason == "below-minimum") voucher.MinOrderAmount = 5_000_000m;

        // Act
        var result = await _service.ReserveAsync(OrderWith(voucher));

        // Assert
        result.Outcome.Should().Be(VoucherUseOutcome.Refused);
        result.Reason.Should().NotBeNullOrWhiteSpace();
        voucher.ReservedCount.Should().Be(0);
    }

    [Fact]
    public async Task ReserveAsync_OrderWithoutVoucher_IsRefused()
    {
        // Act
        var result = await _service.ReserveAsync(new Order { Subtotal = 1_000_000m });

        // Assert
        result.Outcome.Should().Be(VoucherUseOutcome.Refused);
    }

    [Fact]
    public async Task ReserveAsync_UnknownVoucher_ReturnsNotFound()
    {
        // Act
        var result = await _service.ReserveAsync(new Order { VoucherId = Guid.NewGuid(), Subtotal = 1_000_000m });

        // Assert
        result.Outcome.Should().Be(VoucherUseOutcome.NotFound);
    }

    [Fact]
    public async Task RedeemAsync_HeldUse_MovesFromHeldToConsumed()
    {
        // Arrange
        var voucher = AddVoucher("SALE10");
        var order = OrderWith(voucher);
        await _service.ReserveAsync(order);

        // Act
        var result = await _service.RedeemAsync(order.Id);

        // Assert
        result.Outcome.Should().Be(VoucherUseOutcome.Applied);
        voucher.ReservedCount.Should().Be(0);
        voucher.UsedCount.Should().Be(1);
        var row = _vouchers.Redemptions.Single();
        row.Status.Should().Be(VoucherRedemptionStatus.Redeemed);
        row.RedeemedAt.Should().Be(Now);
    }

    [Fact]
    public async Task RedeemAsync_Twice_ConsumesOnlyOnce()
    {
        // Arrange
        var voucher = AddVoucher("SALE10");
        var order = OrderWith(voucher);
        await _service.ReserveAsync(order);
        await _service.RedeemAsync(order.Id);

        // Act
        var second = await _service.RedeemAsync(order.Id);

        // Assert
        second.Outcome.Should().Be(VoucherUseOutcome.Unchanged);
        voucher.UsedCount.Should().Be(1);
        voucher.ReservedCount.Should().Be(0);
    }

    [Fact]
    public async Task RedeemAsync_WithoutHold_ReturnsNotFound()
    {
        // Act
        var result = await _service.RedeemAsync(Guid.NewGuid());

        // Assert
        result.Outcome.Should().Be(VoucherUseOutcome.NotFound);
    }

    [Fact]
    public async Task RedeemAsync_AfterTheHoldWasReleased_IsRefused()
    {
        // Arrange
        var voucher = AddVoucher("SALE10");
        var order = OrderWith(voucher);
        await _service.ReserveAsync(order);
        await _service.ReleaseAsync(order.Id);

        // Act
        var result = await _service.RedeemAsync(order.Id);

        // Assert
        result.Outcome.Should().Be(VoucherUseOutcome.Refused);
        voucher.UsedCount.Should().Be(0);
    }

    [Fact]
    public async Task ReleaseAsync_HeldUse_ReturnsTheUseToTheVoucher()
    {
        // Arrange
        var voucher = AddVoucher("SALE10");
        var order = OrderWith(voucher);
        await _service.ReserveAsync(order);

        // Act
        var result = await _service.ReleaseAsync(order.Id);

        // Assert
        result.Outcome.Should().Be(VoucherUseOutcome.Applied);
        voucher.ReservedCount.Should().Be(0);
        voucher.UsedCount.Should().Be(0);
        var row = _vouchers.Redemptions.Single();
        row.Status.Should().Be(VoucherRedemptionStatus.Released);
        row.ReleasedAt.Should().Be(Now);
    }

    [Fact]
    public async Task ReleaseAsync_Twice_ReturnsTheUseOnlyOnce()
    {
        // Arrange
        var voucher = AddVoucher("SALE10");
        voucher.ReservedCount = 1;
        var order = OrderWith(voucher);
        await _service.ReserveAsync(order);
        await _service.ReleaseAsync(order.Id);

        // Act
        var second = await _service.ReleaseAsync(order.Id);

        // Assert
        second.Outcome.Should().Be(VoucherUseOutcome.Unchanged);
        voucher.ReservedCount.Should().Be(1, "the other order's hold must stay");
    }

    [Fact]
    public async Task ReleaseAsync_AfterPayment_DoesNotGiveTheUseBack()
    {
        // Arrange: a refunded, paid order does not restore its voucher.
        var voucher = AddVoucher("SALE10");
        var order = OrderWith(voucher);
        await _service.ReserveAsync(order);
        await _service.RedeemAsync(order.Id);

        // Act
        var result = await _service.ReleaseAsync(order.Id);

        // Assert
        result.Outcome.Should().Be(VoucherUseOutcome.Unchanged);
        voucher.UsedCount.Should().Be(1);
        voucher.ReservedCount.Should().Be(0);
        _vouchers.Redemptions.Single().Status.Should().Be(VoucherRedemptionStatus.Redeemed);
    }

    [Fact]
    public async Task ReleaseAsync_OrderWithoutAnyUse_ReturnsNotFound()
    {
        // Act
        var result = await _service.ReleaseAsync(Guid.NewGuid());

        // Assert
        result.Outcome.Should().Be(VoucherUseOutcome.NotFound);
    }

    [Fact]
    public async Task ReserveAsync_AfterRelease_HoldsTheUseAgain()
    {
        // Arrange
        var voucher = AddVoucher("SALE10");
        var order = OrderWith(voucher);
        await _service.ReserveAsync(order);
        await _service.ReleaseAsync(order.Id);

        // Act
        var result = await _service.ReserveAsync(order);

        // Assert
        result.Outcome.Should().Be(VoucherUseOutcome.Applied);
        voucher.ReservedCount.Should().Be(1);
        _vouchers.Redemptions.Should().ContainSingle().Which.Status.Should().Be(VoucherRedemptionStatus.Reserved);
    }

    private static CreateVoucherRequest ValidCreate() => new()
    {
        Code = "SALE10",
        Description = "10% off",
        DiscountType = "Percentage",
        DiscountValue = 10m,
        MinOrderAmount = 0m,
        MaxDiscountAmount = 500_000m,
        UsageLimit = 50,
        ExpiresAt = Now.AddDays(30)
    };

    private static UpdateVoucherRequest ValidUpdate() => new()
    {
        Description = "Updated",
        DiscountValue = 10m,
        MinOrderAmount = 0m,
        MaxDiscountAmount = null,
        UsageLimit = 100,
        ExpiresAt = Now.AddDays(30)
    };

    private Voucher AddVoucher(string code)
    {
        var voucher = new Voucher
        {
            Code = code,
            DiscountType = VoucherType.Percentage,
            DiscountValue = 10m,
            UsageLimit = 100,
            ExpiresAt = Now.AddDays(30),
            CreatedAt = Now
        };
        _vouchers.Add(voucher);
        return voucher;
    }

    private static Order OrderWith(Voucher voucher) => new()
    {
        OrderCode = "ORD-202611-0001",
        VoucherId = voucher.Id,
        Subtotal = 2_000_000m,
        DiscountAmount = 200_000m,
        DiscountSource = OrderDiscountSource.Voucher
    };
}
