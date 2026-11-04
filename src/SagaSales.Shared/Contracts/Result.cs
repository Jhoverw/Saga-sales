namespace SagaSales.Shared.Contracts;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }
    public IReadOnlyList<Error> Errors { get; }

    protected Result(bool isSuccess, Error error, IReadOnlyList<Error>? errors = null)
    {
        IsSuccess = isSuccess;
        Error = error;
        Errors = errors ?? (error != Error.None ? [error] : []);
    }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result Failure(IReadOnlyList<Error> errors) => new(false, Error.None, errors);

    public static implicit operator Result(Error error) => Failure(error);
}

public class Result<T> : Result
{
    public T? Value { get; }

    private Result(T value) : base(true, Error.None)
    {
        Value = value;
    }

    private Result(Error error) : base(false, error)
    {
        Value = default;
    }

    private Result(IReadOnlyList<Error> errors) : base(false, Error.None, errors)
    {
        Value = default;
    }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);
    public static Result<T> Failure(IReadOnlyList<Error> errors) => new(errors);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
}

public record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NullValue = new("Error.NullValue", "A null value was provided");
}