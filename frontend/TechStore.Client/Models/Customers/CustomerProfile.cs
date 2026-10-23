namespace TechStore.Client.Models.Customers;

public sealed record CustomerProfile(
    Guid Id,
    string FullName,
    string Phone,
    string? Email,
    DateOnly? DateOfBirth,
    string? Address,
    int LoyaltyPoints,
    string Tier,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int OrderCount,
    decimal TotalSpent,
    DateTime? LastPurchaseAt,
    int CompletedOrderCount = 0,
    int CancelledOrderCount = 0);
