using TechStore.Api.Warranty.Entities;
using TechStore.Api.Warranty.Services;
using static TechStore.Api.Warranty.Entities.WarrantyCaseStatus;

namespace TechStore.UnitTests.Warranty;

public sealed class WarrantyCaseTransitionsTests
{
    [Fact]
    public void CanTransition_AllStatusPairs_AllowOnlyLifecycleTransitions()
    {
        HashSet<(WarrantyCaseStatus, WarrantyCaseStatus)> allowed =
        [
            (Received, Repairing), (Received, PendingReplacement), (Received, Rejected),
            (Repairing, Repaired), (PendingReplacement, Repairing), (PendingReplacement, Replaced),
            (Repaired, ReturnedToCustomer), (Rejected, ReturnedToCustomer)
        ];

        foreach (var from in Enum.GetValues<WarrantyCaseStatus>())
        {
            Assert.Equal(from is not (Replaced or ReturnedToCustomer), WarrantyCaseTransitions.IsOpen(from));
            foreach (var to in Enum.GetValues<WarrantyCaseStatus>())
                Assert.Equal(allowed.Contains((from, to)), WarrantyCaseTransitions.CanTransition(from, to));
        }
    }
}
