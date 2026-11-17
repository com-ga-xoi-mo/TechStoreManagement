namespace TechStore.Client.Models.Customers;

/// <summary>The customer chosen for a sale; null on the screen means a walk-in guest.</summary>
public sealed record CustomerChoice(Guid Id, string FullName, string Phone, string Tier);
