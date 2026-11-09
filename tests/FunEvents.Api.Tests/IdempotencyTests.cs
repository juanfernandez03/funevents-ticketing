using System.Net;
using FunEvents.Api.Tests.Infrastructure;
using FunEvents.Contracts;

namespace FunEvents.Api.Tests;

public sealed class IdempotencyTests(FunEventsApiFactory api)
{
    private readonly HttpClient _client = api.CreateClient();

    [Fact]
    public async Task Reserve_WithSameKeyAndPayload_ReplaysOriginalReservation()
    {
        var eventCode = await api.CreateEventAsync(tickets: 10);
        var userCode = await api.CreateUserAsync();
        var key = Guid.NewGuid().ToString();

        var first = await _client.ReserveAsync(eventCode, userCode, quantity: 2, key);
        var retry = await _client.ReserveAsync(eventCode, userCode, quantity: 2, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.False(first.Headers.Contains(ApiHeaders.IdempotentReplayed));
        Assert.Equal("true", Assert.Single(retry.Headers.GetValues(ApiHeaders.IdempotentReplayed)));
        Assert.Equal(await first.ReadReservationAsync(), await retry.ReadReservationAsync());
        Assert.Equal(8, await api.GetAvailableTicketsAsync(eventCode));
    }

    [Fact]
    public async Task Reserve_WithSameKeyAndDifferentPayload_Returns422IdempotencyKeyReused()
    {
        var eventCode = await api.CreateEventAsync(tickets: 10);
        var userCode = await api.CreateUserAsync();
        var key = Guid.NewGuid().ToString();
        await _client.ReserveAsync(eventCode, userCode, quantity: 2, key);

        var response = await _client.ReserveAsync(eventCode, userCode, quantity: 5, key);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("idempotency_key_reused", await response.ReadProblemCodeAsync());
        Assert.Equal(8, await api.GetAvailableTicketsAsync(eventCode));
    }

    [Fact]
    public async Task Reserve_WithoutKey_CreatesNewReservationEveryTime()
    {
        var eventCode = await api.CreateEventAsync(tickets: 10);
        var userCode = await api.CreateUserAsync();

        await _client.ReserveAsync(eventCode, userCode, quantity: 1);
        await _client.ReserveAsync(eventCode, userCode, quantity: 1);

        Assert.Equal(2, await api.CountReservationsAsync(eventCode));
        Assert.Equal(8, await api.GetAvailableTicketsAsync(eventCode));
    }

    [Fact]
    public async Task Reserve_WithKeyLongerThan64Characters_Returns400AndKeepsInventory()
    {
        var eventCode = await api.CreateEventAsync(tickets: 10);
        var userCode = await api.CreateUserAsync();

        var response = await _client.ReserveAsync(eventCode, userCode, quantity: 1, new string('k', 65));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.ReadValidationErrorsAsync()).TryGetProperty(KeyErrorField, out _));
        Assert.Equal(10, await api.GetAvailableTicketsAsync(eventCode));
    }

    // A key that is sent must be usable: a blank or repeated header is rejected instead of
    // silently becoming "no key", which would leave the client's retries unprotected.
    // HttpClient does not send an empty header value, so whitespace covers the blank case.
    [Theory]
    [InlineData("   ")]
    [InlineData("two words")]
    [InlineData("a1", "b2")]
    public async Task Reserve_WithBlankRepeatedOrSpacedKey_Returns400AndKeepsInventory(params string[] keyValues)
    {
        var eventCode = await api.CreateEventAsync(tickets: 10);
        var userCode = await api.CreateUserAsync();

        var response = await _client.ReserveWithRawKeyAsync(eventCode, userCode, quantity: 1, keyValues);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.ReadValidationErrorsAsync()).TryGetProperty(KeyErrorField, out _));
        Assert.Equal(10, await api.GetAvailableTicketsAsync(eventCode));
        Assert.Equal(0, await api.CountReservationsAsync(eventCode));
    }

    [Fact]
    public async Task Reserve_WithKeyOf64VisibleCharacters_Returns201()
    {
        var eventCode = await api.CreateEventAsync(tickets: 10);
        var userCode = await api.CreateUserAsync();

        var response = await _client.ReserveAsync(eventCode, userCode, quantity: 1, "~!" + new string('k', 62));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // Validation errors are keyed by the request member, like UserCode and Quantity.
    private const string KeyErrorField = "idempotencyKey";

    [Fact]
    public async Task Reserve_TwentyConcurrentRequestsWithSameKey_CreateSingleReservation()
    {
        // Simulates a double click or a client retry racing the original request.
        var eventCode = await api.CreateEventAsync(tickets: 10);
        var userCode = await api.CreateUserAsync();
        var key = Guid.NewGuid().ToString();

        var responses = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => _client.ReserveAsync(eventCode, userCode, quantity: 1, key)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var ids = await Task.WhenAll(responses.Select(r => r.ReadReservationAsync()));
        Assert.Single(ids.Select(r => r.Id).Distinct());
        Assert.Equal(1, await api.CountReservationsAsync(eventCode));
        Assert.Equal(9, await api.GetAvailableTicketsAsync(eventCode));
    }

    [Fact]
    public async Task Reserve_ConcurrentRequestsWithSameKeyForTheLastTicket_AllReplayTheWinner()
    {
        // The losers wait on the inventory row and then find it sold out, by their own winner.
        // A retry must never be told "sold out" for a purchase that succeeded.
        var eventCode = await api.CreateEventAsync(tickets: 1);
        var userCode = await api.CreateUserAsync();
        var key = Guid.NewGuid().ToString();

        var responses = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => _client.ReserveAsync(eventCode, userCode, quantity: 1, key)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var ids = await Task.WhenAll(responses.Select(r => r.ReadReservationAsync()));
        Assert.Single(ids.Select(r => r.Id).Distinct());
        Assert.Equal(1, await api.CountReservationsAsync(eventCode));
        Assert.Equal(0, await api.GetAvailableTicketsAsync(eventCode));
    }

    [Fact]
    public async Task Reserve_ConcurrentRequestsWithDifferentKeysForTheLastTicket_OnlyOneWinsTheRestGet409()
    {
        // Distinct keys are distinct purchases: replaying must never hand one buyer another's reservation.
        var eventCode = await api.CreateEventAsync(tickets: 1);
        var userCode = await api.CreateUserAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => _client.ReserveAsync(eventCode, userCode, quantity: 1, Guid.NewGuid().ToString())));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        var rejected = responses.Where(r => r.StatusCode != HttpStatusCode.Created).ToArray();
        Assert.All(rejected, r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.All(await Task.WhenAll(rejected.Select(r => r.ReadProblemCodeAsync())),
            code => Assert.Equal("not_enough_tickets", code));
        Assert.Equal(1, await api.CountReservationsAsync(eventCode));
        Assert.Equal(0, await api.GetAvailableTicketsAsync(eventCode));
    }
}
