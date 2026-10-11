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

public interface ICategoryService
{
    Task<CategoryTreeResult> GetTreeAsync(CancellationToken cancellationToken = default);
    Task<CreateCategoryResult> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);
}
