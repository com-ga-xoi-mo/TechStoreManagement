namespace TechStore.Api.Inventory.Services;

// Stock may only enter the system with a known cost, so margin reports never rest on a guess.
internal static class UnitCostRules
{
    public const decimal Max = 1_000_000_000_000m;

    // numeric(18,2) holds at most this much; a line total above it would fail in the database.
    public const decimal MaxLineTotal = 9_999_999_999_999_999m;

    public static string? Check(decimal? cost) => cost switch
    {
        null => "Giá nhập là bắt buộc.",
        < 0 => "Giá nhập không được âm.",
        > Max => "Giá nhập không được vượt quá 1.000.000.000.000 ₫.",
        _ => null
    };
}
