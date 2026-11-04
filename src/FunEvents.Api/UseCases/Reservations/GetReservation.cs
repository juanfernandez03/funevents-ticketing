using FunEvents.Api.Common;
using FunEvents.Api.Data;
using FunEvents.Contracts.Reservations;
using Microsoft.EntityFrameworkCore;

namespace FunEvents.Api.UseCases.Reservations;

/// <summary>Query: a reservation by id. Reads only, never tracks entities.</summary>
public sealed class GetReservation(FunEventsDbContext db)
{
    public async Task<Result<ReservationResponse>> ExecuteAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        var response = await db.Reservations
            .AsNoTracking()
            .Where(r => r.Id == reservationId)
            .Select(r => new ReservationResponse(r.Id, r.Event.Code, r.User.Code, r.Quantity, r.CreatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

        return response is null ? ReservationErrors.NotFound(reservationId) : response;
    }
}
