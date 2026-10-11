namespace TechStore.Shared.Requests;

public class SerialImeiImportItemRequest
{
    public string? SerialNumber { get; set; }
    public string? Imei { get; set; }
    public decimal? UnitCost { get; set; }
    public int? WarrantyMonths { get; set; }
}
