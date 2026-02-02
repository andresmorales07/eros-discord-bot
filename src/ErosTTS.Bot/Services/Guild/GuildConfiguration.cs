namespace ErosTTS.Bot.Services.Guild;

/// <summary>
/// TTS configuration for a specific guild.
/// </summary>
public sealed record GuildTtsConfiguration
{
    /// <summary>
    /// The guild ID.
    /// </summary>
    public required ulong GuildId { get; init; }

    /// <summary>
    /// The text channel ID to monitor for messages.
    /// </summary>
    public ulong? TextChannelId { get; init; }

    /// <summary>
    /// The voice channel ID to play TTS audio in.
    /// </summary>
    public ulong? VoiceChannelId { get; init; }

    /// <summary>
    /// Custom ElevenLabs voice ID for this guild. Null uses the default voice.
    /// </summary>
    public string? VoiceId { get; init; }

    /// <summary>
    /// When this configuration was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
