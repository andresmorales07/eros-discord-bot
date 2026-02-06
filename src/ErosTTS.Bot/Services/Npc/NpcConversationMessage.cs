namespace ErosTTS.Bot.Services.Npc;

/// <summary>
/// A conversation message associated with an NPC.
/// </summary>
public sealed record NpcConversationMessage
{
    public required string Role { get; init; }
    public required string Content { get; init; }
    public string? NpcName { get; init; }
    public int? NpcId { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
