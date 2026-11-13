namespace TechStore.Client.Models.Identity;

public sealed record UserAccount(Guid Id, string Username, string FullName, string Email, string? Role,
    bool IsActive, DateTime CreatedAt, DateTime? UpdatedAt)
{
    public string RoleLabel => Role switch
    {
        "Admin" => "Quản trị viên", "Manager" => "Quản lý", "SalesStaff" => "Nhân viên bán hàng",
        "WarehouseStaff" => "Nhân viên kho", _ => "Vai trò chưa hợp lệ"
    };
    public string StatusLabel => IsActive ? "Hoạt động" : "Đã khóa";
}

public sealed record UserAccountQuery(string? Keyword = null, string? Role = null, bool? IsActive = null, int Page = 1, int PageSize = 20);
public sealed record UserAccountSearchResult(IReadOnlyList<UserAccount> Items, int Page, int TotalPages, int TotalCount);
