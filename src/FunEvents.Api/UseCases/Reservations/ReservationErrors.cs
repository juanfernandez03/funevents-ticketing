using FunEvents.Api.Common;

namespace FunEvents.Api.UseCases.Reservations;

public static class ReservationErrors
{
    public static Error UserNotFound(string userCode) =>
        Error.NotFound("user_not_found", $"User '{userCode}' does not exist.");

    public static Error NotEnoughTickets(string eventCode, int requested) =>
        Error.Conflict("not_enough_tickets", $"Event '{eventCode}' does not have {requested} ticket(s) available.");

    public static Error EventStarted(string eventCode) =>
        Error.Conflict("event_started", $"Event '{eventCode}' has already started; its tickets can no longer be reserved.");

    public static Error NotFound(Guid reservationId) =>
        Error.NotFound("reservation_not_found", $"Reservation '{reservationId}' does not exist.");

    public static Error IdempotencyKeyReused(string idempotencyKey) =>
        Error.Unprocessable(
            "idempotency_key_reused",
            $"Idempotency key '{idempotencyKey}' was already used for a different reservation request.");
}
