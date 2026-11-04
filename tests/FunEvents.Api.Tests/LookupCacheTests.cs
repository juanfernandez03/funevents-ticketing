using System.Net;
using System.Net.Http.Json;
using FunEvents.Api.Tests.Infrastructure;
using FunEvents.Contracts.Events;

namespace FunEvents.Api.Tests;

/// <summary>
/// Event and user lookups may be cached (ADR-0007), but a lookup that found nothing must not be remembered:
/// an event or user created a moment later has to be usable straight away.
/// </summary>
public sealed class LookupCacheTests(FunEventsApiFactory api)
{
    private readonly HttpClient _client = api.CreateClient();

    [Fact]
    public async Task Reserve_ForEventCreatedAfterFailedLookup_Returns201()
    {
        var userCode = await api.CreateUserAsync();
        var eventCode = FunEventsApiFactory.NewEventCode();
        var beforeCreation = await _client.ReserveAsync(eventCode, userCode, quantity: 1);

        await api.CreateEventAsync(tickets: 5, eventCode);
        var afterCreation = await _client.ReserveAsync(eventCode, userCode, quantity: 1);

        Assert.Equal(HttpStatusCode.NotFound, beforeCreation.StatusCode);
        Assert.Equal(HttpStatusCode.Created, afterCreation.StatusCode);
    }

    [Fact]
    public async Task Reserve_ForUserCreatedAfterFailedLookup_Returns201()
    {
        var eventCode = await api.CreateEventAsync(tickets: 5);
        var userCode = FunEventsApiFactory.NewUserCode();
        var beforeCreation = await _client.ReserveAsync(eventCode, userCode, quantity: 1);

        await api.CreateUserAsync(userCode);
        var afterCreation = await _client.ReserveAsync(eventCode, userCode, quantity: 1);

        Assert.Equal(HttpStatusCode.NotFound, beforeCreation.StatusCode);
        Assert.Equal(HttpStatusCode.Created, afterCreation.StatusCode);
    }

    [Fact]
    public async Task GetEvent_AfterReservation_IsNeverServedFromCache()
    {
        var eventCode = await api.CreateEventAsync(tickets: 10);
        var userCode = await api.CreateUserAsync();
        await _client.GetAsync($"/events/{eventCode}", TestContext.Current.CancellationToken);

        await _client.ReserveAsync(eventCode, userCode, quantity: 2);
        var ev = await _client.GetFromJsonAsync<EventResponse>(
            $"/events/{eventCode}", TestContext.Current.CancellationToken);

        Assert.Equal(8, ev?.AvailableTickets);
    }
}
