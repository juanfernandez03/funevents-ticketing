using System.Net;
using System.Net.Http.Json;
using FunEvents.Api.Tests.Infrastructure;
using FunEvents.Contracts.Events;

namespace FunEvents.Api.Tests;

public sealed class EventTests(FunEventsApiFactory api)
{
    private readonly HttpClient _client = api.CreateClient();

    [Fact]
    public async Task GetEvent_WithSeededCode_ReturnsEventWithAvailability()
    {
        var response = await _client.GetAsync("/events/ROCK-BCN-2027", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ev = await response.Content.ReadFromJsonAsync<EventResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(ev);
        Assert.Equal("ROCK-BCN-2027", ev.Code);
        Assert.Equal("Rock Night Barcelona", ev.Name);
        Assert.Equal(500, ev.TotalTickets);
        Assert.InRange(ev.AvailableTickets, 0, ev.TotalTickets);
    }

    [Fact]
    public async Task GetEvent_WithUnknownCode_Returns404EventNotFound()
    {
        var response = await _client.GetAsync("/events/NO-SUCH-EVENT", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("event_not_found", await response.ReadProblemCodeAsync());
    }
}
