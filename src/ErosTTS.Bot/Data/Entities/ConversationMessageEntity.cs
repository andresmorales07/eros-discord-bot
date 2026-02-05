namespace ErosTTS.Bot.Data.Entities;

/// <summary>
/// EF Core entity for a conversation message in the character history.
/// </summary>
public sealed class ConversationMessageEntity
{
    /// <summary>
    /// Auto-increment primary key for stable ordering.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the guild character state.
    /// </summary>
    public long GuildId { get; set; }

    /// <summary>
    /// The role of the message sender ("user" or "assistant").
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// The message content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// When the message was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Navigation property back to the guild character state.
    /// </summary>
    public GuildCharacterStateEntity? GuildCharacterState { get; set; }
}
