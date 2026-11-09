using System.Globalization;
using SagaSales.Shared.Formatting;
using Xunit;

namespace SagaSales.UnitTests.Shared;

/// <summary>Owner decision 2026-11-08: the decimal separator is always the point.</summary>
public class AppCultureTests
{
    [Fact]
    public void The_app_culture_uses_the_point_as_decimal_separator()
    {
        var numbers = AppCulture.Current.NumberFormat;

        Assert.Equal(".", numbers.NumberDecimalSeparator);
        Assert.Equal(".", numbers.CurrencyDecimalSeparator);
        Assert.Equal(".", numbers.PercentDecimalSeparator);
        Assert.Equal(",", numbers.NumberGroupSeparator);
    }

    [Fact]
    public void Numbers_and_money_are_formatted_with_the_point()
    {
        Assert.Equal("1,234.50", 1234.5m.ToString("N2", AppCulture.Current));
        Assert.Equal("0.15", 0.15m.ToString("0.##", AppCulture.Current));
        Assert.Contains("1,234.50", 1234.5m.ToString("C", AppCulture.Current), StringComparison.Ordinal);
        Assert.DoesNotContain("1.234,50", 1234.5m.ToString("C", AppCulture.Current), StringComparison.Ordinal);
    }

    [Fact]
    public void A_culture_built_without_culture_data_keeps_the_same_separators()
    {
        var culture = AppCulture.Create("xx-not-a-culture");

        Assert.Equal("12.75", 12.75m.ToString("0.00", culture));
    }

    [Theory]
    [InlineData("1.5", 1.5)]
    [InlineData(" 12.75 ", 12.75)]
    [InlineData("100", 100)]
    [InlineData("-0.5", -0.5)]
    public void Typed_decimals_with_a_point_are_read(string text, double expected)
    {
        Assert.True(AppCulture.TryParseDecimal(text, out var value));
        Assert.Equal((decimal)expected, value);
    }

    [Theory]
    [InlineData("1,5")]      // the comma is never a decimal separator
    [InlineData("1,234.5")]  // no thousands separator in typed input
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    public void Typed_decimals_with_a_comma_or_text_are_rejected(string? text)
    {
        Assert.False(AppCulture.TryParseDecimal(text, out _));
    }

    [Fact]
    public void Apply_sets_the_culture_of_the_current_thread()
    {
        var before = CultureInfo.CurrentCulture;
        var beforeUi = CultureInfo.CurrentUICulture;
        try
        {
            AppCulture.Apply();

            Assert.Equal("3.25", 3.25m.ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
            CultureInfo.CurrentUICulture = beforeUi;
            CultureInfo.DefaultThreadCurrentCulture = null;
            CultureInfo.DefaultThreadCurrentUICulture = null;
        }
    }
}
