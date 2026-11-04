using FunEvents.Api.Common;
using FunEvents.Api.Data;
using FunEvents.Api.Data.Entities;
using FunEvents.Api.UseCases.Events;
using FunEvents.Contracts.Reservations;
using Microsoft.EntityFrameworkCore;

namespace FunEvents.Api.UseCases.Reservations;

/// <summary>Command: takes tickets from an event's inventory for a user, without ever overselling.</summary>
public sealed partial class ReserveTickets(FunEventsDbContext db, TimeProvider clock, ILogger<ReserveTickets> logger)
{
    public async Task<Result<ReservationResponse>> ExecuteAsync(
        string eventCode,
        ReserveTicketsRequest request,
        CancellationToken cancellationToken)
    {
        var targetEvent = await db.Events
            .AsNoTracking()
            .Where(e => e.Code == eventCode)
            .Select(e => new CodeReference(e.Id, e.Code))
            .SingleOrDefaultAsync(cancellationToken);
        if (targetEvent is null)
        {
            return EventErrors.NotFound(eventCode);
        }

        var buyer = await db.Users
            .AsNoTracking()
            .Where(u => u.Code == request.UserCode)
            .Select(u => new CodeReference(u.Id, u.Code))
            .SingleOrDefaultAsync(cancellationToken);
        if (buyer is null)
        {
            return ReservationErrors.UserNotFound(request.UserCode);
        }

        // The Aspire integration enables EF Core connection retries, so the explicit transaction
        // has to run inside the execution strategy: on a transient failure the whole unit is retried.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            ct => ReserveInTransactionAsync(targetEvent, buyer, request.Quantity, ct),
            cancellationToken);
    }

    private async Task<Result<ReservationResponse>> ReserveInTransactionAsync(
        CodeReference targetEvent,
        CodeReference buyer,
        int quantity,
        CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Check-and-decrement in a single statement. PostgreSQL locks the row and re-evaluates the
        // WHERE clause after the lock, so concurrent buyers can never push the counter below zero.
        var updatedRows = await db.Events
            .Where(e => e.Id == targetEvent.Id && e.AvailableTickets >= quantity)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(e => e.AvailableTickets, e => e.AvailableTickets - quantity),
                cancellationToken);

        if (updatedRows == 0)
        {
            LogNotEnoughTickets(targetEvent.Code, quantity);
            return ReservationErrors.NotEnoughTickets(targetEvent.Code, quantity);
        }

        // PostgreSQL stores microseconds: truncate now so the response matches what is read back later.
        var utcNow = clock.GetUtcNow();
        var now = utcNow.AddTicks(-(utcNow.Ticks % TimeSpan.TicksPerMicrosecond));
        var reservation = new ReservationEntity
        {
            Id = Guid.CreateVersion7(now),
            EventId = targetEvent.Id,
            UserId = buyer.Id,
            Quantity = quantity,
            CreatedAtUtc = now,
        };
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        LogReserved(reservation.Id, quantity, targetEvent.Code, buyer.Code);
        return new ReservationResponse(reservation.Id, targetEvent.Code, buyer.Code, quantity, now);
    }

    /// <summary>Internal id and public code of an event or a user.</summary>
    private sealed record CodeReference(Guid Id, string Code);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reservation {ReservationId}: {Quantity} ticket(s) for {EventCode} reserved by {UserCode}")]
    private partial void LogReserved(Guid reservationId, int quantity, string eventCode, string userCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Rejected: {EventCode} does not have {Quantity} ticket(s) available")]
    private partial void LogNotEnoughTickets(string eventCode, int quantity);
}
