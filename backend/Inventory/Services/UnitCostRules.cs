namespace TechStore.Api.Inventory.Services;

// Stock may only enter the system with a known cost, so margin reports never rest on a guess.
internal static class UnitCostRules
{
    public const decimal Max = 1_000_000_000_000m;

    // numeric(18,2) holds at most this much; a line total above it would fail in the database.
    public const decimal MaxLineTotal = 9_999_999_999_999_999m;

    public static string? Check(decimal? cost) => cost switch
    {
        null => "Unit cost is required.",
        < 0 => "Unit cost must be greater than or equal to zero.",
        > Max => $"Unit cost must not exceed {Max:N0}.",
        _ => null
    };
}
