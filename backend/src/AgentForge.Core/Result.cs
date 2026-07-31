namespace AgentForge.Core;

public enum ErrorKind
{
    NotFound,
    Conflict,
    Validation,
    RateLimited,
    DependencyFailure
}

public readonly record struct Error(ErrorKind Kind, string Code, string Message);

public readonly struct Result<T>
{
    private Result(T value)
    {
        Value = value;
        Error = null;
    }

    private Result(Error error)
    {
        Value = default;
        Error = error;
    }

    public T? Value { get; }

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(Error error) => new(error);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure) =>
        Error is { } error ? onFailure(error) : onSuccess(Value!);

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(Error error) => new(error);
}
