namespace ErosTTS.Bot.Services.Character;

/// <summary>
/// Represents a message in the conversation history.
/// </summary>
public sealed record ConversationMessage
{
    /// <summary>
    /// The role of the message sender ("user" or "assistant").
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// The content of the message.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// When the message was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
