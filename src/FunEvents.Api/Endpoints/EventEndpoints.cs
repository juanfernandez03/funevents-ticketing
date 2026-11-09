using FunEvents.Api.UseCases.Events;
using FunEvents.Contracts.Events;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FunEvents.Api.Endpoints;

public static class EventEndpoints
{
    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/events/{eventCode:maxlength(32)}", GetEventAsync)
            .WithName("GetEvent")
            .WithTags("Events")
            .WithSummary("Get an event and its current availability.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<Results<Ok<EventResponse>, ProblemHttpResult>> GetEventAsync(
        string eventCode,
        GetEvent useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(eventCode, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }
}
