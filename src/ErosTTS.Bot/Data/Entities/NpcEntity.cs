namespace ErosTTS.Bot.Data.Entities;

/// <summary>
/// EF Core entity for an NPC definition.
/// </summary>
public sealed class NpcEntity
{
    public int Id { get; set; }
    public long GuildId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Personality { get; set; } = string.Empty;
    public string? VoiceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<NpcConversationMessageEntity> ConversationMessages { get; set; } = [];
}
