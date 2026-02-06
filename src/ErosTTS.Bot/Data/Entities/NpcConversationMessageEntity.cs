namespace ErosTTS.Bot.Data.Entities;

/// <summary>
/// EF Core entity for NPC conversation messages.
/// </summary>
public sealed class NpcConversationMessageEntity
{
    public int Id { get; set; }
    public long GuildId { get; set; }
    public int? NpcId { get; set; }
    public string? NpcName { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }

    public NpcEntity? Npc { get; set; }
}
