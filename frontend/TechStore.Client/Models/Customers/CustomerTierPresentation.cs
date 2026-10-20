namespace TechStore.Client.Models.Customers;

public sealed record CustomerTierVisual(
    string Tier,
    string Label,
    string BadgeLabel,
    string FillResourceKey,
    string TextResourceKey);

public static class CustomerTierPresentation
{
    public static IReadOnlyList<CustomerTierVisual> All { get; } =
    [
        FromTier("Standard"),
        FromTier("Silver"),
        FromTier("Gold"),
        FromTier("Platinum")
    ];

    public static CustomerTierVisual FromTier(string? tier) => tier switch
    {
        "Silver" => new("Silver", "Bạc", "◈ Bạc", "TechStoreNeutralFillBrush", "TechStoreNeutralTextBrush"),
        "Gold" => new("Gold", "Vàng", "★ Vàng", "TechStoreWarningFillBrush", "TechStoreWarningTextBrush"),
        "Platinum" => new("Platinum", "Bạch kim", "✦ Bạch kim", "TechStoreAccentFillBrush", "TechStoreAccentTextBrush"),
        _ => new("Standard", "Tiêu chuẩn", "♙ Tiêu chuẩn", "TechStoreInfoFillBrush", "TechStoreInfoTextBrush")
    };
}
