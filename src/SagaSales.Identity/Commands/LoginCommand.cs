namespace SagaSales.Identity.Commands;

/// <summary>
/// Authenticates a user and returns a signed access token. The previous version built
/// the tokens and then discarded them, so login never returned a usable token.
/// </summary>
public record LoginCommand(string Email, string Password) : ICommand<LoginResponse>;

public sealed class LoginCommandHandler(
    TenantDbContext dbContext,
    IPasswordHasher passwordHasher,
    IJwtTokenService tokenService) : ICommandHandler<LoginCommand, LoginResponse>
{
    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstNullOrEmpty(request.Email, nameof(request.Email));
        Guard.AgainstNullOrEmpty(request.Password, nameof(request.Password));

        var email = request.Email.Trim().ToLowerInvariant();

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive, cancellationToken);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            // Same failure for unknown user and wrong password: no account enumeration.
            throw new UnauthorizedException("Invalid email or password.");
        }

        await dbContext.Users
            .Where(u => u.Id == user.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLoginAt, DateTime.UtcNow), cancellationToken);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(SagaSalesConstants.Claims.TenantId, user.TenantId.ToString()),
            new Claim(SagaSalesConstants.Claims.Email, user.Email),
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
