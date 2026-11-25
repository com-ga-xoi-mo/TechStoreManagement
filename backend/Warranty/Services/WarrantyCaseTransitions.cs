using TechStore.Api.Warranty.Entities;

namespace TechStore.Api.Warranty.Services;

public static class WarrantyCaseTransitions
{
    public static bool IsOpen(WarrantyCaseStatus status) => status is WarrantyCaseStatus.Received
        or WarrantyCaseStatus.Repairing or WarrantyCaseStatus.PendingReplacement or WarrantyCaseStatus.Repaired
        or WarrantyCaseStatus.Rejected;

    public static bool CanTransition(WarrantyCaseStatus from, WarrantyCaseStatus to) => (from, to) switch
    {
        (WarrantyCaseStatus.Received, WarrantyCaseStatus.Repairing or WarrantyCaseStatus.PendingReplacement or WarrantyCaseStatus.Rejected) => true,
        (WarrantyCaseStatus.Repairing, WarrantyCaseStatus.Repaired) => true,
        (WarrantyCaseStatus.PendingReplacement, WarrantyCaseStatus.Repairing or WarrantyCaseStatus.Replaced) => true,
        (WarrantyCaseStatus.Repaired or WarrantyCaseStatus.Rejected, WarrantyCaseStatus.ReturnedToCustomer) => true,
        _ => false
    };
}
