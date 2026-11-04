using System.Text.Json;

namespace FunEvents.Api.Tests.Infrastructure;

public static class HttpTestExtensions
{
    /// <summary>Reads the machine-readable <c>code</c> extension of a ProblemDetails response.</summary>
    public static async Task<string?> ReadProblemCodeAsync(this HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}
