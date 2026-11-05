namespace SagaSales.Identity.Commands;

/// <summary>
/// Authenticates a user and returns a signed access token.
/// The user is looked up through <c>sp_user_find_by_email</c>; the last sign-in is stamped by
/// <c>sp_user_touch_login</c>. Nothing here touches an ORM.
/// </summary>
public record LoginCommand(string Email, string Password) : ICommand<LoginResponse>;

public sealed class LoginCommandHandler(
    ITenantRepository repository,
    IPasswordHasher passwordHasher,
    IJwtTokenService tokenService) : ICommandHandler<LoginCommand, LoginResponse>
{
    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstNullOrEmpty(request.Email, nameof(request.Email));
        Guard.AgainstNullOrEmpty(request.Password, nameof(request.Password));

        var email = request.Email.Trim().ToLowerInvariant();

        var user = await repository.FindUserByEmailAsync(email, cancellationToken);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            // Same failure for unknown user and wrong password: no account enumeration.
            throw new UnauthorizedException("Invalid email or password.");
        }

        await repository.TouchLoginAsync(user.TenantId, user.Id, cancellationToken);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(SagaSalesConstants.Claims.TenantId, user.TenantId.ToString()),

            // Carried in the token so every screen can say which store the user is in.
            new Claim("tenant_name", user.TenantName),
            new Claim("tenant_slug", user.TenantSlug),
        };

        var response = new LoginResponse
        {
            AccessToken = tokenService.GenerateAccessToken(claims),
            RefreshToken = tokenService.GenerateRefreshToken(),
            TokenType = "Bearer",
            ExpiresIn = tokenService.AccessTokenLifetimeSeconds,
            User = new UserInfo
            {
                Id = user.Id,
                Email = user.Email,
                Role = user.Role,
                DisplayName = user.DisplayName,
                TenantId = user.TenantId,
            },
        };

        return Result<LoginResponse>.Success(response);
    }
}
