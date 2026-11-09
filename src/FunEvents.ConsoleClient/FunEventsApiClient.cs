using System.Net;
using System.Net.Http.Json;
using FunEvents.Contracts;
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

/// <summary>Typed HTTP client for the FunEvents API. Retries come from the standard resilience handler,
/// which is safe for POST because every reservation carries an idempotency key.</summary>
public sealed class FunEventsApiClient(HttpClient http)
{
    public Uri? BaseAddress => http.BaseAddress;

    public async Task<ApiResult<EventResponse>> GetEventAsync(string eventCode, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri($"events/{Uri.EscapeDataString(eventCode)}", UriKind.Relative), cancellationToken);
        return await ReadAsync<EventResponse>(response, cancellationToken);
    }

    public async Task<ApiResult<ReservationResponse>> ReserveAsync(
        string eventCode,
        ReserveTicketsRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri($"events/{Uri.EscapeDataString(eventCode)}/reservations", UriKind.Relative));
        message.Headers.Add(ApiHeaders.IdempotencyKey, idempotencyKey);
        message.Content = JsonContent.Create(request);

        using var response = await http.SendAsync(message, cancellationToken);
        var result = await ReadAsync<ReservationResponse>(response, cancellationToken);
        var replayed = response.Headers.TryGetValues(ApiHeaders.IdempotentReplayed, out var values)
            && values.Contains("true", StringComparer.OrdinalIgnoreCase);
        return result with { Replayed = replayed };
    }

    private static async Task<ApiResult<T>> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
        where T : class
    {
        if (response.IsSuccessStatusCode)
        {
            var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
            return new ApiResult<T>(response.StatusCode, value, Problem: null);
        }

        var problem = response.Content.Headers.ContentType?.MediaType == "application/problem+json"
            ? await response.Content.ReadFromJsonAsync<ApiProblem>(cancellationToken)
            : new ApiProblem(response.ReasonPhrase, Detail: null, Code: null, Errors: null);
        return new ApiResult<T>(response.StatusCode, Value: null, problem);
    }
}
