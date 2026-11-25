using FluentAssertions;
using TechStore.Api.Common.Data;
using TechStore.Api.Customers.Services;
using Xunit;

namespace TechStore.UnitTests;

/// <summary>The demo customers carry the tier their points earn, and their starting balances stay as they were.</summary>
public class DemoSeedCustomerTierTests
{
    [Fact]
    public void Customers_EveryTierMatchesWhatItsPointsEarn()
    {
        // Arrange / Act
        var customers = DemoSeedManifest.Customers;

        // Assert
        customers.Should().OnlyContain(x => x.Tier == LoyaltyRules.TierFor(x.LoyaltyPoints),
            "a tier that differs from its points would be changed by the first points movement and would give a wrong VIP discount");
    }

    [Fact]
    public void Customers_KeepTheirStartingBalances()
    {
        // Arrange / Act
        var balances = DemoSeedManifest.Customers.Select(x => (x.Phone, x.LoyaltyPoints)).ToArray();

        // Assert
        balances.Should().Equal(
            ("0901234567", 150), ("0912345678", 600),
            ("0930000001", 100), ("0930000002", 200), ("0930000003", 300), ("0930000004", 400),
            ("0930000005", 500), ("0930000006", 600), ("0930000007", 700), ("0930000008", 800));
    }
}
