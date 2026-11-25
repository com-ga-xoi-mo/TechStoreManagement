namespace TechStore.Api.Sales;

/// <summary>Receiving bank and sandbox webhook settings. Real values come from configuration or backend/.env, never from code.</summary>
public sealed class VietQrOptions
{
    public const string SectionName = "VietQr";

    public string BankBin { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;

    // Empty disables the webhook (503). Sandbox only: no real bank provider is connected.
    public string WebhookSecret { get; set; } = string.Empty;

    public int HoldMinutes { get; set; } = 15;

    // How often the worker expires overdue requests; 0 turns the worker off (tests).
    public int ExpirySweepSeconds { get; set; } = 30;

    public bool IsBankConfigured =>
        !string.IsNullOrWhiteSpace(BankBin) && !string.IsNullOrWhiteSpace(AccountNumber) && !string.IsNullOrWhiteSpace(AccountName);
}
