using System.ComponentModel;
using FunEvents.Api.Common;
using FunEvents.Api.Data;
using FunEvents.Api.Data.Entities;
using FunEvents.Api.UseCases.Events;
using FunEvents.Contracts.Reservations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Npgsql;

namespace FunEvents.Api.UseCases.Reservations;

/// <param name="Reservation">The reservation, new or the one created by an earlier request with the same key.</param>
/// <param name="IsReplay"><c>true</c> when the idempotency key had already been used with the same payload.</param>
public sealed record ReservationOutcome(ReservationResponse Reservation, bool IsReplay);

/// <summary>Command: takes tickets from an event's inventory for a user, without ever overselling.</summary>
public sealed partial class ReserveTickets(
    FunEventsDbContext db,
    HybridCache cache,
    TimeProvider clock,
    ILogger<ReserveTickets> logger)
{
    private const string IdempotencyKeyIndex = "ix_reservations_idempotency_key";

    // Codes never change once created, so their ids can be cached. Availability is never cached.
    private static readonly HybridCacheEntryOptions LookupCacheOptions = new() { Expiration = TimeSpan.FromMinutes(10) };

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

        var targetEvent = await FindEventAsync(eventCode, cancellationToken);
        if (targetEvent is null)
        {
            return EventErrors.NotFound(eventCode);
        }

        var buyer = await FindUserAsync(request.UserCode, cancellationToken);
        if (buyer is null)
        {
            return ReservationErrors.UserNotFound(request.UserCode);
        }

        try
        {
            // The Aspire integration enables EF Core connection retries, so the explicit transaction
            // has to run inside the execution strategy: on a transient failure the whole unit is retried.
            var strategy = db.Database.CreateExecutionStrategy();
            var result = await strategy.ExecuteAsync(
                ct => ReserveInTransactionAsync(targetEvent, buyer, request.Quantity, idempotencyKey, ct),
                cancellationToken);

            // Sold out may mean a concurrent request with the same key took the last tickets:
            // it waited on the row lock, then saw the counter its own winner had decremented.
            if (!result.IsSuccess && idempotencyKey is not null)
            {
                return await ReplayConcurrentWinnerAsync(idempotencyKey, eventCode, request, cancellationToken) ?? result;
            }

            return result;
        }
        catch (DbUpdateException exception) when (idempotencyKey is not null && IsIdempotencyKeyConflict(exception))
        {
            // A concurrent request with the same key committed first. Our transaction was rolled back,
            // inventory decrement included, so answering with the winner's result is all that is left.
            return await ReplayConcurrentWinnerAsync(idempotencyKey, eventCode, request, cancellationToken)
                ?? throw new InvalidOperationException($"Reservation with idempotency key '{idempotencyKey}' vanished.", exception);
        }
    }

    /// <summary>Replays the reservation a concurrent request committed with the same key, if there is one.</summary>
    private async Task<Result<ReservationOutcome>?> ReplayConcurrentWinnerAsync(
        string idempotencyKey,
        string eventCode,
        ReserveTicketsRequest request,
        CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var winner = await FindByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        return winner is null ? null : Replay(winner, eventCode, request, idempotencyKey);
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

    private Task<CodeReference?> FindEventAsync(string eventCode, CancellationToken cancellationToken) =>
        GetCachedLookupAsync($"event-id:{eventCode}", ct => db.Events
            .AsNoTracking()
            .Where(e => e.Code == eventCode)
            .Select(e => new CodeReference(e.Id, e.Code))
            .SingleOrDefaultAsync(ct), cancellationToken);

    private Task<CodeReference?> FindUserAsync(string userCode, CancellationToken cancellationToken) =>
        GetCachedLookupAsync($"user-id:{userCode}", ct => db.Users
            .AsNoTracking()
            .Where(u => u.Code == userCode)
            .Select(u => new CodeReference(u.Id, u.Code))
            .SingleOrDefaultAsync(ct), cancellationToken);

    private async Task<CodeReference?> GetCachedLookupAsync(
        string key,
        Func<CancellationToken, Task<CodeReference?>> lookup,
        CancellationToken cancellationToken)
    {
        var reference = await cache.GetOrCreateAsync(
            key,
            lookup,
            static (lookup, ct) => new ValueTask<CodeReference?>(lookup(ct)),
            LookupCacheOptions,
            cancellationToken: cancellationToken);

        // Never keep a miss: the event or user may be created a second later.
        if (reference is null)
        {
            await cache.RemoveAsync(key, cancellationToken);
        }

        return reference;
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

    /// <summary>Internal id and public code of an event or a user. Immutable, so the cache can share instances.</summary>
    [ImmutableObject(true)]
    public sealed record CodeReference(Guid Id, string Code);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reservation {ReservationId}: {Quantity} ticket(s) for {EventCode} reserved by {UserCode}")]
    private partial void LogReserved(Guid reservationId, int quantity, string eventCode, string userCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Rejected: {EventCode} does not have {Quantity} ticket(s) available")]
    private partial void LogNotEnoughTickets(string eventCode, int quantity);

    [LoggerMessage(Level = LogLevel.Information, Message = "Replayed reservation {ReservationId} for idempotency key {IdempotencyKey}")]
    private partial void LogReplayed(Guid reservationId, string idempotencyKey);
}
