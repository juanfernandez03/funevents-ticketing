using System.ComponentModel.DataAnnotations;

namespace FunEvents.Contracts;

/// <summary>Validates an optional Idempotency-Key with <see cref="ApiHeaders.IsValidIdempotencyKey"/>; absent is valid.</summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class IdempotencyKeyAttribute()
    : ValidationAttribute(
        $"The {ApiHeaders.IdempotencyKey} header must be sent once, with 1 to {ApiHeaders.IdempotencyKeyMaxLength} visible ASCII characters and no comma.")
{
    public override bool IsValid(object? value) => value switch
    {
        null => true,
        string key => ApiHeaders.IsValidIdempotencyKey(key),
        _ => false,
    };
}
