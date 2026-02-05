namespace ErosTTS.Bot.Data.Entities;

/// <summary>
/// EF Core entity for guild TTS configuration.
/// </summary>
public sealed class GuildTtsConfigurationEntity
{
    /// <summary>
    /// Discord guild ID stored as signed long.
    /// </summary>
    public long GuildId { get; set; }

    /// <summary>
    /// The text channel ID to monitor for messages.
    /// </summary>
    public long? TextChannelId { get; set; }

    /// <summary>
    /// The voice channel ID to play TTS audio in.
    /// </summary>
    public long? VoiceChannelId { get; set; }

    /// <summary>
    /// Custom ElevenLabs voice ID for this guild.
    /// </summary>
    public string? VoiceId { get; set; }

    /// <summary>
    /// When this configuration was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
