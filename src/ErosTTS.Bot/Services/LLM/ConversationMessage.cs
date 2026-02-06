namespace ErosTTS.Bot.Services.LLM;

/// <summary>
/// Represents a message in the conversation history for LLM requests.
/// </summary>
public sealed record ConversationMessage
{
    public required string Role { get; init; }
    public required string Content { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
