namespace TechStore.Shared.Requests;

public sealed class UserSearchRequest
{
    public string? Q { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public sealed class UpdateUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public sealed class UpdateUserStatusRequest
{
    public bool? IsActive { get; set; }
}

public sealed class UpdateUserRoleRequest
{
    public string Role { get; set; } = string.Empty;
}

public sealed class ResetUserPasswordRequest
{
    public string Password { get; set; } = string.Empty;
}
