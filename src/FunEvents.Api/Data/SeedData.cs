using FunEvents.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FunEvents.Api.Data;

/// <summary>
/// Known events the console client can use straight away.
/// Runs through EF Core's UseSeeding/UseAsyncSeeding, i.e. after migrations are applied.
/// </summary>
public static class SeedData
{
    public static void Seed(DbContext context, bool _)
    {
        if (!context.Set<EventEntity>().Any())
        {
            context.AddRange(Events());
        }

        context.SaveChanges();
    }

    public static async Task SeedAsync(DbContext context, bool _, CancellationToken cancellationToken)
    {
        if (!await context.Set<EventEntity>().AnyAsync(cancellationToken))
        {
            context.AddRange(Events());
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static EventEntity[] Events() =>
    [
        NewEvent("ROCK-BCN-2027", "Rock Night Barcelona", "FunEvents Arena, Barcelona", new(2027, 3, 20, 20, 0, 0, TimeSpan.Zero), 500),
        NewEvent("HAMLET-MAD-2027", "Hamlet", "Teatro Central, Madrid", new(2027, 2, 12, 19, 30, 0, TimeSpan.Zero), 120),
        // Few tickets on purpose: used to show that concurrent buyers cannot oversell.
        NewEvent("JAZZ-VLC-2027", "Jazz under the Stars", "Open Air Stage, Valencia", new(2027, 6, 5, 21, 0, 0, TimeSpan.Zero), 10),
    ];

    private static EventEntity NewEvent(string code, string name, string venue, DateTimeOffset startsAt, int tickets) => new()
    {
        Id = Guid.CreateVersion7(),
        Code = code,
        Name = name,
        Venue = venue,
        StartsAtUtc = startsAt,
        TotalTickets = tickets,
        AvailableTickets = tickets,
    };
}
