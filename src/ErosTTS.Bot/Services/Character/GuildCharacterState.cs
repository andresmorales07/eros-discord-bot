namespace ErosTTS.Bot.Services.Character;

/// <summary>
/// Character state for a specific guild, including context and conversation history.
/// </summary>
public sealed record GuildCharacterState
{
    /// <summary>
    /// The guild ID.
    /// </summary>
    public required ulong GuildId { get; init; }

    /// <summary>
    /// The character context/system prompt describing who the bot is playing.
    /// </summary>
    public string Context { get; init; } = string.Empty;

    /// <summary>
    /// The conversation history for this guild.
    /// </summary>
    public IReadOnlyList<ConversationMessage> ConversationHistory { get; init; } = [];

    /// <summary>
    /// When this state was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
