using FunEvents.Api.UseCases.Reservations;
using FunEvents.Contracts.Reservations;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FunEvents.Api.Endpoints;

public static class ReservationEndpoints
{
    public static IEndpointRouteBuilder MapReservationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/events/{eventCode:maxlength(32)}/reservations", ReserveAsync)
            .WithName("ReserveTickets")
            .WithTags("Reservations")
            .WithSummary("Reserve tickets for an event on behalf of a user.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("/reservations/{reservationId:guid}", GetReservationAsync)
            .WithName("GetReservation")
            .WithTags("Reservations")
            .WithSummary("Get a reservation.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    // Endpoints only bind input and translate the result: the rules live in the use cases.
    private static async Task<Results<Created<ReservationResponse>, ProblemHttpResult>> ReserveAsync(
        string eventCode,
        ReserveTicketsRequest request,
        ReserveTickets useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(eventCode, request, cancellationToken);
        return result.IsSuccess
            ? TypedResults.Created($"/reservations/{result.Value.Id}", result.Value)
            : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<ReservationResponse>, ProblemHttpResult>> GetReservationAsync(
        Guid reservationId,
        GetReservation useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(reservationId, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }
}
