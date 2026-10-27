using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Catalog.Services;

public record CategoryTreeResult(bool IsSuccess, IReadOnlyList<CategoryTreeNodeDto> Data);

public record CreateCategoryResult(
    bool IsSuccess,
    CategoryDto? Data,
    Dictionary<string, string[]>? ValidationErrors,
    bool IsConflict,
    string? ConflictReason)
{
    public static CreateCategoryResult Success(CategoryDto data) =>
        new(true, data, null, false, null);

    public static CreateCategoryResult ValidationFailed(Dictionary<string, string[]> errors) =>
        new(false, null, errors, false, null);

    public static CreateCategoryResult Conflict(string reason) =>
        new(false, null, null, true, reason);
}

public record UpdateCategoryResult(
    bool IsSuccess,
    CategoryDto? Data,
    Dictionary<string, string[]>? ValidationErrors,
    bool IsNotFound,
    bool IsConflict,
    string? ConflictReason)
{
    public static UpdateCategoryResult Success(CategoryDto data) => new(true, data, null, false, false, null);
    public static UpdateCategoryResult NotFound() => new(false, null, null, true, false, null);
    public static UpdateCategoryResult ValidationFailed(Dictionary<string, string[]> errors) => new(false, null, errors, false, false, null);
    public static UpdateCategoryResult Conflict(string reason) => new(false, null, null, false, true, reason);
}

public record DeleteCategoryResult(
    bool IsSuccess,
    bool IsNotFound,
    Dictionary<string, string[]>? ValidationErrors,
    bool IsConflict,
    string? ConflictReason)
{
    public static DeleteCategoryResult Success() => new(true, false, null, false, null);
    public static DeleteCategoryResult NotFound() => new(false, true, null, false, null);
    public static DeleteCategoryResult ValidationFailed(Dictionary<string, string[]> errors) => new(false, false, errors, false, null);
    public static DeleteCategoryResult Conflict(string reason) => new(false, false, null, true, reason);
}

public interface ICategoryService
{
    Task<CategoryTreeResult> GetTreeAsync(CancellationToken cancellationToken = default);
    Task<CreateCategoryResult> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<UpdateCategoryResult> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<DeleteCategoryResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
