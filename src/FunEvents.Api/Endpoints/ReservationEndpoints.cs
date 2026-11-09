using FunEvents.Api.UseCases.Reservations;
using FunEvents.Contracts;
using FunEvents.Contracts.Reservations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace FunEvents.Api.Endpoints;

public static class ReservationEndpoints
{
    public static IEndpointRouteBuilder MapReservationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/events/{eventCode:maxlength(32)}/reservations", ReserveAsync)
            .WithName("ReserveTickets")
            .WithTags("Reservations")
            .WithSummary("Reserve tickets for an event on behalf of a user.")
            .WithDescription(
                $"Send an `{ApiHeaders.IdempotencyKey}` header to make retries safe: repeating the request with the same key " +
                $"returns the original reservation and the `{ApiHeaders.IdempotentReplayed}: true` header.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

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
        [FromHeader(Name = ApiHeaders.IdempotencyKey), IdempotencyKey] string? idempotencyKey,
        ReserveTickets useCase,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(eventCode, request, idempotencyKey, cancellationToken);
        if (!result.IsSuccess)
        {
            return result.Error.ToProblem();
        }

        var (reservation, isReplay) = result.Value;
        if (isReplay)
        {
            response.Headers[ApiHeaders.IdempotentReplayed] = "true";
        }

        return TypedResults.Created($"/reservations/{reservation.Id}", reservation);
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
