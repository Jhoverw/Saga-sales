using SagaSales.Shared.Enums;
using SagaSales.Shared.Validation;
using Xunit;

namespace SagaSales.UnitTests.Shared;

/// <summary>
/// Spec 002, FR-003 to FR-005. Every number here is synthetic: built with the module-10 algorithm,
/// not taken from a real person.
/// </summary>
public class EcuadorIdentificationTests
{
    [Theory]
    [InlineData("0102030400")]
    [InlineData("1700000001")]
    [InlineData("0912345675")]
    [InlineData("2400000010")]
    [InlineData("3000000004")] // province 30: Ecuadorians registered abroad
    public void Cedula_with_a_correct_check_digit_is_valid(string cedula)
    {
        Assert.True(EcuadorIdentification.IsValidCedula(cedula));
    }

    [Theory]
    [InlineData("0102030401")]  // wrong check digit
    [InlineData("1700000002")]  // wrong check digit
    [InlineData("2500000000")]  // province 25 does not exist
    [InlineData("0000000000")]  // province 00 does not exist
    [InlineData("1770000001")]  // third digit 7 is not a natural person
    [InlineData("170000000")]   // 9 digits
    [InlineData("17000000011")] // 11 digits
    [InlineData("17000000A1")]  // not only digits
    [InlineData("")]
    [InlineData(null)]
    public void Invalid_cedulas_are_rejected(string? cedula)
    {
        Assert.False(EcuadorIdentification.IsValidCedula(cedula));
    }

    [Theory]
    [InlineData("1700000001001")] // natural person: a valid cedula plus establishment
    [InlineData("0912345675002")] // natural person, second establishment
    [InlineData("1790012345001")] // private company (third digit 9)
    [InlineData("1760001550001")] // public entity (third digit 6)
    public void Valid_rucs_are_accepted(string ruc)
    {
        Assert.True(EcuadorIdentification.IsValidRuc(ruc));
    }

    [Theory]
    [InlineData("1700000002001")] // natural person whose cedula part is wrong
    [InlineData("1700000001000")] // establishment 000
    [InlineData("1790012345000")] // company with establishment 000
    [InlineData("1760001550000")] // public entity with establishment 0000
    [InlineData("1780012345001")] // third digit 8 is not used
    [InlineData("2590012345001")] // province 25 does not exist
    [InlineData("179001234500")]  // 12 digits
    [InlineData("")]
    [InlineData(null)]
    public void Invalid_rucs_are_rejected(string? ruc)
    {
        Assert.False(EcuadorIdentification.IsValidRuc(ruc));
    }

    [Theory]
    [InlineData("1790012345001", true)]
    [InlineData("1760001550001", true)]
    [InlineData("1700000001001", false)]
    public void Company_rucs_are_told_apart_from_natural_people(string ruc, bool expected)
    {
        Assert.Equal(expected, EcuadorIdentification.IsCompanyRuc(ruc));
    }

    [Theory]
    [InlineData(IdentificationType.Cedula, "1700000001")]
    [InlineData(IdentificationType.Ruc, "1790012345001")]
    [InlineData(IdentificationType.Passport, "AB123456")]
    [InlineData(IdentificationType.Foreign, "X99887766")]
    [InlineData(IdentificationType.FinalConsumer, "9999999999999")]
    public void Validate_accepts_a_valid_number_for_its_type(IdentificationType type, string number)
    {
        Assert.Null(EcuadorIdentification.Validate(type, number));
    }

    [Theory]
    [InlineData(IdentificationType.Cedula, "1700000002")]
    [InlineData(IdentificationType.Cedula, "1700000001001")] // a RUC is not a cedula
    [InlineData(IdentificationType.Ruc, "1700000001")]       // a cedula is not a RUC
    [InlineData(IdentificationType.Passport, "A")]
    [InlineData(IdentificationType.Passport, "AB 123")]
    [InlineData(IdentificationType.FinalConsumer, "1700000001")]
    [InlineData(IdentificationType.Cedula, "")]
    public void Validate_explains_why_a_number_is_rejected(IdentificationType type, string number)
    {
        var error = EcuadorIdentification.Validate(type, number);

        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
