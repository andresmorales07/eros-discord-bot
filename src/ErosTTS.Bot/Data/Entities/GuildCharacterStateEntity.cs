namespace ErosTTS.Bot.Data.Entities;

/// <summary>
/// EF Core entity for guild character state.
/// </summary>
public sealed class GuildCharacterStateEntity
{
    /// <summary>
    /// Discord guild ID stored as signed long.
    /// </summary>
    public long GuildId { get; set; }

    /// <summary>
    /// The character context/system prompt.
    /// </summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>
    /// When this state was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Navigation property for conversation messages.
    /// </summary>
    public List<ConversationMessageEntity> ConversationHistory { get; set; } = [];
}
