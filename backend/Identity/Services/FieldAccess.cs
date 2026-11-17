using TechStore.Shared.Enums;

namespace TechStore.Api.Identity.Services;

/// <summary>Which roles may read sensitive fields inside business DTOs. One table for Catalog and Inventory.</summary>
public static class FieldAccess
{
    public static bool CanViewProductCost(RoleType role) => role is RoleType.Admin or RoleType.Manager;
}
