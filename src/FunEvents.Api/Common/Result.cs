using System.Diagnostics.CodeAnalysis;

namespace FunEvents.Api.Common;

public enum ErrorType
{
    NotFound,
    Conflict,
    Unprocessable,
}

/// <summary>An expected business failure. Unexpected failures stay exceptions.</summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Unprocessable(string code, string message) => new(code, message, ErrorType.Unprocessable);
}

/// <summary>Outcome of a use case: either a value or an <see cref="Common.Error"/>.</summary>
public sealed class Result<T>
{
    private Result(T value) => Value = value;

    private Result(Error error) => Error = error;

    public T? Value { get; }

    public Error? Error { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
}
