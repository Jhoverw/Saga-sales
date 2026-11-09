namespace SagaSales.Shared.Formatting;

using System.Globalization;

/// <summary>
/// The one number format of Saga-Sales (constitution, Technology Constraints): the decimal separator
/// is always the point (.) — on screens, in inputs, CSV, PDF and messages. The comma is only the
/// thousands separator when a number is displayed. Dates and names keep the Spanish (Ecuador) format.
/// </summary>
public static class AppCulture
{
    public const string DecimalSeparator = ".";

    public const string GroupSeparator = ",";

    /// <summary>Read-only culture every service and screen formats with.</summary>
    public static CultureInfo Current { get; } = CultureInfo.ReadOnly(Create());

    /// <summary>
    /// Builds the culture from Spanish (Ecuador) with the point as decimal separator. When the host has
    /// no culture data (invariant globalization), it falls back to the invariant culture with the same
    /// separators, so numbers are formatted identically everywhere.
    /// </summary>
    public static CultureInfo Create(string baseCulture = "es-EC")
    {
        CultureInfo culture;
        try
        {
            culture = (CultureInfo)CultureInfo.GetCultureInfo(baseCulture).Clone();
        }
        catch (CultureNotFoundException)
        {
            culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        }

        var numbers = culture.NumberFormat;
        numbers.NumberDecimalSeparator = DecimalSeparator;
        numbers.CurrencyDecimalSeparator = DecimalSeparator;
        numbers.PercentDecimalSeparator = DecimalSeparator;
        numbers.NumberGroupSeparator = GroupSeparator;
        numbers.CurrencyGroupSeparator = GroupSeparator;
        numbers.PercentGroupSeparator = GroupSeparator;
        numbers.CurrencySymbol = "$";

        return culture;
    }

    /// <summary>Makes <see cref="Current"/> the culture of the process and of every new thread.</summary>
    public static void Apply()
    {
        CultureInfo.DefaultThreadCurrentCulture = Current;
        CultureInfo.DefaultThreadCurrentUICulture = Current;
        CultureInfo.CurrentCulture = Current;
        CultureInfo.CurrentUICulture = Current;
    }

    /// <summary>
    /// Reads a decimal typed by a person. Only the point is accepted as decimal separator and no
    /// thousands separator is allowed, so "1,5" is rejected instead of silently becoming 15.
    /// </summary>
    public static bool TryParseDecimal(string? text, out decimal value)
        => decimal.TryParse(
            text?.Trim(),
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out value);
}
