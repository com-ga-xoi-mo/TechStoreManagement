using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Catalog;
using Xunit;

namespace TechStore.Client.UnitTests.ViewModels.Catalog;

public sealed class CatalogPermissionsTests
{
    [Theory]
    [InlineData(SessionStatus.Active, "Admin", true)]
    [InlineData(SessionStatus.Active, "Manager", true)]
    [InlineData(SessionStatus.Active, "SalesStaff", false)]
    [InlineData(SessionStatus.Active, "WarehouseStaff", false)]
    [InlineData(SessionStatus.Active, "admin", false)]
    [InlineData(SessionStatus.Anonymous, "Admin", false)]
    [InlineData(SessionStatus.Expired, "Admin", false)]
    public void CanManage_SessionCombinations_ReturnsExpectedResult(SessionStatus status, string? role, bool expected)
    {
        // Arrange
        var snapshot = new SessionSnapshot(
            Generation: 1,
            Status: status,
            AccessToken: status == SessionStatus.Active ? "token" : null,
            RefreshToken: null,
            Username: "user",
            FullName: "User Name",
            Role: role);

        // Act
        var result = CatalogPermissions.CanManage(snapshot);

        // Assert
        Assert.Equal(expected, result);
    }
}
