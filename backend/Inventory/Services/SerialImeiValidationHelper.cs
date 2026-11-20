using System.Text.RegularExpressions;

namespace TechStore.Api.Inventory.Services;

public static partial class SerialImeiValidationHelper
{
    [GeneratedRegex(@"^[0-9]{15}$")]
    private static partial Regex ImeiRegex();

    public const int MaxSerialNumberLength = 100;

    public static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static bool IsValidImei(string? imei) =>
        !string.IsNullOrEmpty(imei) && ImeiRegex().IsMatch(imei);

    public static bool ValidateDeviceIdentifiers(
        string? rawSerial,
        string? rawImei,
        out string? serial,
        out string? imei,
        out string? errorField,
        out string? errorMessage,
        int maxSerialLength = MaxSerialNumberLength)
    {
        serial = Normalize(rawSerial);
        imei = Normalize(rawImei);
        errorField = null;
        errorMessage = null;

        if (serial == null && imei == null)
        {
            errorMessage = "Either SerialNumber or IMEI must be provided.";
            return false;
        }

        if (imei != null && !IsValidImei(imei))
        {
            errorField = "imei";
            errorMessage = "IMEI must be exactly 15 digits.";
            return false;
        }

        if (serial != null && serial.Length > maxSerialLength)
        {
            errorField = "serialNumber";
            errorMessage = $"Serial number cannot exceed {maxSerialLength} characters.";
            return false;
        }

        return true;
    }
}
