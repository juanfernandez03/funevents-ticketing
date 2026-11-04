using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FunEvents.Contracts.Reservations;

namespace FunEvents.Api.Tests.Infrastructure;

public static class HttpTestExtensions
{
    public static Task<HttpResponseMessage> ReserveAsync(
        this HttpClient client,
        string eventCode,
        string userCode,
        int quantity)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/events/{eventCode}/reservations")
        {
            Content = JsonContent.Create(new ReserveTicketsRequest(userCode, quantity)),
        };

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static Task<HttpResponseMessage> PostRawJsonAsync(this HttpClient client, string url, string json) =>
        client.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

    public static async Task<ReservationResponse> ReadReservationAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<ReservationResponse>(TestContext.Current.CancellationToken)
        ?? throw new InvalidOperationException("Empty reservation response.");

    /// <summary>Reads the machine-readable <c>code</c> extension of a ProblemDetails response.</summary>
    public static async Task<string?> ReadProblemCodeAsync(this HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    /// <summary>Reads the <c>errors</c> dictionary of a validation ProblemDetails response.</summary>
    public static async Task<JsonElement> ReadValidationErrorsAsync(this HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.GetProperty("errors").Clone();
    }
}
