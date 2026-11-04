using System.Net;
using FunEvents.ConsoleClient.Tests.Infrastructure;

namespace FunEvents.ConsoleClient.Tests;

public sealed class ReservationScenarioTests
{
    private static readonly ClientOptions OneReservation =
        new("ROCK-BCN-2027", "USR-001", Quantity: 2, Parallel: 1, IdempotencyKey: null, ApiUrl: null);

    [Fact]
    public async Task RunAsync_WhenTheReservationIsCreated_ShowsItAndReturnsZero()
    {
        var reservationId = Guid.CreateVersion7();
        var api = new StubApiHandler(request => request.Method == HttpMethod.Get
            ? ApiResponses.Event(available: 500)
            : ApiResponses.Reservation(quantity: 2, id: reservationId));
        using var output = new StringWriter();

        var exitCode = await new ReservationScenario(api.CreateClient(), output)
            .RunAsync(OneReservation, TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains($"[OK] Reservation {reservationId} created", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(["GET", "POST", "GET"], api.Requests.Select(r => r.Method.Method));
    }

    [Fact]
    public async Task RunAsync_SendsANewIdempotencyKeyUnlessOneIsGiven()
    {
        var api = new StubApiHandler(request => request.Method == HttpMethod.Get ? ApiResponses.Event() : ApiResponses.Reservation());
        var scenario = new ReservationScenario(api.CreateClient(), TextWriter.Null);

        await scenario.RunAsync(OneReservation, TestContext.Current.CancellationToken);
        await scenario.RunAsync(OneReservation, TestContext.Current.CancellationToken);
        await scenario.RunAsync(OneReservation with { IdempotencyKey = "retry-1" }, TestContext.Current.CancellationToken);

        var keys = api.Requests.Where(r => r.Method == HttpMethod.Post).Select(r => r.IdempotencyKey).ToArray();
        Assert.Equal(3, keys.Length);
        Assert.All(keys, key => Assert.False(string.IsNullOrEmpty(key)));
        Assert.NotEqual(keys[0], keys[1]);
        Assert.Equal("retry-1", keys[2]);
    }

    [Fact]
    public async Task RunAsync_WhenTheReservationIsRejected_ShowsWhyAndReturnsOne()
    {
        var api = new StubApiHandler(request => request.Method == HttpMethod.Get
            ? ApiResponses.Event(available: 1)
            : ApiResponses.Problem(HttpStatusCode.Conflict, "Conflict with the current state", "not_enough_tickets",
                "Event 'ROCK-BCN-2027' does not have 2 ticket(s) available."));
        using var output = new StringWriter();

        var exitCode = await new ReservationScenario(api.CreateClient(), output)
            .RunAsync(OneReservation, TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains("[FAILED] Reservation rejected: 409", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("does not have 2 ticket(s) available", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WhenTheEventDoesNotExist_ReturnsOneWithoutReserving()
    {
        var api = new StubApiHandler(_ => ApiResponses.Problem(HttpStatusCode.NotFound, "Resource not found", "event_not_found"));
        using var output = new StringWriter();

        var exitCode = await new ReservationScenario(api.CreateClient(), output)
            .RunAsync(OneReservation, TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains("[FAILED] Could not load the event: 404", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(api.Requests, r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task RunAsync_InParallel_CountsTheReservationsThatWereCreated()
    {
        // Only two tickets left: the stub accepts the first two requests and rejects the rest.
        var accepted = 0;
        var api = new StubApiHandler(request =>
            request.Method == HttpMethod.Get ? ApiResponses.Event(available: 2)
            : Interlocked.Increment(ref accepted) <= 2 ? ApiResponses.Reservation()
            : ApiResponses.Problem(HttpStatusCode.Conflict, "Conflict with the current state", "not_enough_tickets"));
        using var output = new StringWriter();
        var options = OneReservation with { Quantity = 1, Parallel = 5 };

        var exitCode = await new ReservationScenario(api.CreateClient(), output)
            .RunAsync(options, TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Equal(5, api.Requests.Count(r => r.Method == HttpMethod.Post));
        var text = output.ToString();
        Assert.Matches(@"201 Created\s+x2\b", text);
        Assert.Matches(@"409 Conflict\s+x3\s+not_enough_tickets", text);
        Assert.Contains("Distinct reservations: 2, tickets reserved: 2", text, StringComparison.Ordinal);
    }
}
