using FunEvents.Api.Common;
using FunEvents.Api.Data;
using FunEvents.Contracts.Events;
using Microsoft.EntityFrameworkCore;

namespace FunEvents.Api.UseCases.Events;

/// <summary>Query: an event with its current availability. Reads only, never tracks entities.</summary>
public sealed class GetEvent(FunEventsDbContext db)
{
    public async Task<Result<EventResponse>> ExecuteAsync(string eventCode, CancellationToken cancellationToken)
    {
        var response = await db.Events
            .AsNoTracking()
            .Where(e => e.Code == eventCode)
            .Select(e => new EventResponse(e.Code, e.Name, e.Venue, e.StartsAtUtc, e.TotalTickets, e.AvailableTickets))
            .SingleOrDefaultAsync(cancellationToken);

        return response is null ? EventErrors.NotFound(eventCode) : response;
    }
}
