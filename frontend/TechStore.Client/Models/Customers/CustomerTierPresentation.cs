using TechStore.Client.Styles;

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
        "Silver" => new("Silver", "Bạc", "◈ Bạc", AppColors.NeutralFill, AppColors.NeutralText),
        "Gold" => new("Gold", "Vàng", "★ Vàng", AppColors.WarningFill, AppColors.WarningText),
        "Platinum" => new("Platinum", "Bạch kim", "✦ Bạch kim", AppColors.AccentFill, AppColors.AccentText),
        _ => new("Standard", "Tiêu chuẩn", "♙ Tiêu chuẩn", AppColors.InfoFill, AppColors.InfoText)
    };
}
