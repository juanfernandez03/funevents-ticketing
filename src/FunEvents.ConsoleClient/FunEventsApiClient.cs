using System.Net;
using FunEvents.Contracts.Events;
using FunEvents.Contracts.Reservations;

namespace FunEvents.ConsoleClient;

/// <summary>Subset of an RFC 9457 problem response that the client shows to the user.</summary>
public sealed record ApiProblem(
    string? Title,
    string? Detail,
    string? Code,
    Dictionary<string, string[]>? Errors);

public sealed record ApiResult<T>(HttpStatusCode StatusCode, T? Value, ApiProblem? Problem, bool Replayed = false)
{
    public bool IsSuccess => Value is not null;
}

public sealed class FunEventsApiClient(HttpClient http)
{
    public Uri? BaseAddress => http.BaseAddress;

    public Task<ApiResult<EventResponse>> GetEventAsync(string eventCode, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<ApiResult<ReservationResponse>> ReserveAsync(
        string eventCode,
        ReserveTicketsRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
