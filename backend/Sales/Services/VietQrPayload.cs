using System.Globalization;
using System.Text;
using System.Security.Cryptography;
using QRCoder;

namespace TechStore.Api.Sales.Services;

/// <summary>
/// The EMVCo "VietQR" string (NAPAS account transfer, dynamic amount) a customer's banking app scans, and the PNG
/// picture of it. Pure functions: nothing here reads configuration or the database.
/// </summary>
public static class VietQrPayload
{
    public const int MaxMemoLength = 25;

    /// <summary>Builds the payload. Throws <see cref="ArgumentException"/> when a field cannot be encoded.</summary>
    public static string Build(string bankBin, string accountNumber, decimal amount, string memo)
    {
        if (bankBin is not { Length: 6 } || !bankBin.All(char.IsAsciiDigit))
            throw new ArgumentException("The bank BIN must be 6 digits.", nameof(bankBin));
        if (string.IsNullOrWhiteSpace(accountNumber) || accountNumber.Length > 19 || !accountNumber.All(char.IsAsciiLetterOrDigit))
            throw new ArgumentException("The account number must be 1 to 19 letters or digits.", nameof(accountNumber));
        if (amount <= 0 || amount != decimal.Truncate(amount) || amount >= 10_000_000_000_000m)
            throw new ArgumentException("The amount must be a positive whole number of dong.", nameof(amount));
        if (string.IsNullOrWhiteSpace(memo) || memo.Length > MaxMemoLength || !memo.All(c => char.IsAsciiLetterOrDigit(c) || c is ' ' or '-'))
            throw new ArgumentException($"The memo must be 1 to {MaxMemoLength} letters, digits, spaces or hyphens.", nameof(memo));

        var beneficiary = Field("00", bankBin) + Field("01", accountNumber);
        var merchantAccount = Field("00", "A000000727") + Field("01", beneficiary) + Field("02", "QRIBFTTA");

        var body = new StringBuilder()
            .Append(Field("00", "01"))
            .Append(Field("01", "12"))
            .Append(Field("38", merchantAccount))
            .Append(Field("53", "704"))
            .Append(Field("54", amount.ToString("0", CultureInfo.InvariantCulture)))
            .Append(Field("58", "VN"))
            .Append(Field("62", Field("08", memo)))
            .Append("6304")
            .ToString();

        return body + Crc16(body).ToString("X4", CultureInfo.InvariantCulture);
    }

    /// <summary>CRC-16/CCITT-FALSE (polynomial 0x1021, start 0xFFFF) over the ASCII bytes of the text.</summary>
    public static ushort Crc16(string text)
    {
        ushort crc = 0xFFFF;
        foreach (var b in Encoding.ASCII.GetBytes(text))
        {
            crc ^= (ushort)(b << 8);
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }

        return crc;
    }

    /// <summary>True when the last four characters are the CRC of everything before them.</summary>
    public static bool HasValidCrc(string payload) =>
        payload.Length > 8
        && payload[^8..^4] == "6304"
        && payload[^4..].Equals(Crc16(payload[..^4]).ToString("X4", CultureInfo.InvariantCulture), StringComparison.Ordinal);

    /// <summary>The payload as rows of 1 (dark) and 0 (light) joined by line feeds, quiet zone included.</summary>
    public static string ToModules(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        return string.Join('\n', data.ModuleMatrix.Select(row =>
            string.Create(row.Length, row, static (span, bits) =>
            {
                for (var i = 0; i < span.Length; i++) span[i] = bits[i] ? '1' : '0';
            })));
    }

    /// <summary>The picture of a payload as a base64 PNG.</summary>
    public static string ToPngBase64(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        return Convert.ToBase64String(new PngByteQRCode(data).GetGraphic(8));
    }

    /// <summary>
    /// A transfer memo that fits the payload: "TS" and 10 characters from an alphabet without 0, O, 1, I, so a
    /// cashier reading it aloud is not misunderstood. Random, not derived from the order.
    /// </summary>
    public static string NewTransferCode() => "TS" + RandomNumberGenerator.GetString("ABCDEFGHJKLMNPQRSTUVWXYZ23456789", 10);

    private static string Field(string id, string value) =>
        id + value.Length.ToString("00", CultureInfo.InvariantCulture) + value;
}
