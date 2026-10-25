namespace TechStore.Client.Repositories.Catalog;

public enum CategoryApiFailure
{
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
