using FunEvents.Api.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FunEvents.Api.Endpoints;

/// <summary>The single place where business errors become HTTP responses (RFC 9457 ProblemDetails).</summary>
public static class ErrorHttpExtensions
{
    public static ProblemHttpResult ToProblem(this Error error)
    {
        var (statusCode, title) = error.Type switch
        {
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Resource not found"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflict with the current state"),
            ErrorType.Unprocessable => (StatusCodes.Status422UnprocessableEntity, "Request cannot be processed"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error"),
        };

        return TypedResults.Problem(
            detail: error.Message,
            statusCode: statusCode,
            title: title,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
