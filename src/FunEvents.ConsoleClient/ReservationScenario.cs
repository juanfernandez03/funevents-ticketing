namespace FunEvents.ConsoleClient;

/// <summary>Shows the event, sends one or many reservations and shows the event again.</summary>
public sealed class ReservationScenario(FunEventsApiClient api, TextWriter output)
{
    public Task<int> RunAsync(ClientOptions options, CancellationToken cancellationToken)
    {
        output.WriteLine($"FunEvents test client -> {api.BaseAddress}");
        throw new NotImplementedException();
    }
}
