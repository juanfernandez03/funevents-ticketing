using FunEvents.Api.Common;
using FunEvents.Api.Data;
using FunEvents.Api.Data.Entities;
using FunEvents.Api.UseCases.Events;
using FunEvents.Contracts.Reservations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FunEvents.Api.UseCases.Reservations;

/// <param name="Reservation">The reservation, new or the one created by an earlier request with the same key.</param>
/// <param name="IsReplay"><c>true</c> when the idempotency key had already been used with the same payload.</param>
public sealed record ReservationOutcome(ReservationResponse Reservation, bool IsReplay);

/// <summary>Command: takes tickets from an event's inventory for a user, without ever overselling.</summary>
public sealed partial class ReserveTickets(FunEventsDbContext db, TimeProvider clock, ILogger<ReserveTickets> logger)
{
    private const string IdempotencyKeyIndex = "ix_reservations_idempotency_key";

    public async Task<Result<ReservationOutcome>> ExecuteAsync(
        string eventCode,
        ReserveTicketsRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (idempotencyKey is not null)
        {
            var previous = await FindByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
            if (previous is not null)
            {
                return Replay(previous, eventCode, request, idempotencyKey);
            }
        }

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

        try
        {
            // The Aspire integration enables EF Core connection retries, so the explicit transaction
            // has to run inside the execution strategy: on a transient failure the whole unit is retried.
            var strategy = db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(
                ct => ReserveInTransactionAsync(targetEvent, buyer, request.Quantity, idempotencyKey, ct),
                cancellationToken);
        }
        catch (DbUpdateException exception) when (idempotencyKey is not null && IsIdempotencyKeyConflict(exception))
        {
            // A concurrent request with the same key committed first. Our transaction was rolled back,
            // inventory decrement included, so answering with the winner's result is all that is left.
            db.ChangeTracker.Clear();
            var winner = await FindByIdempotencyKeyAsync(idempotencyKey, cancellationToken)
                ?? throw new InvalidOperationException($"Reservation with idempotency key '{idempotencyKey}' vanished.", exception);
            return Replay(winner, eventCode, request, idempotencyKey);
        }
    }

    private async Task<Result<ReservationOutcome>> ReserveInTransactionAsync(
        CodeReference targetEvent,
        CodeReference buyer,
        int quantity,
        string? idempotencyKey,
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
            IdempotencyKey = idempotencyKey,
        };
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        LogReserved(reservation.Id, quantity, targetEvent.Code, buyer.Code);
        var response = new ReservationResponse(reservation.Id, targetEvent.Code, buyer.Code, quantity, now);
        return new ReservationOutcome(response, IsReplay: false);
    }

    private Task<ReservationResponse?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        db.Reservations
            .AsNoTracking()
            .Where(r => r.IdempotencyKey == idempotencyKey)
            .Select(r => new ReservationResponse(r.Id, r.Event.Code, r.User.Code, r.Quantity, r.CreatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

    private Result<ReservationOutcome> Replay(
        ReservationResponse previous,
        string eventCode,
        ReserveTicketsRequest request,
        string idempotencyKey)
    {
        var isSamePayload = previous.EventCode == eventCode
            && previous.UserCode == request.UserCode
            && previous.Quantity == request.Quantity;

        if (!isSamePayload)
        {
            return ReservationErrors.IdempotencyKeyReused(idempotencyKey);
        }

        LogReplayed(previous.Id, idempotencyKey);
        return new ReservationOutcome(previous, IsReplay: true);
    }

    private static bool IsIdempotencyKeyConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: IdempotencyKeyIndex,
        };

    /// <summary>Internal id and public code of an event or a user.</summary>
    private sealed record CodeReference(Guid Id, string Code);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reservation {ReservationId}: {Quantity} ticket(s) for {EventCode} reserved by {UserCode}")]
    private partial void LogReserved(Guid reservationId, int quantity, string eventCode, string userCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Rejected: {EventCode} does not have {Quantity} ticket(s) available")]
    private partial void LogNotEnoughTickets(string eventCode, int quantity);

    [LoggerMessage(Level = LogLevel.Information, Message = "Replayed reservation {ReservationId} for idempotency key {IdempotencyKey}")]
    private partial void LogReplayed(Guid reservationId, string idempotencyKey);
}
