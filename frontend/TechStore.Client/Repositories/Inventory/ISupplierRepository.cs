using TechStore.Client.Models.Inventory;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.Repositories.Inventory;

public enum SupplierFailureKind
{
    None,
    BadRequest,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    Network
}

public sealed record SupplierSearchResult(
    bool Succeeded,
    IReadOnlyList<SupplierItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    SupplierFailureKind Failure = SupplierFailureKind.None,
    string? Message = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? FieldErrors = null)
{
    public static SupplierSearchResult Success(PagedResultDto<SupplierDto> paged) =>
        new(
            true,
            paged.Items.Select(Map).ToList(),
            paged.Page,
            paged.PageSize,
            paged.TotalCount,
            paged.TotalPages);

    public static SupplierSearchResult Failed(
        SupplierFailureKind failure,
        string? message = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? fieldErrors = null) =>
        new(false, [], 1, 20, 0, 0, failure, message, fieldErrors);

    public static SupplierItem Map(SupplierDto dto) => new(
        dto.Id,
        dto.Name,
        dto.ContactName,
        dto.Phone,
        dto.Email,
        dto.Address,
        dto.IsActive,
        dto.ReceiptCount,
        dto.CreatedAt,
        dto.UpdatedAt);
}

public sealed record SupplierDetailResult(
    bool Succeeded,
    SupplierItem? Data = null,
    SupplierFailureKind Failure = SupplierFailureKind.None,
    string? Message = null)
{
    public static SupplierDetailResult Success(SupplierDto dto) =>
        new(true, SupplierSearchResult.Map(dto));

    public static SupplierDetailResult Failed(SupplierFailureKind failure, string? message = null) =>
        new(false, null, failure, message);
}

public sealed record SupplierMutationResult(
    bool Succeeded,
    SupplierItem? Data = null,
    SupplierFailureKind Failure = SupplierFailureKind.None,
    string? Message = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? FieldErrors = null)
{
    public static SupplierMutationResult Success(SupplierDto dto) =>
        new(true, SupplierSearchResult.Map(dto));

    public static SupplierMutationResult Failed(
        SupplierFailureKind failure,
        string? message = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? fieldErrors = null) =>
        new(false, null, failure, message, fieldErrors);
}

public interface ISupplierRepository
{
    Task<SupplierSearchResult> GetSuppliersAsync(
        SupplierFilterRequest request,
        CancellationToken cancellationToken = default);

    Task<SupplierDetailResult> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<SupplierMutationResult> CreateAsync(
        SaveSupplierRequest request,
        CancellationToken cancellationToken = default);

    Task<SupplierMutationResult> UpdateAsync(
        Guid id,
        SaveSupplierRequest request,
        CancellationToken cancellationToken = default);

    Task<SupplierMutationResult> SetStatusAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default);
}
