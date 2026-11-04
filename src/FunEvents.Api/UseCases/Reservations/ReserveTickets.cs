using FunEvents.Api.Common;
using FunEvents.Api.Data;
using FunEvents.Api.Data.Entities;
using FunEvents.Api.UseCases.Events;
using FunEvents.Contracts.Reservations;
using Microsoft.EntityFrameworkCore;

namespace FunEvents.Api.UseCases.Reservations;

/// <summary>Command: takes tickets from an event's inventory for a user.</summary>
public sealed partial class ReserveTickets(FunEventsDbContext db, TimeProvider clock, ILogger<ReserveTickets> logger)
{
    public async Task<Result<ReservationResponse>> ExecuteAsync(
        string eventCode,
        ReserveTicketsRequest request,
        CancellationToken cancellationToken)
    {
        var ev = await db.Events.SingleOrDefaultAsync(e => e.Code == eventCode, cancellationToken);
        if (ev is null)
        {
            return EventErrors.NotFound(eventCode);
        }

        var buyer = await db.Users
            .AsNoTracking()
            .Where(u => u.Code == request.UserCode)
            .Select(u => new { u.Id, u.Code })
            .SingleOrDefaultAsync(cancellationToken);
        if (buyer is null)
        {
            return ReservationErrors.UserNotFound(request.UserCode);
        }

        if (ev.AvailableTickets < request.Quantity)
        {
            LogNotEnoughTickets(ev.Code, request.Quantity);
            return ReservationErrors.NotEnoughTickets(ev.Code, request.Quantity);
        }

        ev.AvailableTickets -= request.Quantity;

        // PostgreSQL stores microseconds: truncate now so the response matches what is read back later.
        var utcNow = clock.GetUtcNow();
        var now = utcNow.AddTicks(-(utcNow.Ticks % TimeSpan.TicksPerMicrosecond));
        var reservation = new ReservationEntity
        {
            Id = Guid.CreateVersion7(now),
            EventId = ev.Id,
            UserId = buyer.Id,
            Quantity = request.Quantity,
            CreatedAtUtc = now,
        };
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync(cancellationToken);

        LogReserved(reservation.Id, reservation.Quantity, ev.Code, buyer.Code);
        return new ReservationResponse(reservation.Id, ev.Code, buyer.Code, reservation.Quantity, now);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Reservation {ReservationId}: {Quantity} ticket(s) for {EventCode} reserved by {UserCode}")]
    private partial void LogReserved(Guid reservationId, int quantity, string eventCode, string userCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Rejected: {EventCode} does not have {Quantity} ticket(s) available")]
    private partial void LogNotEnoughTickets(string eventCode, int quantity);
}
