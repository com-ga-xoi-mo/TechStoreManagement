using TechStore.Api.Common.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Identity.Entities;

public class Role : BaseEntity
{
    public RoleType Name { get; set; }
    public string? Description { get; set; }

    // Navigation properties
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
