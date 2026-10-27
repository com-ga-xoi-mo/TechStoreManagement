namespace TechStore.Shared.Requests;

public sealed class AdjustStockRequest
{
    /// <summary>Signed quantity delta. Positive adds physical stock; negative removes it.</summary>
    public int QuantityChange { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? Note { get; set; }
}
