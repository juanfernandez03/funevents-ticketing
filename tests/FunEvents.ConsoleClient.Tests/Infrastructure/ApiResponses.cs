using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FunEvents.Contracts;
using FunEvents.Contracts.Events;
using FunEvents.Contracts.Reservations;

namespace FunEvents.ConsoleClient.Tests.Infrastructure;

/// <summary>Responses shaped like the real API's, built from the shared contracts.</summary>
public static class ApiResponses
{
    public static HttpResponseMessage Event(string code = "ROCK-BCN-2027", int available = 500, int total = 500) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new EventResponse(
                code, "Rock Night Barcelona", "FunEvents Arena, Barcelona",
                new DateTimeOffset(2027, 3, 20, 20, 0, 0, TimeSpan.Zero), total, available)),
        };

    public static HttpResponseMessage Reservation(
        string eventCode = "ROCK-BCN-2027",
        string userCode = "USR-001",
        int quantity = 1,
        Guid? id = null,
        bool replayed = false)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = JsonContent.Create(new ReservationResponse(
                id ?? Guid.CreateVersion7(), eventCode, userCode, quantity, DateTimeOffset.UtcNow)),
        };
        if (replayed)
        {
            response.Headers.Add(ApiHeaders.IdempotentReplayed, "true");
        }

        return response;
    }

    public static HttpResponseMessage Problem(HttpStatusCode status, string title, string? code = null, string? detail = null) =>
        new(status)
        {
            Content = JsonContent.Create(
                new { title, status = (int)status, detail, code },
                mediaType: new MediaTypeHeaderValue("application/problem+json")),
        };
}
