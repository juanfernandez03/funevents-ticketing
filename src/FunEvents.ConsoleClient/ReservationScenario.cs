using System.Diagnostics;
using System.Net;
using FunEvents.Contracts.Events;
using FunEvents.Contracts.Reservations;

namespace FunEvents.ConsoleClient;

/// <summary>Shows the event, sends one or many reservations and shows the event again.</summary>
public sealed class ReservationScenario(FunEventsApiClient api, TextWriter output)
{
    public async Task<int> RunAsync(ClientOptions options, CancellationToken cancellationToken)
    {
        output.WriteLine($"FunEvents test client -> {api.BaseAddress}");
        output.WriteLine();

        var before = await api.GetEventAsync(options.EventCode, cancellationToken);
        if (!before.IsSuccess)
        {
            WriteProblem("Could not load the event", before.StatusCode, before.Problem);
            return 1;
        }

        WriteEvent(before.Value!);

        var request = new ReserveTicketsRequest(options.UserCode, options.Quantity);
        var exitCode = options.Parallel == 1
            ? await ReserveOnceAsync(options, request, cancellationToken)
            : await ReserveInParallelAsync(options, request, cancellationToken);

        var after = await api.GetEventAsync(options.EventCode, cancellationToken);
        if (after.IsSuccess)
        {
            output.WriteLine();
            output.WriteLine($"Available now: {after.Value!.AvailableTickets}/{after.Value.TotalTickets}");
        }

        return exitCode;
    }

    private async Task<int> ReserveOnceAsync(ClientOptions options, ReserveTicketsRequest request, CancellationToken cancellationToken)
    {
        var key = options.IdempotencyKey ?? Guid.NewGuid().ToString();
        output.WriteLine($"Reserving {request.Quantity} ticket(s) for {request.UserCode} (Idempotency-Key: {key})...");

        var result = await api.ReserveAsync(options.EventCode, request, key, cancellationToken);
        if (!result.IsSuccess)
        {
            WriteProblem("Reservation rejected", result.StatusCode, result.Problem);
            return 1;
        }

        var reservation = result.Value!;
        output.WriteLine(result.Replayed
            ? $"[OK] Same key already used: returning the existing reservation {reservation.Id}"
            : $"[OK] Reservation {reservation.Id} created");
        output.WriteLine($"     {reservation.Quantity} ticket(s) for {reservation.EventCode}, user {reservation.UserCode}, at {reservation.CreatedAtUtc:u}");
        return 0;
    }

    private async Task<int> ReserveInParallelAsync(ClientOptions options, ReserveTicketsRequest request, CancellationToken cancellationToken)
    {
        var keyInfo = options.IdempotencyKey is null ? "a new key each" : $"the same key '{options.IdempotencyKey}'";
        output.WriteLine($"Sending {options.Parallel} reservations of {request.Quantity} ticket(s) at the same time, {keyInfo}...");

        var stopwatch = Stopwatch.StartNew();
        var results = await Task.WhenAll(Enumerable.Range(0, options.Parallel).Select(_ =>
            api.ReserveAsync(options.EventCode, request, options.IdempotencyKey ?? Guid.NewGuid().ToString(), cancellationToken)));
        stopwatch.Stop();

        output.WriteLine($"Done in {stopwatch.ElapsedMilliseconds} ms");
        output.WriteLine();
        foreach (var group in results.GroupBy(r => r.StatusCode).OrderBy(g => g.Key))
        {
            var reason = group.First().Problem?.Code ?? (group.Key == HttpStatusCode.Created ? "created" : string.Empty);
            output.WriteLine($"  {(int)group.Key} {group.Key,-12} x{group.Count(),-4} {reason}");
        }

        var created = results.Where(r => r.IsSuccess).ToArray();
        var distinctReservations = created.Select(r => r.Value!.Id).Distinct().Count();
        output.WriteLine();
        output.WriteLine($"Distinct reservations: {distinctReservations}, tickets reserved: {created.DistinctBy(r => r.Value!.Id).Sum(r => r.Value!.Quantity)}");
        return 0;
    }

    private void WriteEvent(EventResponse ev)
    {
        output.WriteLine($"{ev.Name} ({ev.Code})");
        output.WriteLine($"{ev.Venue}, {ev.StartsAtUtc:yyyy-MM-dd HH:mm} UTC");
        output.WriteLine($"Available: {ev.AvailableTickets}/{ev.TotalTickets}");
        output.WriteLine();
    }

    private void WriteProblem(string message, HttpStatusCode status, ApiProblem? problem)
    {
        output.WriteLine($"[FAILED] {message}: {(int)status} {problem?.Title}");
        if (problem?.Detail is { } detail)
        {
            output.WriteLine($"         {detail}");
        }

        foreach (var (field, errors) in problem?.Errors ?? [])
        {
            output.WriteLine($"         {field}: {string.Join(" ", errors)}");
        }
    }
}
