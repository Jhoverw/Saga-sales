namespace SagaSales.Identity.Commands;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Registers a store plus its first administrator. Uniqueness and the atomic creation of both
/// records are enforced by <c>sp_tenant_create</c>, not by the handler.
/// </summary>
public record RegisterTenantCommand(
    string Slug,
    string DisplayName,
    string AdminEmail,
    string AdminPassword,
    string? CustomDomain = null) : ICommand<RegisterTenantResponse>;

public sealed class RegisterTenantCommandHandler(
    ITenantRepository repository,
    IPasswordHasher passwordHasher) : ICommandHandler<RegisterTenantCommand, RegisterTenantResponse>
{
    public async Task<Result<RegisterTenantResponse>> Handle(
        RegisterTenantCommand request,
        CancellationToken cancellationToken)
    {
        Guard.AgainstNullOrEmpty(request.Slug, nameof(request.Slug));
        Guard.AgainstNullOrEmpty(request.DisplayName, nameof(request.DisplayName));
        Guard.AgainstNullOrEmpty(request.AdminEmail, nameof(request.AdminEmail));
        Guard.AgainstNullOrEmpty(request.AdminPassword, nameof(request.AdminPassword));

        // The slug reaches a CHECK constraint that only accepts lowercase letters, digits and
        // hyphens, so it is normalised here: the operator types a store name, not a URL fragment.
        var slug = Slugify(request.Slug);

        if (slug.Length < 2)
        {
            throw new ValidationException($"'{request.Slug}' cannot be turned into a valid identifier.");
        }

        var email = request.AdminEmail.Trim().ToLowerInvariant();

        // A duplicate slug surfaces from the procedure as CONFLICT → 409.
        var registration = await repository.CreateAsync(
            slug,
            request.DisplayName.Trim(),
            email,
            passwordHasher.Hash(request.AdminPassword),
            request.CustomDomain,
            cancellationToken);

        return Result<RegisterTenantResponse>.Success(new RegisterTenantResponse
        {
            TenantId = registration.Id,
            AdminEmail = registration.AdminEmail,
            Role = "admin",
        });
    }

    /// <summary>
    /// "Mi Tienda Café" becomes "mi-tienda-cafe": accents are decomposed and dropped, anything that
    /// is not a letter or digit becomes a hyphen, and the result fits the 50 character column.
    /// </summary>
    private static string Slugify(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        var slug = Regex.Replace(builder.ToString().Normalize(NormalizationForm.FormC), "[^a-z0-9]+", "-")
            .Trim('-');

        return slug.Length > 50 ? slug[..50].Trim('-') : slug;
    }
}
