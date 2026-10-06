using TechStore.Api.Common.Entities;

namespace TechStore.Api.Identity.Entities;

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; } = false;

    // Navigation properties
    public virtual User User { get; set; } = null!;
}
