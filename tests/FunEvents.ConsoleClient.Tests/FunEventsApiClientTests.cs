using System.Net;
using System.Text.Json;
using FunEvents.ConsoleClient.Tests.Infrastructure;
using FunEvents.Contracts.Reservations;

namespace FunEvents.ConsoleClient.Tests;

public sealed class FunEventsApiClientTests
{
    [Fact]
    public async Task ReserveAsync_PostsTheRequestWithItsIdempotencyKey()
    {
        var api = new StubApiHandler(_ => ApiResponses.Reservation(quantity: 2));

        var result = await api.CreateClient().ReserveAsync(
            "ROCK-BCN-2027", new ReserveTicketsRequest("USR-001", 2), "key-1", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        Assert.False(result.Replayed);

        var request = Assert.Single(api.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/events/ROCK-BCN-2027/reservations", request.Path);
        Assert.Equal("key-1", request.IdempotencyKey);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("USR-001", body.RootElement.GetProperty("userCode").GetString());
        Assert.Equal(2, body.RootElement.GetProperty("quantity").GetInt32());
    }

    [Fact]
    public async Task ReserveAsync_WhenTheApiReplays_ReportsTheReplay()
    {
        var api = new StubApiHandler(_ => ApiResponses.Reservation(replayed: true));

        var result = await api.CreateClient().ReserveAsync(
            "ROCK-BCN-2027", new ReserveTicketsRequest("USR-001", 1), "key-1", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(result.Replayed);
    }

    [Fact]
    public async Task ReserveAsync_WhenRejected_ReadsTheProblem()
    {
        var api = new StubApiHandler(_ => ApiResponses.Problem(
            HttpStatusCode.Conflict, "Conflict with the current state", "not_enough_tickets",
            "Event 'JAZZ-VLC-2027' does not have 2 ticket(s) available."));

        var result = await api.CreateClient().ReserveAsync(
            "JAZZ-VLC-2027", new ReserveTicketsRequest("USR-001", 2), "key-1", TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        Assert.Equal("not_enough_tickets", result.Problem?.Code);
        Assert.Equal("Event 'JAZZ-VLC-2027' does not have 2 ticket(s) available.", result.Problem?.Detail);
    }

    [Fact]
    public async Task GetEventAsync_EscapesTheEventCode()
    {
        var api = new StubApiHandler(_ => ApiResponses.Problem(HttpStatusCode.NotFound, "Resource not found", "event_not_found"));

        var result = await api.CreateClient().GetEventAsync("A B/C", TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("event_not_found", result.Problem?.Code);
        Assert.Equal("/events/A%20B%2FC", Assert.Single(api.Requests).Path);
    }
}
