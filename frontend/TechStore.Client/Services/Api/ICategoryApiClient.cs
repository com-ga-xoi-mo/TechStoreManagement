using TechStore.Client.Features.Categories.Models;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.Services.Api;

public interface ICategoryApiClient
{
    Task<IReadOnlyList<CategoryNode>> GetTreeAsync(CancellationToken cancellationToken = default);
    Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public enum CategoryApiFailure
{
    SessionRequired,
    Unauthorized,
    Forbidden,
    Conflict,
    BadRequest,
    NotFound,
    Unavailable,
    InvalidResponse
}

public sealed class CategoryApiException(CategoryApiFailure failure, string? message = null)
    : Exception(message ?? "Category data operation failed.")
{
    public CategoryApiFailure Failure { get; } = failure;
}
