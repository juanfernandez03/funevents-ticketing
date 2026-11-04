namespace FunEvents.Api.Data.Entities;

/// <summary>A registered buyer. Channels identify users by their public code.</summary>
public sealed class UserEntity
{
    public Guid Id { get; set; }

    public required string Code { get; set; }

    public required string FullName { get; set; }

    public required string Email { get; set; }
}
