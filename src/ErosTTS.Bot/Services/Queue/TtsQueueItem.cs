namespace ErosTTS.Bot.Services.Queue;

/// <summary>
/// Represents an item in the TTS processing queue.
/// </summary>
public sealed record TtsQueueItem
{
    /// <summary>
    /// Unique identifier for this queue item.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The Discord guild (server) ID.
    /// </summary>
    public required ulong GuildId { get; init; }

    /// <summary>
    /// The text channel ID where the message originated.
    /// </summary>
    public required ulong TextChannelId { get; init; }

    /// <summary>
    /// The voice channel ID where audio should be played.
    /// </summary>
    public required ulong VoiceChannelId { get; init; }

    /// <summary>
    /// The text to convert to speech.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// The username of the message author.
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// When the item was added to the queue.
    /// </summary>
    public DateTimeOffset QueuedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Number of retry attempts for this item.
    /// </summary>
    public int RetryCount { get; set; } = 0;
}
