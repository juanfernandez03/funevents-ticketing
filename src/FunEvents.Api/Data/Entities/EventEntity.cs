namespace FunEvents.Api.Data.Entities;

/// <summary>An event that sells tickets. In the prototype there is a single inventory counter per event.</summary>
public sealed class EventEntity
{
    public Guid Id { get; set; }

    /// <summary>Public, human-friendly identifier used by every channel (e.g. <c>ROCK-BCN-2027</c>).</summary>
    public required string Code { get; set; }

    public required string Name { get; set; }

    public required string Venue { get; set; }

    public DateTimeOffset StartsAtUtc { get; set; }

    public int TotalTickets { get; set; }

    /// <summary>Tickets not yet reserved. Only ever changed through an atomic conditional update.</summary>
    public int AvailableTickets { get; set; }
}
