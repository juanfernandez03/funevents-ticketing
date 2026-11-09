namespace FunEvents.Api.Data.Entities;

/// <summary>Tickets taken from an event's inventory for a user.</summary>
public sealed class ReservationEntity
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public EventEntity Event { get; set; } = null!;

    public Guid UserId { get; set; }

    public UserEntity User { get; set; } = null!;

    public int Quantity { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Client-supplied key that makes retries safe. Unique when present.</summary>
    public string? IdempotencyKey { get; set; }
}
