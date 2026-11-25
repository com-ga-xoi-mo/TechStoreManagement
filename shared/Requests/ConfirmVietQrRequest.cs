namespace TechStore.Shared.Requests;

// Manual confirmation by Admin or Manager from the bank statement.
public class ConfirmVietQrRequest
{
    public string? ReferenceCode { get; set; }
    public decimal? ReceivedAmount { get; set; }
}
