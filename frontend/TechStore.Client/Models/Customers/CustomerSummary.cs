namespace TechStore.Client.Models.Customers;

public sealed record CustomerSummary(
    Guid Id,
    string FullName,
    string Phone,
    int LoyaltyPoints,
    string Tier,
    bool IsActive,
    DateTime CreatedAt,
    int OrderCount,
    decimal TotalSpent,
    DateTime? LastPurchaseAt);
