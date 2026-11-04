namespace FunEvents.Contracts.Events;

public sealed record EventResponse(
    string Code,
    string Name,
    string Venue,
    DateTimeOffset StartsAtUtc,
    int TotalTickets,
    int AvailableTickets);
