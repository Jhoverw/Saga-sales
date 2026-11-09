using SagaSales.Shared.Catalog;
using Xunit;

namespace SagaSales.UnitTests.Shared;

/// <summary>Spec 010 HU-10.2: códigos de barras aceptados y advertencia del dígito verificador GS1.</summary>
public class BarcodeRulesTests
{
    [Theory]
    [InlineData("4006381333931")]   // EAN-13
    [InlineData("96385074")]        // EAN-8
    [InlineData("036000291452")]    // UPC-A
    [InlineData("10614141000415")]  // GTIN-14 (caja)
    public void Valid_gs1_codes_pass_the_check_digit(string code)
    {
        Assert.True(BarcodeRules.HasValidCheckDigit(code));
        Assert.Null(BarcodeRules.Warning(code));
    }

    [Fact]
    public void A_wrong_check_digit_is_a_warning_not_a_rejection()
    {
        Assert.True(BarcodeRules.IsAcceptable("4006381333932"));
        Assert.False(BarcodeRules.HasValidCheckDigit("4006381333932"));
        Assert.NotNull(BarcodeRules.Warning("4006381333932"));
    }

    [Theory]
    [InlineData("GA12")]
    [InlineData("INT-0001")]
    [InlineData("12345")]
    public void Store_codes_are_accepted_without_warning(string code)
    {
        Assert.True(BarcodeRules.IsAcceptable(code));
        Assert.Null(BarcodeRules.Warning(code));
    }

    [Theory]
    [InlineData("")]
    [InlineData("12")]
    [InlineData("786 123")]
    [InlineData("1234567890123456789012345678901")]
    public void Too_short_too_long_or_with_spaces_is_refused(string code)
        => Assert.False(BarcodeRules.IsAcceptable(code));

    [Theory]
    [InlineData("7861234567890", true)]
    [InlineData("12345678", true)]
    [InlineData("1234567", false)]
    [InlineData("galletas", false)]
    public void Only_long_digit_strings_look_scanned(string text, bool expected)
        => Assert.Equal(expected, BarcodeRules.LooksScanned(text));

    [Fact]
    public void Surrounding_spaces_from_the_reader_are_ignored()
        => Assert.True(BarcodeRules.HasValidCheckDigit(" 4006381333931 "));
}
