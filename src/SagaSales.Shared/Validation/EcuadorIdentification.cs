namespace SagaSales.Shared.Validation;

using SagaSales.Shared.Enums;

/// <summary>
/// Validates Ecuadorian buyer identifications (spec 002, FR-004 and FR-005). Shared by the
/// Customer API and the Blazor screens so both reject exactly the same numbers (FR-009).
/// </summary>
public static class EcuadorIdentification
{
    /// <summary>Identification number the SRI uses for "consumidor final".</summary>
    public const string FinalConsumerNumber = "9999999999999";

    private static readonly int[] CedulaCoefficients = [2, 1, 2, 1, 2, 1, 2, 1, 2];

    /// <summary>
    /// Ten digits, a real province (01–24, or 30 for Ecuadorians abroad), third digit below 6
    /// (natural person) and the module-10 check digit.
    /// </summary>
    public static bool IsValidCedula(string? value)
    {
        if (!IsDigits(value, 10) || !HasValidProvince(value!) || Digit(value!, 2) >= 6)
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            var product = Digit(value!, i) * CedulaCoefficients[i];
            sum += product > 9 ? product - 9 : product;
        }

        var check = (10 - (sum % 10)) % 10;
        return check == Digit(value!, 9);
    }

    /// <summary>
    /// Thirteen digits and a real province. Natural person (third digit 0–5): the first ten digits
    /// are a valid cedula and the establishment is not 000. Company (9) or public entity (6): the
    /// structure is checked; the check digit is not enforced so valid RUCs are never blocked.
    /// </summary>
    public static bool IsValidRuc(string? value)
    {
        if (!IsDigits(value, 13) || !HasValidProvince(value!))
        {
            return false;
        }

        var third = Digit(value!, 2);
        return third switch
        {
            <= 5 => IsValidCedula(value![..10]) && value[10..] != "000",
            6 => value![9..] != "0000",
            9 => value![10..] != "000",
            _ => false,
        };
    }

    /// <summary>True for a RUC that belongs to a company or public entity: it needs a business name.</summary>
    public static bool IsCompanyRuc(string? value)
        => IsValidRuc(value) && Digit(value!, 2) is 6 or 9;

    /// <summary>Returns null when the number is valid for its type, or the reason it is not.</summary>
    public static string? Validate(IdentificationType type, string? number)
    {
        var value = number?.Trim() ?? string.Empty;

        return type switch
        {
            IdentificationType.Cedula when !IsValidCedula(value) => "La cédula no es válida.",
            IdentificationType.Ruc when !IsValidRuc(value) => "El RUC no es válido.",
            IdentificationType.Passport or IdentificationType.Foreign when !IsDocumentCode(value)
                => "La identificación debe tener de 3 a 20 letras o números, sin espacios.",
            IdentificationType.FinalConsumer when value != FinalConsumerNumber
                => $"Consumidor final usa el número {FinalConsumerNumber}.",
            IdentificationType.Cedula or IdentificationType.Ruc or IdentificationType.Passport
                or IdentificationType.Foreign or IdentificationType.FinalConsumer => null,
            _ => "Tipo de identificación desconocido.",
        };
    }

    private static bool IsDigits(string? value, int length)
        => value is not null && value.Length == length && value.All(char.IsAsciiDigit);

    private static bool IsDocumentCode(string value)
        => value.Length is >= 3 and <= 20 && value.All(char.IsAsciiLetterOrDigit);

    private static bool HasValidProvince(string value)
    {
        var province = (Digit(value, 0) * 10) + Digit(value, 1);
        return province is (>= 1 and <= 24) or 30;
    }

    private static int Digit(string value, int index) => value[index] - '0';
}
