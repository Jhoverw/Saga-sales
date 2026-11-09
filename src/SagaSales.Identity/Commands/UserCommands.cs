namespace SagaSales.Identity.Commands;

/// <summary>Staff users managed by the owner (spec 005).</summary>
public record GetUsersQuery : IQuery<IReadOnlyList<UserDto>>;

public sealed class GetUsersQueryHandler(IUserRepository repository) : IQueryHandler<GetUsersQuery, IReadOnlyList<UserDto>>
{
    public async Task<Result<IReadOnlyList<UserDto>>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
        => Result<IReadOnlyList<UserDto>>.Success(await repository.ListAsync(cancellationToken));
}

public record CreateUserCommand(CreateUserRequest Request) : ICommand<UserDto>;

public sealed class CreateUserCommandHandler(IUserRepository repository, IPasswordHasher passwordHasher)
    : ICommandHandler<CreateUserCommand, UserDto>
{
    public async Task<Result<UserDto>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await repository.CreateAsync(
            request.Request.Email.Trim().ToLowerInvariant(),
            passwordHasher.Hash(request.Request.Password),
            request.Request.Role,
            request.Request.DisplayName.Trim(),
            cancellationToken);

        return Result<UserDto>.Success(user);
    }
}

public record UpdateUserCommand(Guid UserId, UpdateUserRequest Request) : ICommand<UserDto>;

public sealed class UpdateUserCommandHandler(IUserRepository repository) : ICommandHandler<UpdateUserCommand, UserDto>
{
    public async Task<Result<UserDto>> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.UserId, nameof(request.UserId));

        var user = await repository.UpdateAsync(
            request.UserId,
            request.Request.DisplayName.Trim(),
            request.Request.Role,
            request.Request.IsActive,
            cancellationToken);

        return Result<UserDto>.Success(user);
    }
}

public record ResetPasswordCommand(Guid UserId, ResetPasswordRequest Request) : ICommand;

public sealed class ResetPasswordCommandHandler(IUserRepository repository, IPasswordHasher passwordHasher)
    : ICommandHandler<ResetPasswordCommand>
{
    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.UserId, nameof(request.UserId));

        await repository.SetPasswordAsync(request.UserId, passwordHasher.Hash(request.Request.Password), cancellationToken);
        return Result.Success();
    }
}
