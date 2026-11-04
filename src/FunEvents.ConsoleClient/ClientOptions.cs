using System.Diagnostics.CodeAnalysis;
using System.Globalization;

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
    public const string Usage = """
        Usage:
          FunEvents.ConsoleClient --event <code> --user <code> [options]

        Options:
          --event <code>             Event code, e.g. ROCK-BCN-2027 (required)
          --user <code>              User code, e.g. USR-001 (required)
          --quantity <n>             Tickets per reservation (default: 1)
          --parallel <n>             Send n reservations at the same time (default: 1)
          --idempotency-key <key>    Reuse this key instead of a new one per request
          --api-url <url>            API base address (default: service discovery, http://localhost:5080)

        Examples:
          FunEvents.ConsoleClient --event ROCK-BCN-2027 --user USR-001 --quantity 2
          FunEvents.ConsoleClient --event JAZZ-VLC-2027 --user USR-002 --parallel 50
        """;

    public static bool TryParse(
        string[] args,
        [NotNullWhen(true)] out ClientOptions? options,
        [NotNullWhen(false)] out string? error)
    {
        options = null;
        string? eventCode = null, userCode = null, idempotencyKey = null;
        int quantity = 1, parallel = 1;
        Uri? apiUrl = null;

        for (var i = 0; i < args.Length; i++)
        {
            var name = args[i];
            if (i + 1 >= args.Length)
            {
                error = $"Missing value for '{name}'.";
                return false;
            }

            var value = args[++i];
            switch (name)
            {
                case "--event":
                    eventCode = value;
                    break;
                case "--user":
                    userCode = value;
                    break;
                case "--idempotency-key":
                    idempotencyKey = value;
                    break;
                case "--quantity" when TryParsePositive(value, out quantity):
                    break;
                case "--parallel" when TryParsePositive(value, out parallel):
                    break;
                case "--api-url" when Uri.TryCreate(value, UriKind.Absolute, out apiUrl):
                    break;
                case "--quantity" or "--parallel" or "--api-url":
                    error = $"Invalid value '{value}' for '{name}'.";
                    return false;
                default:
                    error = $"Unknown option '{name}'.";
                    return false;
            }
        }

        if (eventCode is null || userCode is null)
        {
            error = "Both --event and --user are required.";
            return false;
        }

        // Range rules (e.g. at most 10 tickets) are the API's job: the client shows its validation errors.
        options = new ClientOptions(eventCode, userCode, quantity, parallel, idempotencyKey, apiUrl);
        error = null;
        return true;
    }

    private static bool TryParsePositive(string value, out int result) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result) && result > 0;
}
