namespace FunEvents.Contracts.Reservations;

public sealed record ReservationResponse(
    Guid Id,
    string EventCode,
    string UserCode,
    int Quantity,
    DateTimeOffset CreatedAtUtc);
