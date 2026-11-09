using FunEvents.ConsoleClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

if (args is ["-h"] or ["--help"])
{
    Console.WriteLine(ClientOptions.Usage);
    return 0;
}

if (!ClientOptions.TryParse(args, out var options, out var error))
{
    Console.Error.WriteLine(error);
    Console.WriteLine();
    Console.WriteLine(ClientOptions.Usage);
    return 2;
}

// The host gives the client the same plumbing as the API: configuration, service discovery,
// resilience and OpenTelemetry, so its calls appear in the Aspire dashboard as part of the trace.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    // Read appsettings.json next to the executable, wherever the client is started from.
    ContentRootPath = AppContext.BaseDirectory,
});
builder.AddServiceDefaults();
builder.Services.AddHttpClient<FunEventsApiClient>(client =>
    client.BaseAddress = options.ApiUrl ?? new Uri("https+http://api"));

using var host = builder.Build();
await host.StartAsync();

var scenario = new ReservationScenario(host.Services.GetRequiredService<FunEventsApiClient>(), Console.Out);
var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
try
{
    return await Program.RunScenarioAsync(scenario, options, Console.Out, lifetime.ApplicationStopping);
}
finally
{
    // Do not use ApplicationStopping here: shutdown must still flush telemetry after cancellation.
    await host.StopAsync();
}

public partial class Program
{
    public static async Task<int> RunScenarioAsync(
        ReservationScenario scenario, ClientOptions options, TextWriter output, CancellationToken cancellationToken)
    {
        try
        {
            return await scenario.RunAsync(options, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            output.WriteLine("[CANCELLED] Stopped by the user");
            return 130;
        }
    }
}
