namespace TechStore.Client.Services.Navigation;

public enum ShellDestination
{
    Dashboard,
    Products,
    Inventory,
    Orders,
    Customers,
    Settings
}

public sealed record ShellDestinationInfo(
    ShellDestination Id,
    string Title,
    string Group,
    string Symbol,
    bool IsFooterItem = false);

public static class ShellDestinations
{
    public static IReadOnlyList<ShellDestinationInfo> All { get; } = Array.AsReadOnly<ShellDestinationInfo>(
    [
        new(ShellDestination.Dashboard, "Tổng quan", "Chính", "Home"),
        new(ShellDestination.Products, "Sản phẩm", "Chính", "Shop"),
        new(ShellDestination.Inventory, "Danh mục và Kho", "Chính", "AllApps"),
        new(ShellDestination.Orders, "Đơn hàng", "Chính", "Document"),
        new(ShellDestination.Customers, "Khách hàng", "Chính", "Contact"),
        new(ShellDestination.Settings, "Cài đặt", "Footer", "Setting", IsFooterItem: true)
    ]);

    public static ShellDestinationInfo Get(ShellDestination destination) =>
        All.FirstOrDefault(item => item.Id == destination)
        ?? throw new ArgumentOutOfRangeException(nameof(destination), destination, "Unknown Shell destination.");

    public static bool TryGet(string? route, out ShellDestinationInfo destination)
    {
        destination = All.FirstOrDefault(item =>
            string.Equals(item.Id.ToString(), route, StringComparison.OrdinalIgnoreCase))!;
        return destination is not null;
    }
}
