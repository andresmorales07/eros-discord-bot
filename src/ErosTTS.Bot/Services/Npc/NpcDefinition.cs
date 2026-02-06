namespace ErosTTS.Bot.Services.Npc;

/// <summary>
/// Represents an NPC with a name, personality, and optional voice override.
/// </summary>
public sealed record NpcDefinition
{
    public required int Id { get; init; }
    public required ulong GuildId { get; init; }
    public required string Name { get; init; }
    public required string Personality { get; init; }
    public string? VoiceId { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
