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
int exitCode;
try
{
    exitCode = await scenario.RunAsync(options, CancellationToken.None);
}
catch (HttpRequestException exception)
{
    Console.Error.WriteLine($"[FAILED] Could not reach the API: {exception.Message}");
    exitCode = 1;
}

// Stopping the host flushes the telemetry before the process exits.
await host.StopAsync();
return exitCode;
