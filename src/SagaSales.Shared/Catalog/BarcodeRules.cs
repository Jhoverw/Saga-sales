namespace SagaSales.Shared.Catalog;

/// <summary>
/// Reglas de los códigos de barras (spec 010, HU-10.2): se acepta cualquier código sin espacios de 3 a 30
/// caracteres, y si tiene forma de EAN-8, UPC-A, EAN-13 o GTIN-14 se revisa el dígito verificador para advertir
/// (no bloquear: hay etiquetas propias y códigos internos).
/// </summary>
public static class BarcodeRules
{
    public const int MinLength = 3;

    public const int MaxLength = 30;

    public static string Normalize(string? value) => (value ?? string.Empty).Trim();

    public static bool IsAcceptable(string? value)
    {
        var code = Normalize(value);
        return code.Length is >= MinLength and <= MaxLength && !code.Any(char.IsWhiteSpace);
    }

    /// <summary>8, 12, 13 o 14 dígitos: la forma de los códigos GS1 que trae el fabricante.</summary>
    public static bool IsGtinShaped(string? value)
    {
        var code = Normalize(value);
        return code.Length is 8 or 12 or 13 or 14 && code.All(char.IsAsciiDigit);
    }

    /// <summary>Dígito verificador GS1 (módulo 10, pesos 3 y 1 desde la derecha).</summary>
    public static bool HasValidCheckDigit(string? value)
    {
        var code = Normalize(value);
        if (!IsGtinShaped(code))
        {
            return false;
        }

        var sum = 0;
        for (var i = code.Length - 2; i >= 0; i--)
        {
            var weight = (code.Length - 2 - i) % 2 == 0 ? 3 : 1;
            sum += (code[i] - '0') * weight;
        }

        var check = (10 - (sum % 10)) % 10;
        return check == code[^1] - '0';
    }

    /// <summary>Texto de advertencia, o null si el código está bien o no es GS1.</summary>
    public static string? Warning(string? value)
        => IsGtinShaped(value) && !HasValidCheckDigit(value)
            ? "El dígito verificador no cuadra: revisa que el código esté bien escrito."
            : null;

    /// <summary>Lo escrito parece salido de un lector (solo dígitos, 8 o más) y no un nombre.</summary>
    public static bool LooksScanned(string? value)
    {
        var code = Normalize(value);
        return code.Length >= 8 && code.All(char.IsAsciiDigit);
    }
}
