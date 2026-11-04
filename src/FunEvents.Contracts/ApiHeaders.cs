namespace FunEvents.Contracts;

public static class ApiHeaders
{
    /// <summary>Client-generated key that makes a write safe to retry (e.g. a GUID per user action).</summary>
    public const string IdempotencyKey = "Idempotency-Key";

    public const int IdempotencyKeyMaxLength = 64;

    /// <summary>Set to <c>true</c> when the response replays the result of an earlier request with the same key.</summary>
    public const string IdempotentReplayed = "Idempotent-Replayed";
}
