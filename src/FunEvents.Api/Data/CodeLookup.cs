using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace FunEvents.Api.Data;

/// <summary>Internal id and public code of an event or a user. Immutable, so the cache can share instances.</summary>
[ImmutableObject(true)]
public sealed record CodeReference(Guid Id, string Code);

/// <summary>
/// Resolves public codes to internal ids. Codes never change once created, so found ids are cached;
/// misses are not, so an event or user created a second later is found. Availability is never cached.
/// </summary>
public sealed class CodeLookup(HybridCache cache, IServiceScopeFactory scopeFactory)
{
    private static readonly HybridCacheEntryOptions CacheOptions = new() { Expiration = TimeSpan.FromMinutes(10) };

    public Task<CodeReference?> FindEventAsync(string eventCode, CancellationToken cancellationToken) =>
        GetCachedAsync($"event-id:{eventCode}", (db, ct) => db.Events
            .AsNoTracking()
            .Where(e => e.Code == eventCode)
            .Select(e => new CodeReference(e.Id, e.Code))
            .SingleOrDefaultAsync(ct), cancellationToken);

    public Task<CodeReference?> FindUserAsync(string userCode, CancellationToken cancellationToken) =>
        GetCachedAsync($"user-id:{userCode}", (db, ct) => db.Users
            .AsNoTracking()
            .Where(u => u.Code == userCode)
            .Select(u => new CodeReference(u.Id, u.Code))
            .SingleOrDefaultAsync(ct), cancellationToken);

    private async Task<CodeReference?> GetCachedAsync(
        string key,
        Func<FunEventsDbContext, CancellationToken, Task<CodeReference?>> query,
        CancellationToken cancellationToken)
    {
        var reference = await cache.GetOrCreateAsync(
            key,
            (scopeFactory, query),
            static async (state, ct) =>
            {
                // HybridCache shares one factory run across concurrent callers, so it must not use the
                // initiating request's DbContext: that request may finish or be cancelled first.
                await using var scope = state.scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<FunEventsDbContext>();
                return await state.query(db, ct);
            },
            CacheOptions,
            cancellationToken: cancellationToken);

        // Never keep a miss.
        if (reference is null)
        {
            await cache.RemoveAsync(key, cancellationToken);
        }

        return reference;
    }
}
