using System.Net;
using System.Net.Http.Json;
using FunEvents.Api.Tests.Infrastructure;
using FunEvents.Contracts.Events;

namespace FunEvents.Api.Tests;

public sealed class ReservationTests(FunEventsApiFactory api)
{
    private readonly HttpClient _client = api.CreateClient();

    [Fact]
    public async Task Reserve_WithAvailableTickets_Returns201AndDecrementsAvailability()
    {
        var eventCode = await api.CreateEventAsync(tickets: 10);
        var userCode = await api.CreateUserAsync();

        var response = await _client.ReserveAsync(eventCode, userCode, quantity: 3);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reservation = await response.ReadReservationAsync();
        Assert.Equal(eventCode, reservation.EventCode);
        Assert.Equal(userCode, reservation.UserCode);
        Assert.Equal(3, reservation.Quantity);
        Assert.Equal($"/reservations/{reservation.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(7, await api.GetAvailableTicketsAsync(eventCode));
    }

    [Fact]
    public async Task Reserve_WithUnknownEvent_Returns404EventNotFound()
    {
        var userCode = await api.CreateUserAsync();

        var response = await _client.ReserveAsync("NO-SUCH-EVENT", userCode, quantity: 1);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("event_not_found", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Reserve_WithUnknownUser_Returns404UserNotFoundAndKeepsInventory()
    {
        var eventCode = await api.CreateEventAsync(tickets: 5);

        var response = await _client.ReserveAsync(eventCode, "NO-SUCH-USER", quantity: 1);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("user_not_found", await response.ReadProblemCodeAsync());
        Assert.Equal(5, await api.GetAvailableTicketsAsync(eventCode));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(11)]
    public async Task Reserve_WithQuantityOutOfRange_Returns400AndKeepsInventory(int quantity)
    {
        var eventCode = await api.CreateEventAsync(tickets: 50);
        var userCode = await api.CreateUserAsync();

        var response = await _client.ReserveAsync(eventCode, userCode, quantity);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.ReadValidationErrorsAsync()).TryGetProperty("Quantity", out _));
        Assert.Equal(50, await api.GetAvailableTicketsAsync(eventCode));
    }

    [Fact]
    public async Task Reserve_WithoutUserCode_Returns400()
    {
        var eventCode = await api.CreateEventAsync(tickets: 5);

        var response = await _client.PostRawJsonAsync($"/events/{eventCode}/reservations", """{ "quantity": 1 }""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.ReadValidationErrorsAsync()).TryGetProperty("UserCode", out _));
    }

    [Fact]
    public async Task Reserve_WithMalformedJson_Returns400()
    {
        var eventCode = await api.CreateEventAsync(tickets: 5);

        var response = await _client.PostRawJsonAsync($"/events/{eventCode}/reservations", """{ "userCode": """);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reserve_WithMoreTicketsThanAvailable_Returns409AndKeepsInventory()
    {
        var eventCode = await api.CreateEventAsync(tickets: 2);
        var userCode = await api.CreateUserAsync();

        var response = await _client.ReserveAsync(eventCode, userCode, quantity: 3);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("not_enough_tickets", await response.ReadProblemCodeAsync());
        Assert.Equal(2, await api.GetAvailableTicketsAsync(eventCode));
        Assert.Equal(0, await api.CountReservationsAsync(eventCode));
        Assert.Equal(0, await api.CountReservedTicketsAsync(eventCode));
    }

    [Fact]
    public async Task Reserve_LastAvailableTickets_Returns201AndLeavesZero()
    {
        var eventCode = await api.CreateEventAsync(tickets: 4);
        var userCode = await api.CreateUserAsync();

        var response = await _client.ReserveAsync(eventCode, userCode, quantity: 4);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(0, await api.GetAvailableTicketsAsync(eventCode));
    }

    [Fact]
    public async Task GetReservation_AfterReserving_ReturnsTheSameReservation()
    {
        var eventCode = await api.CreateEventAsync(tickets: 10);
        var userCode = await api.CreateUserAsync();
        var created = await (await _client.ReserveAsync(eventCode, userCode, quantity: 2)).ReadReservationAsync();

        var response = await _client.GetAsync($"/reservations/{created.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created, await response.ReadReservationAsync());
    }

    [Fact]
    public async Task GetReservation_WithUnknownId_Returns404ReservationNotFound()
    {
        var response = await _client.GetAsync($"/reservations/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("reservation_not_found", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task GetEvent_AfterReserving_ShowsRemainingAvailability()
    {
        var eventCode = await api.CreateEventAsync(tickets: 10);
        var userCode = await api.CreateUserAsync();
        await _client.ReserveAsync(eventCode, userCode, quantity: 4);

        var ev = await _client.GetFromJsonAsync<EventResponse>($"/events/{eventCode}", TestContext.Current.CancellationToken);

        Assert.NotNull(ev);
        Assert.Equal(10, ev.TotalTickets);
        Assert.Equal(6, ev.AvailableTickets);
    }
}
