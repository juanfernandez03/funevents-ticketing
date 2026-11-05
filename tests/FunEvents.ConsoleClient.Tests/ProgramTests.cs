using FunEvents.ConsoleClient.Tests.Infrastructure;

namespace FunEvents.ConsoleClient.Tests;

public sealed class ProgramTests
{
    private static readonly ClientOptions Options = new("ROCK-BCN-2027", "USR-001", 1, 1, null, null);

    [Fact]
    public async Task RunScenarioAsync_WhenCancelledDuringAReservation_ReportsCancellationAndStopsRequesting()
    {
        using var cancellation = new CancellationTokenSource();
        using var api = new StubApiHandler(request =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return ApiResponses.Event();
            }

            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        });
        using var output = new StringWriter();
        var scenario = new ReservationScenario(api.CreateClient(), output);

        var exitCode = await Program.RunScenarioAsync(scenario, Options, output, cancellation.Token);

        Assert.Equal(130, exitCode);
        Assert.EndsWith($"[CANCELLED] Stopped by the user{Environment.NewLine}", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("[FAILED]", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(["GET", "POST"], api.Requests.Select(r => r.Method.Method));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public async Task RunScenarioAsync_WithoutCancellation_PreservesTheScenarioExitCode(bool reject, int expected)
    {
        using var api = new StubApiHandler(request => request.Method == HttpMethod.Get
            ? ApiResponses.Event()
            : reject
                ? ApiResponses.Problem(System.Net.HttpStatusCode.Conflict, "Conflict", "not_enough_tickets")
                : ApiResponses.Reservation());
        using var output = new StringWriter();
        var scenario = new ReservationScenario(api.CreateClient(), output);

        var exitCode = await Program.RunScenarioAsync(scenario, Options, output, TestContext.Current.CancellationToken);

        Assert.Equal(expected, exitCode);
        Assert.DoesNotContain("[CANCELLED]", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(["GET", "POST", "GET"], api.Requests.Select(r => r.Method.Method));
    }
}
