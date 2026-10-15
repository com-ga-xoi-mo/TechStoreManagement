namespace TechStore.Shared.Requests;

public class UpdateStoreSettingsRequest
{
    public string StoreName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
