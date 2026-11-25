using FluentAssertions;
using TechStore.Api.Sales;
using Xunit;

namespace TechStore.UnitTests;

public class VietQrWebhookSignatureTests
{
    private const string Secret = "unit-test-secret";
    private const string Body = "{\"transactionId\":\"FT1\",\"amount\":1000}";
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 8, 0, 0, TimeSpan.Zero);

    private static string Stamp(DateTimeOffset at) => at.ToUnixTimeSeconds().ToString();

    [Fact]
    public void Verify_CorrectSignatureAndFreshTimestamp_IsValid()
    {
        // Arrange
        var stamp = Stamp(Now);
        var signature = VietQrWebhookSignature.Sign(Secret, stamp, Body);

        // Act and Assert
        VietQrWebhookSignature.Verify(Secret, stamp, signature, Body, Now).Should().Be(VietQrSignatureResult.Valid);
    }

    [Fact]
    public void Sign_IsLowercaseHexOfAnHmacSha256()
    {
        // Act
        var signature = VietQrWebhookSignature.Sign(Secret, "1700000000", Body);

        // Assert
        signature.Should().MatchRegex("^[0-9a-f]{64}$");
        VietQrWebhookSignature.Sign(Secret, "1700000000", Body).Should().Be(signature);
        VietQrWebhookSignature.Sign("other", "1700000000", Body).Should().NotBe(signature);
    }

    [Fact]
    public void Verify_SignatureInUpperCase_IsStillAccepted()
    {
        // Arrange
        var stamp = Stamp(Now);
        var signature = VietQrWebhookSignature.Sign(Secret, stamp, Body).ToUpperInvariant();

        // Act and Assert
        VietQrWebhookSignature.Verify(Secret, stamp, signature, Body, Now).Should().Be(VietQrSignatureResult.Valid);
    }

    [Fact]
    public void Verify_ChangedBody_IsInvalid()
    {
        // Arrange
        var stamp = Stamp(Now);
        var signature = VietQrWebhookSignature.Sign(Secret, stamp, Body);

        // Act and Assert
        VietQrWebhookSignature.Verify(Secret, stamp, signature, Body.Replace("1000", "9000"), Now).Should().Be(VietQrSignatureResult.Invalid);
    }

    [Fact]
    public void Verify_SignedWithAnotherSecret_IsInvalid()
    {
        // Arrange
        var stamp = Stamp(Now);

        // Act and Assert
        VietQrWebhookSignature.Verify(Secret, stamp, VietQrWebhookSignature.Sign("other", stamp, Body), Body, Now).Should().Be(VietQrSignatureResult.Invalid);
    }

    [Theory]
    [InlineData(-301)]
    [InlineData(301)]
    public void Verify_TimestampOutsideFiveMinutes_IsInvalid(int offsetSeconds)
    {
        // Arrange
        var stamp = Stamp(Now.AddSeconds(offsetSeconds));
        var signature = VietQrWebhookSignature.Sign(Secret, stamp, Body);

        // Act and Assert
        VietQrWebhookSignature.Verify(Secret, stamp, signature, Body, Now).Should().Be(VietQrSignatureResult.Invalid);
    }

    [Theory]
    [InlineData(-299)]
    [InlineData(299)]
    public void Verify_TimestampInsideFiveMinutes_IsValid(int offsetSeconds)
    {
        // Arrange
        var stamp = Stamp(Now.AddSeconds(offsetSeconds));
        var signature = VietQrWebhookSignature.Sign(Secret, stamp, Body);

        // Act and Assert
        VietQrWebhookSignature.Verify(Secret, stamp, signature, Body, Now).Should().Be(VietQrSignatureResult.Valid);
    }

    [Theory]
    [InlineData(null, "abc")]
    [InlineData("", "abc")]
    [InlineData("not-a-number", "abc")]
    [InlineData("1764057600", null)]
    [InlineData("1764057600", "")]
    [InlineData("99999999999999999999", "abc")]
    public void Verify_MissingOrMalformedHeaders_IsInvalid(string? stamp, string? signature)
    {
        // Act and Assert
        VietQrWebhookSignature.Verify(Secret, stamp, signature, Body, Now).Should().Be(VietQrSignatureResult.Invalid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Verify_NoSecretConfigured_ReportsNotConfigured(string? secret)
    {
        // Arrange
        var stamp = Stamp(Now);

        // Act and Assert
        VietQrWebhookSignature.Verify(secret, stamp, "anything", Body, Now).Should().Be(VietQrSignatureResult.NotConfigured);
    }
}
