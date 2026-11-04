namespace SagaSales.Shared.Contracts;

/// <summary>
/// Marker for a command that produces no value beyond success/failure.
/// </summary>
public interface ICommand : IRequest<Result>
{
}

/// <summary>
/// Marker for a command that produces a value.
/// </summary>
public interface ICommand<TResponse> : IRequest<Result<TResponse>>
{
}

/// <summary>
/// Handler for a command with no return value.
/// </summary>
public interface ICommandHandler<TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand
{
}

/// <summary>
/// Handler for a command that returns a value.
/// </summary>
public interface ICommandHandler<TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>
{
}
