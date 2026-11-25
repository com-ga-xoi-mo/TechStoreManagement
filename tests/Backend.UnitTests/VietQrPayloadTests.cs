using System.Text.RegularExpressions;
using FluentAssertions;
using TechStore.Api.Sales.Services;
using Xunit;

namespace TechStore.UnitTests;

public class VietQrPayloadTests
{
    [Fact]
    public void Crc16_StandardCheckString_MatchesTheCcittFalseVector()
    {
        // Act
        var crc = VietQrPayload.Crc16("123456789");

        // Assert
        crc.Should().Be(0x29B1);
    }

    [Fact]
    public void Build_ValidInput_EncodesBankAccountAmountAndMemoInTheNapasLayout()
    {
        // Act
        var payload = VietQrPayload.Build("970422", "0123456789", 9_500_000m, "TSABCD234567");

        // Assert
        var top = Parse(payload);
        top.Select(f => f.Id).Should().Equal("00", "01", "38", "53", "54", "58", "62", "63");
        Value(top, "00").Should().Be("01");
        Value(top, "01").Should().Be("12");
        Value(top, "53").Should().Be("704");
        Value(top, "54").Should().Be("9500000");
        Value(top, "58").Should().Be("VN");

        var merchant = Parse(Value(top, "38"));
        Value(merchant, "00").Should().Be("A000000727");
        Value(merchant, "02").Should().Be("QRIBFTTA");
        var beneficiary = Parse(Value(merchant, "01"));
        Value(beneficiary, "00").Should().Be("970422");
        Value(beneficiary, "01").Should().Be("0123456789");

        Value(Parse(Value(top, "62")), "08").Should().Be("TSABCD234567");
        VietQrPayload.HasValidCrc(payload).Should().BeTrue();
    }

    [Fact]
    public void Build_SameInputTwice_GivesTheSamePayload()
    {
        // Act
        var first = VietQrPayload.Build("970422", "0123456789", 1_000m, "TSABCD234567");
        var second = VietQrPayload.Build("970422", "0123456789", 1_000m, "TSABCD234567");

        // Assert
        second.Should().Be(first);
    }

    [Fact]
    public void HasValidCrc_AnyChangedCharacter_IsRejected()
    {
        // Arrange
        var payload = VietQrPayload.Build("970422", "0123456789", 9_500_000m, "TSABCD234567");
        var tampered = payload.Replace("9500000", "9500001");

        // Act and Assert
        VietQrPayload.HasValidCrc(tampered).Should().BeFalse();
        VietQrPayload.HasValidCrc("short").Should().BeFalse();
    }

    [Theory]
    [InlineData("97042", "0123456789", 1000, "TSABCD234567")]
    [InlineData("97042A", "0123456789", 1000, "TSABCD234567")]
    [InlineData("970422", "", 1000, "TSABCD234567")]
    [InlineData("970422", "01234 56789", 1000, "TSABCD234567")]
    [InlineData("970422", "01234567890123456789", 1000, "TSABCD234567")]
    [InlineData("970422", "0123456789", 0, "TSABCD234567")]
    [InlineData("970422", "0123456789", -5, "TSABCD234567")]
    [InlineData("970422", "0123456789", 1000, "")]
    [InlineData("970422", "0123456789", 1000, "TS ABCD 234567 ABCDEFGHIJKL")]
    [InlineData("970422", "0123456789", 1000, "TS_ABCD")]
    public void Build_InvalidField_Throws(string bin, string account, int amount, string memo)
    {
        // Act
        var act = () => VietQrPayload.Build(bin, account, amount, memo);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Build_FractionalAmount_Throws()
    {
        // Act
        var act = () => VietQrPayload.Build("970422", "0123456789", 1000.5m, "TSABCD234567");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ToPngBase64_Payload_ReturnsAValidPngPicture()
    {
        // Act
        var bytes = Convert.FromBase64String(VietQrPayload.ToPngBase64(VietQrPayload.Build("970422", "0123456789", 1_000m, "TSABCD234567")));

        // Assert
        bytes.Take(8).Should().Equal(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);
        bytes.Length.Should().BeGreaterThan(200);
    }

    [Fact]
    public void NewTransferCode_Many_AreShortUnambiguousAndDifferent()
    {
        // Act
        var codes = Enumerable.Range(0, 2_000).Select(_ => VietQrPayload.NewTransferCode()).ToList();

        // Assert
        codes.Should().OnlyContain(c => Regex.IsMatch(c, "^TS[A-HJ-NP-Z2-9]{10}$"));
        codes.Distinct().Count().Should().Be(codes.Count);
        codes[0].Length.Should().BeLessThanOrEqualTo(VietQrPayload.MaxMemoLength);
    }

    private static string Value(List<(string Id, string Value)> fields, string id) => fields.Single(f => f.Id == id).Value;

    // Reads "ID LL value" fields; the tests check structure, not a hand-written copy of the same encoder.
    private static List<(string Id, string Value)> Parse(string text)
    {
        var fields = new List<(string, string)>();
        var i = 0;
        while (i < text.Length)
        {
            var id = text.Substring(i, 2);
            var length = int.Parse(text.Substring(i + 2, 2));
            fields.Add((id, text.Substring(i + 4, length)));
            i += 4 + length;
        }

        return fields;
    }
}
