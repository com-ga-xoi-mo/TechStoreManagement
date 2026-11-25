using System.Security.Cryptography;
using System.Text;

namespace TechStore.Api.Sales;

public enum VietQrSignatureResult
{
    Valid,
    NotConfigured,
    Invalid
}

/// <summary>
/// Sandbox webhook signature: lowercase hex HMAC-SHA256 of <c>timestamp + "." + rawBody</c> with the shared secret,
/// the timestamp being Unix seconds within five minutes of the server clock.
/// </summary>
public static class VietQrWebhookSignature
{
    public static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(5);

    public static string Sign(string secret, string timestamp, string rawBody)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var mac = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(timestamp + "." + rawBody));
        return Convert.ToHexString(mac).ToLowerInvariant();
    }

    public static VietQrSignatureResult Verify(string? secret, string? timestamp, string? signature, string rawBody, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(secret)) return VietQrSignatureResult.NotConfigured;
        if (string.IsNullOrWhiteSpace(timestamp) || string.IsNullOrWhiteSpace(signature)
            || !long.TryParse(timestamp, out var seconds))
            return VietQrSignatureResult.Invalid;

        DateTimeOffset sent;
        try
        {
            sent = DateTimeOffset.FromUnixTimeSeconds(seconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return VietQrSignatureResult.Invalid;
        }

        if ((now - sent).Duration() > Tolerance) return VietQrSignatureResult.Invalid;

        var expected = Encoding.ASCII.GetBytes(Sign(secret, timestamp, rawBody));
        var given = Encoding.ASCII.GetBytes(signature.Trim().ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(expected, given) ? VietQrSignatureResult.Valid : VietQrSignatureResult.Invalid;
    }
}
