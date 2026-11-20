using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Inventory.Services;

public record SupplierResult<T>(
    bool IsSuccess,
    T? Data,
    Dictionary<string, string[]>? ValidationErrors,
    bool IsNotFound,
    bool IsConflict,
    string? ConflictReason)
{
    public static SupplierResult<T> Success(T data) =>
        new(true, data, null, false, false, null);

    public static SupplierResult<T> ValidationFailed(Dictionary<string, string[]> errors) =>
        new(false, default, errors, false, false, null);

    public static SupplierResult<T> NotFound() =>
        new(false, default, null, true, false, null);

    public static SupplierResult<T> Conflict(string reason) =>
        new(false, default, null, false, true, reason);
}

public interface ISupplierService
{
    Task<SupplierResult<PagedResultDto<SupplierDto>>> SearchAsync(
        SupplierFilterRequest request,
        CancellationToken cancellationToken = default);

    Task<SupplierResult<SupplierDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<SupplierResult<SupplierDto>> CreateAsync(
        SaveSupplierRequest request,
        CancellationToken cancellationToken = default);

    Task<SupplierResult<SupplierDto>> UpdateAsync(
        Guid id,
        SaveSupplierRequest request,
        CancellationToken cancellationToken = default);

    Task<SupplierResult<SupplierDto>> SetStatusAsync(
        Guid id,
        SetSupplierStatusRequest request,
        CancellationToken cancellationToken = default);
}
