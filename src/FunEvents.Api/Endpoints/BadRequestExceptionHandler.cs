using Microsoft.AspNetCore.Diagnostics;

namespace FunEvents.Api.Endpoints;

/// <summary>
/// Requests the framework cannot bind (missing or malformed body, wrong media type) get the same
/// ProblemDetails shape as a validation error, so clients handle a single 400 format.
/// </summary>
internal sealed class BadRequestExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public const string BodyErrorKey = "body";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException badRequest)
        {
            return false;
        }

        // The exception message names internal parameter types, so the client gets a fixed message instead.
        var problem = badRequest.StatusCode == StatusCodes.Status400BadRequest
            ? new HttpValidationProblemDetails(new Dictionary<string, string[]>
            {
                [BodyErrorKey] = ["The request body is missing or is not valid JSON for this operation."],
            })
            : new Microsoft.AspNetCore.Mvc.ProblemDetails();
        problem.Status = badRequest.StatusCode;
        httpContext.Response.StatusCode = badRequest.StatusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
