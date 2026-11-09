using System.Net;
using FunEvents.Api.Tests.Infrastructure;

namespace FunEvents.Api.Tests;

/// <summary>The core guarantee of the system: concurrent buyers can never take more tickets than exist.</summary>
public sealed class OversellingTests(FunEventsApiFactory api)
{
    private readonly HttpClient _client = api.CreateClient();

    [Fact]
    public async Task Reserve_FiftyConcurrentBuyersForTenTickets_ExactlyTenSucceed()
    {
        const int tickets = 10;
        const int buyers = 50;
        var eventCode = await api.CreateEventAsync(tickets);
        var userCode = await api.CreateUserAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, buyers)
            .Select(_ => _client.ReserveAsync(eventCode, userCode, quantity: 1)));

        Assert.Equal(tickets, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(buyers - tickets, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(0, await api.GetAvailableTicketsAsync(eventCode));
        Assert.Equal(tickets, await api.CountReservedTicketsAsync(eventCode));
    }

    [Fact]
    public async Task Reserve_ConcurrentBuyersOfThreeForTenTickets_NeverExceedTheTotal()
    {
        // 10 tickets, buyers of 3: only three can succeed and one ticket must remain.
        var eventCode = await api.CreateEventAsync(tickets: 10);
        var userCode = await api.CreateUserAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 30)
            .Select(_ => _client.ReserveAsync(eventCode, userCode, quantity: 3)));

        Assert.Equal(3, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(27, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, await api.GetAvailableTicketsAsync(eventCode));
        Assert.Equal(9, await api.CountReservedTicketsAsync(eventCode));
    }
}
