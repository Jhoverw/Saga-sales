using SagaSales.EcuadorInvoicing.Utilities;
using Xunit;

namespace SagaSales.UnitTests.EcuadorInvoicing;

/// <summary>
/// The clave de acceso is the identifier the SRI validates: 49 digits, date first and a
/// modulo-11 check digit. These tests pin the format so a refactor cannot silently break it.
/// </summary>
public class ClaveAccesoGeneratorTests
{
    private const string Ruc = "1790012345001";

    private static readonly DateTime Emission =
        new(2026, 3, 15, 10, 30, 0, DateTimeKind.Utc);

    private static string NewClave(int numero = 42, string codigo = "12345678")
        => ClaveAccesoGenerator.Generate(Emission, "01", Ruc, "1", "001001", numero, codigo);

    [Fact]
    public void Generate_returns_49_digits()
    {
        var clave = NewClave();

        Assert.Equal(49, clave.Length);
        Assert.All(clave, c => Assert.True(char.IsDigit(c), $"'{c}' is not a digit in {clave}"));
    }

    [Fact]
    public void Generate_starts_with_the_emission_date_as_ddMMyyyy()
    {
        var clave = NewClave();

        Assert.Equal("15032026", clave[..8]);
    }

    [Fact]
    public void Generate_puts_the_ruc_after_the_comprobant_type()
    {
        var clave = NewClave();

        // fecha(8) + tipo(2) + RUC(13)
        Assert.Equal(Ruc, clave.Substring(10, 13));
    }

    [Fact]
    public void Generate_recomputes_the_check_digit()
    {
        var clave = NewClave();

        Assert.Equal(ExpectedCheckDigit(clave[..48]), clave[^1] - '0');
    }

    [Fact]
    public void Generate_changes_the_check_digit_when_the_sequence_changes()
    {
        var first = NewClave(numero: 42);
        var second = NewClave(numero: 43);

        Assert.NotEqual(first, second);
        Assert.NotEqual(first[^1], second[^1]);
    }

    /// <summary>Independent modulo-11 implementation, so the test is not a copy of the code.</summary>
    private static int ExpectedCheckDigit(string body)
    {
        var factor = 2;
        var total = 0;

        for (var i = body.Length - 1; i >= 0; i--)
        {
            total += (body[i] - '0') * factor;
            factor = factor == 7 ? 2 : factor + 1;
        }

        var remainder = total % 11;
        var digit = 11 - remainder;

        return digit switch
        {
            11 => 0,
            10 => 1,
            _ => digit,
        };
    }
}
