using System.Text.RegularExpressions;
using TechStore.Api.Common.Data;
using TechStore.Api.Customers.Entities;
using TechStore.Api.Customers.Repositories;
using TechStore.Api.Orders.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Api.Customers.Services;

public partial class VoucherService(
    IVoucherRepository voucherRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IVoucherService
{
    private const string VoucherCodeConstraint = "idx_vouchers_code";
    private const int DefaultUsageLimit = 100;
    private const int MaxDescriptionLength = 500;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    [GeneratedRegex("^[A-Za-z0-9_-]{3,32}$")]
    private static partial Regex CodePattern();

    // ------------------------------------------------------------------
    // Management (Admin, Manager)
    // ------------------------------------------------------------------

    public async Task<CustomerOperationResult<PagedResultDto<VoucherDto>>> SearchAsync(VoucherSearchRequest request, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, request.Page ?? 1);
        var pageSize = Math.Clamp(request.PageSize ?? DefaultPageSize, 1, MaxPageSize);
        var criteria = new VoucherSearchCriteria(string.IsNullOrWhiteSpace(request.Q) ? null : request.Q.Trim(), request.IsActive, page, pageSize);
        var (items, count) = await voucherRepository.SearchAsync(criteria, cancellationToken);
        var totalPages = count == 0 ? 0 : (int)Math.Ceiling(count / (double)pageSize);
        return CustomerOperationResult<PagedResultDto<VoucherDto>>.Success(
            new PagedResultDto<VoucherDto>(items.Select(Map).ToList(), page, pageSize, count, totalPages));
    }

    public async Task<CustomerOperationResult<VoucherDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var voucher = await voucherRepository.GetByIdAsync(id, cancellationToken);
        return voucher == null
            ? CustomerOperationResult<VoucherDto>.NotFound()
            : CustomerOperationResult<VoucherDto>.Success(Map(voucher));
    }

    public async Task<CustomerOperationResult<VoucherDto>> CreateAsync(CreateVoucherRequest request, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var errors = new Dictionary<string, string[]>();

        var code = request.Code?.Trim() ?? string.Empty;
        if (code.Length == 0)
            errors["code"] = ["Code is required."];
        else if (!CodePattern().IsMatch(code))
            errors["code"] = ["Code must be 3-32 characters: letters, digits, '-' or '_'."];

        VoucherType type = default;
        if (string.IsNullOrWhiteSpace(request.DiscountType))
            errors["discountType"] = ["Discount type is required."];
        else
        {
            var name = request.DiscountType.Trim();
            var match = Enum.GetNames<VoucherType>().FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            if (match == null)
                errors["discountType"] = [$"Discount type must be one of: {string.Join(", ", Enum.GetNames<VoucherType>())}."];
            else
                type = Enum.Parse<VoucherType>(match);
        }

        var fields = ValidateFields(errors, type, request.DiscountValue, request.MinOrderAmount, request.MaxDiscountAmount,
            request.UsageLimit ?? DefaultUsageLimit, request.ExpiresAt, request.Description, now);
        if (errors.Count > 0)
            return CustomerOperationResult<VoucherDto>.Invalid(errors);

        code = code.ToUpperInvariant();
        if (await voucherRepository.CodeExistsAsync(code, cancellationToken))
            return CustomerOperationResult<VoucherDto>.Conflict();

        var voucher = new Voucher
        {
            Code = code,
            DiscountType = type,
            CreatedAt = now
        };
        Apply(voucher, fields);
        voucherRepository.Add(voucher);
        try { await unitOfWork.SaveChangesAsync(cancellationToken); }
        catch (DuplicateKeyException ex) when (ex.ConstraintName == VoucherCodeConstraint) { return CustomerOperationResult<VoucherDto>.Conflict(); }
        return CustomerOperationResult<VoucherDto>.Success(Map(voucher));
    }

    public async Task<CustomerOperationResult<VoucherDto>> UpdateAsync(Guid id, UpdateVoucherRequest request, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var voucher = await voucherRepository.GetTrackedByIdAsync(id, cancellationToken);
        if (voucher == null) return CustomerOperationResult<VoucherDto>.NotFound();

        var errors = new Dictionary<string, string[]>();
        if (request.UsageLimit == null)
            errors["usageLimit"] = ["Usage limit is required."];

        var fields = ValidateFields(errors, voucher.DiscountType, request.DiscountValue, request.MinOrderAmount, request.MaxDiscountAmount,
            request.UsageLimit ?? DefaultUsageLimit, request.ExpiresAt, request.Description, now);

        // Uses already consumed or held stay valid: the limit cannot drop below them.
        var inPlay = voucher.UsedCount + voucher.ReservedCount;
        if (request.UsageLimit.HasValue && request.UsageLimit.Value < inPlay)
            errors["usageLimit"] = [$"Usage limit cannot be below the {inPlay} uses already consumed or held."];

        if (errors.Count > 0)
            return CustomerOperationResult<VoucherDto>.Invalid(errors);

        Apply(voucher, fields);
        voucher.UpdatedAt = now;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CustomerOperationResult<VoucherDto>.Success(Map(voucher));
    }

    public async Task<CustomerOperationResult<VoucherDto>> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var voucher = await voucherRepository.GetTrackedByIdAsync(id, cancellationToken);
        if (voucher == null) return CustomerOperationResult<VoucherDto>.NotFound();

        voucher.IsActive = isActive;
        voucher.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CustomerOperationResult<VoucherDto>.Success(Map(voucher));
    }

    // ------------------------------------------------------------------
    // Hold / consume / return (called inside the caller's transaction)
    // ------------------------------------------------------------------

    public async Task<VoucherUseResult> ReserveAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (!order.VoucherId.HasValue)
            return new VoucherUseResult(VoucherUseOutcome.Refused, "The order has no voucher.");

        // Lock first so two sales racing for the last use are decided one after the other.
        var voucher = await voucherRepository.GetForUpdateAsync(order.VoucherId.Value, cancellationToken);
        if (voucher == null)
            return new VoucherUseResult(VoucherUseOutcome.NotFound, "Voucher was not found.");

        var redemption = await voucherRepository.GetRedemptionByOrderAsync(order.Id, cancellationToken);
        if (redemption != null && redemption.Status != VoucherRedemptionStatus.Released)
            return new VoucherUseResult(VoucherUseOutcome.Unchanged);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var evaluation = VoucherRules.Evaluate(voucher, order.Subtotal, now);
        if (!evaluation.IsEligible)
            return new VoucherUseResult(VoucherUseOutcome.Refused, evaluation.Reason);

        voucher.ReservedCount++;
        voucher.UpdatedAt = now;
        if (redemption == null)
        {
            voucherRepository.Add(new VoucherRedemption { VoucherId = voucher.Id, OrderId = order.Id, Status = VoucherRedemptionStatus.Reserved });
        }
        else
        {
            // The earlier hold was returned; the same order holds a use again.
            redemption.Status = VoucherRedemptionStatus.Reserved;
            redemption.ReleasedAt = null;
            redemption.UpdatedAt = now;
        }

        return new VoucherUseResult(VoucherUseOutcome.Applied);
    }

    public async Task<VoucherUseResult> RedeemAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var redemption = await voucherRepository.GetRedemptionByOrderAsync(orderId, cancellationToken);
        if (redemption == null)
            return new VoucherUseResult(VoucherUseOutcome.NotFound, "The order holds no voucher use.");
        if (redemption.Status == VoucherRedemptionStatus.Redeemed)
            return new VoucherUseResult(VoucherUseOutcome.Unchanged);
        if (redemption.Status == VoucherRedemptionStatus.Released)
            return new VoucherUseResult(VoucherUseOutcome.Refused, "The hold was already returned; hold the use again before consuming it.");

        var voucher = await voucherRepository.GetForUpdateAsync(redemption.VoucherId, cancellationToken);
        if (voucher == null)
            return new VoucherUseResult(VoucherUseOutcome.NotFound, "Voucher was not found.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        voucher.ReservedCount = Math.Max(0, voucher.ReservedCount - 1);
        voucher.UsedCount++;
        voucher.UpdatedAt = now;
        redemption.Status = VoucherRedemptionStatus.Redeemed;
        redemption.RedeemedAt = now;
        redemption.UpdatedAt = now;
        return new VoucherUseResult(VoucherUseOutcome.Applied);
    }

    public async Task<VoucherUseResult> ReleaseAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var redemption = await voucherRepository.GetRedemptionByOrderAsync(orderId, cancellationToken);
        if (redemption == null)
            return new VoucherUseResult(VoucherUseOutcome.NotFound, "The order holds no voucher use.");
        // Already returned, or consumed by a payment: a refund never gives a consumed use back.
        if (redemption.Status != VoucherRedemptionStatus.Reserved)
            return new VoucherUseResult(VoucherUseOutcome.Unchanged);

        var voucher = await voucherRepository.GetForUpdateAsync(redemption.VoucherId, cancellationToken);
        if (voucher == null)
            return new VoucherUseResult(VoucherUseOutcome.NotFound, "Voucher was not found.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        voucher.ReservedCount = Math.Max(0, voucher.ReservedCount - 1);
        voucher.UpdatedAt = now;
        redemption.Status = VoucherRedemptionStatus.Released;
        redemption.ReleasedAt = now;
        redemption.UpdatedAt = now;
        return new VoucherUseResult(VoucherUseOutcome.Applied);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private sealed record VoucherFields(
        decimal DiscountValue,
        decimal MinOrderAmount,
        decimal? MaxDiscountAmount,
        int UsageLimit,
        DateTime ExpiresAt,
        string? Description);

    // Checks the fields shared by create and update; adds an error per bad field and returns the cleaned values.
    private static VoucherFields ValidateFields(
        Dictionary<string, string[]> errors,
        VoucherType type,
        decimal? discountValue,
        decimal? minOrderAmount,
        decimal? maxDiscountAmount,
        int usageLimit,
        DateTime? expiresAt,
        string? description,
        DateTime now)
    {
        if (!discountValue.HasValue)
            errors["discountValue"] = ["Discount value is required."];
        else if (discountValue.Value <= 0)
            errors["discountValue"] = ["Discount value must be greater than 0."];
        else if (type == VoucherType.Percentage && discountValue.Value > 100)
            errors["discountValue"] = ["A percentage discount cannot exceed 100."];
        else if (type == VoucherType.Percentage && decimal.Round(discountValue.Value, 2) != discountValue.Value)
            errors["discountValue"] = ["A percentage has at most 2 decimal places."];
        else if (type == VoucherType.FixedAmount && discountValue.Value != decimal.Truncate(discountValue.Value))
            errors["discountValue"] = ["A fixed discount must be a whole number of đồng."];

        var min = minOrderAmount ?? 0m;
        if (min < 0 || min != decimal.Truncate(min))
            errors["minOrderAmount"] = ["Minimum order must be a whole, non-negative number of đồng."];

        if (maxDiscountAmount.HasValue && (maxDiscountAmount.Value < 0 || maxDiscountAmount.Value != decimal.Truncate(maxDiscountAmount.Value)))
            errors["maxDiscountAmount"] = ["Maximum discount must be a whole, non-negative number of đồng."];

        if (usageLimit < 1)
            errors["usageLimit"] = ["Usage limit must be at least 1."];

        DateTime expires = default;
        if (!expiresAt.HasValue)
            errors["expiresAt"] = ["Expiry date is required."];
        else
        {
            expires = ToUtc(expiresAt.Value);
            if (expires <= now)
                errors["expiresAt"] = ["Expiry date must be in the future."];
        }

        var text = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (text is { Length: > MaxDescriptionLength })
            errors["description"] = [$"Description cannot exceed {MaxDescriptionLength} characters."];

        // A cap only makes sense for a percentage; a fixed voucher never takes more than its value.
        return new VoucherFields(
            discountValue ?? 0m,
            min,
            type == VoucherType.Percentage ? maxDiscountAmount : null,
            usageLimit,
            expires,
            text);
    }

    private static void Apply(Voucher voucher, VoucherFields fields)
    {
        voucher.DiscountValue = fields.DiscountValue;
        voucher.MinOrderAmount = fields.MinOrderAmount;
        voucher.MaxDiscountAmount = fields.MaxDiscountAmount;
        voucher.UsageLimit = fields.UsageLimit;
        voucher.ExpiresAt = fields.ExpiresAt;
        voucher.Description = fields.Description;
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static VoucherDto Map(Voucher voucher) => new()
    {
        Id = voucher.Id,
        Code = voucher.Code,
        Description = voucher.Description,
        DiscountType = voucher.DiscountType.ToString(),
        DiscountValue = voucher.DiscountValue,
        MinOrderAmount = voucher.MinOrderAmount,
        MaxDiscountAmount = voucher.MaxDiscountAmount,
        UsageLimit = voucher.UsageLimit,
        UsedCount = voucher.UsedCount,
        ReservedCount = voucher.ReservedCount,
        ExpiresAt = voucher.ExpiresAt,
        IsActive = voucher.IsActive,
        CreatedAt = voucher.CreatedAt
    };
}
