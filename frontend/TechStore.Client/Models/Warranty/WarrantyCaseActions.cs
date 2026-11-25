using TechStore.Client.Services.Navigation;

namespace TechStore.Client.Models.Warranty;

public sealed record WarrantyCaseActions(bool Inspect, bool Repaired, bool DecideReplacement, bool Return)
{
    public static WarrantyCaseActions For(string? status, string? role) => new(
        status == "Received" && RoleAccess.CanInspectWarranty(role),
        status == "Repairing" && RoleAccess.CanInspectWarranty(role),
        status == "PendingReplacement" && RoleAccess.CanDecideWarrantyReplacement(role),
        status is "Repaired" or "Rejected" && RoleAccess.CanReceiveWarranty(role));
}
