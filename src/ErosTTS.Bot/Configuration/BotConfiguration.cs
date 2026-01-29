namespace ErosTTS.Bot.Configuration;

/// <summary>
/// Configuration for the Discord bot.
/// </summary>
public sealed class BotConfiguration
{
    public const string SectionName = "Discord";

    /// <summary>
    /// The Discord bot token.
    /// </summary>
    public required string Token { get; init; }

    /// <summary>
    /// Default text channel ID to monitor (can be overridden per guild via slash commands).
    /// </summary>
    public ulong? DefaultTextChannelId { get; init; }

    /// <summary>
    /// Default voice channel ID for playback (can be overridden per guild via slash commands).
    /// </summary>
    public ulong? DefaultVoiceChannelId { get; init; }

    /// <summary>
    /// Maximum message length to process. Messages longer than this will be truncated.
    /// </summary>
    public int MaxMessageLength { get; init; } = 500;

    /// <summary>
    /// Whether to process messages from other bots.
    /// </summary>
    public bool ProcessBotMessages { get; init; } = false;
}
