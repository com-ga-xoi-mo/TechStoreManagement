using System.Text.Json;
using TechStore.Shared.DTOs;

namespace TechStore.UnitTests;

public sealed class UserAccountContractTests
{
    [Fact]
    public void Serialize_Account_ContainsOnlyPublicMetadata()
    {
        var json = JsonSerializer.SerializeToElement(new UserAccountDto());
        Assert.Equal(new[] { "CreatedAt", "Email", "FullName", "Id", "IsActive", "Role", "UpdatedAt", "Username" },
            json.EnumerateObject().Select(x => x.Name).Order().ToArray());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("Role").ValueKind);
    }
}
