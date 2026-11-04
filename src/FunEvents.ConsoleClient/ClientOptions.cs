using System.Diagnostics.CodeAnalysis;

namespace FunEvents.ConsoleClient;

/// <summary>Command-line options. Hand-parsed: six flags do not justify a dependency.</summary>
public sealed record ClientOptions(
    string EventCode,
    string UserCode,
    int Quantity,
    int Parallel,
    string? IdempotencyKey,
    Uri? ApiUrl)
{
    public static bool TryParse(
        string[] args,
        [NotNullWhen(true)] out ClientOptions? options,
        [NotNullWhen(false)] out string? error) =>
        throw new NotImplementedException();
}
