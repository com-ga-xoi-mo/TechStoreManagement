using TechStore.Shared.Enums;

namespace TechStore.Api.Identity.Repositories;

public sealed record UserSearchCriteria(string? Keyword, RoleType? Role, bool? IsActive, int Page, int PageSize);
