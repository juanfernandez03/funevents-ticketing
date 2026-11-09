namespace FunEvents.Contracts;

public static class ApiHeaders
{
    /// <summary>Client-generated key that makes a write safe to retry (e.g. a GUID per user action).</summary>
    public const string IdempotencyKey = "Idempotency-Key";

    public const int IdempotencyKeyMaxLength = 64;

    /// <summary>Set to <c>true</c> when the response replays the result of an earlier request with the same key.</summary>
    public const string IdempotentReplayed = "Idempotent-Replayed";

    /// <summary>
    /// The single rule for a key, shared by the API and the client: 1 to 64 visible ASCII characters, sent once.
    /// No whitespace, and no comma, because a repeated header reaches the server joined by commas.
    /// </summary>
    public static bool IsValidIdempotencyKey(string key) =>
        key.Length is >= 1 and <= IdempotencyKeyMaxLength
        && key.All(character => character is >= '!' and <= '~' and not ',');
}
