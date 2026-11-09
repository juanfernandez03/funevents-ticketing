using FunEvents.Api.Common;
using FunEvents.Api.Data;
using FunEvents.Api.Tests.Infrastructure;
using FunEvents.Api.UseCases.Reservations;
using FunEvents.Contracts.Reservations;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace FunEvents.Api.Tests;

public sealed class LookupScopeTests(FunEventsApiFactory api)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lookup_AfterInitiatingScopeIsDisposed_ReturnsNotFound(bool lookupUser)
    {
        var eventCode = lookupUser ? await api.CreateEventAsync(tickets: 5) : FunEventsApiFactory.NewEventCode();
        var userCode = FunEventsApiFactory.NewUserCode();
        var cache = new DelayedLookupCache(lookupUser ? "user-id:" : "event-id:");
        var scope = api.Services.CreateAsyncScope();
        Task<Result<ReservationOutcome>> pending;
        try
        {
            var codes = ActivatorUtilities.CreateInstance<CodeLookup>(scope.ServiceProvider, cache);
            var command = ActivatorUtilities.CreateInstance<ReserveTickets>(scope.ServiceProvider, codes);
            pending = command.ExecuteAsync(eventCode,
                new ReserveTicketsRequest(userCode, 1), null,
                TestContext.Current.CancellationToken);
            await cache.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            await scope.DisposeAsync();
            cache.Release.TrySetResult();
        }

        var result = await pending;

        Assert.False(result.IsSuccess);
        Assert.Equal(Error.NotFound(lookupUser ? "user_not_found" : "event_not_found",
            lookupUser ? $"User '{userCode}' does not exist." : $"Event '{eventCode}' does not exist."), result.Error);
        Assert.Equal(0, await api.CountReservationsAsync(eventCode));
        if (lookupUser)
        {
            Assert.Equal(5, await api.GetAvailableTicketsAsync(eventCode));
        }
    }

    // Hold the factory before it queries, modelling shared cache work that outlives its initiating scope.
    private sealed class DelayedLookupCache(string prefix) : HybridCache
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<T> GetOrCreateAsync<TState, T>(string key, TState state,
            Func<TState, CancellationToken, ValueTask<T>> factory, HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null, CancellationToken cancellationToken = default)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                Started.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }

            return await factory(state, cancellationToken);
        }

        public override ValueTask SetAsync<T>(string key, T value, HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
