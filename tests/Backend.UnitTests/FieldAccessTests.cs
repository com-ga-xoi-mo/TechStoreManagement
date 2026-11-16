using TechStore.Api.Identity.Services;
using TechStore.Shared.Enums;

namespace TechStore.UnitTests;

public sealed class FieldAccessTests
{
    [Theory]
    [InlineData(RoleType.Admin, true)]
    [InlineData(RoleType.Manager, true)]
    [InlineData(RoleType.SalesStaff, false)]
    [InlineData(RoleType.WarehouseStaff, false)]
    public void CanViewProductCost_ByRole_MatchesPermissionTable(RoleType role, bool expected)
    {
        // Act
        var allowed = FieldAccess.CanViewProductCost(role);

        // Assert
        Assert.Equal(expected, allowed);
    }
}
