using System.Collections.Concurrent;
using FunEvents.Contracts;

namespace FunEvents.ConsoleClient.Tests.Infrastructure;

public sealed record RecordedRequest(HttpMethod Method, string Path, string? IdempotencyKey, string? Body);

/// <summary>
/// Stands in for the API: records each request and answers with the response the test chose.
/// The real API is covered by FunEvents.Api.Tests; here only the client's behaviour matters.
/// </summary>
public sealed class StubApiHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    private readonly ConcurrentQueue<RecordedRequest> _requests = new();

    public IReadOnlyCollection<RecordedRequest> Requests => _requests;

    public FunEventsApiClient CreateClient() =>
        new(new HttpClient(this) { BaseAddress = new Uri("http://funevents.test/") });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var idempotencyKey = request.Headers.TryGetValues(ApiHeaders.IdempotencyKey, out var values) ? values.Single() : null;
        _requests.Enqueue(new RecordedRequest(request.Method, request.RequestUri!.AbsolutePath, idempotencyKey, body));
        return respond(request);
    }
}
