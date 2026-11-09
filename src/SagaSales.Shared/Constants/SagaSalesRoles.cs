namespace SagaSales.Shared.Constants;

/// <summary>
/// Staff roles (spec 005). The values are the ones stored in identity's users.role and carried in
/// the token; the comments are the names the screens show.
/// </summary>
public static class SagaSalesRoles
{
    /// <summary>Dueño: everything, including users.</summary>
    public const string Owner = "admin";

    /// <summary>Administrador: everything except users.</summary>
    public const string Manager = "manager";

    /// <summary>Cajero: sells, charges and registers customers.</summary>
    public const string Cashier = "sales";

    /// <summary>For [Authorize(Roles = ...)]: owner or manager.</summary>
    public const string ManagerOrOwner = Owner + "," + Manager;

    /// <summary>For [Authorize(Roles = ...)]: any staff member, cashier included.</summary>
    public const string Staff = Owner + "," + Manager + "," + Cashier;

    public static readonly IReadOnlyList<string> All = [Owner, Manager, Cashier];

    public static string Label(string? role) => role switch
    {
        Owner => "Dueño",
        Manager => "Administrador",
        Cashier => "Cajero",
        _ => role ?? string.Empty,
    };
}
