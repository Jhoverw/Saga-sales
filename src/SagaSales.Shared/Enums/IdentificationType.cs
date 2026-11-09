namespace SagaSales.Shared.Enums;

/// <summary>
/// Buyer identification types with the codes the SRI uses on an invoice (spec 002, FR-003).
/// The numeric value is the SRI code, so it can be sent to the invoicing provider as is.
/// </summary>
public enum IdentificationType : short
{
    Ruc = 4,
    Cedula = 5,
    Passport = 6,
    FinalConsumer = 7,
    Foreign = 8,
}
