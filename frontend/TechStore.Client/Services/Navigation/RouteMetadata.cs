namespace TechStore.Client.Services.Navigation;

public enum RouteId
{
    Dashboard,
    Catalog,
    Inventory,
    Orders,
    Customers,
    ThemePreview // Development only
}

public sealed record RouteMetadata(
    RouteId Id,
    string Label,
    string IconSymbol,
    string? RequiredCapabilityKey = null);
