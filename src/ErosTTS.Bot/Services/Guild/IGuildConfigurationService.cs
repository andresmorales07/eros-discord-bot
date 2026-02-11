namespace ErosTTS.Bot.Services.Guild;

/// <summary>
/// Interface for managing per-guild TTS configurations.
/// </summary>
public interface IGuildConfigurationService
{
    /// <summary>
    /// Sets the TTS channels for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="textChannelId">The text channel to monitor.</param>
    /// <param name="voiceChannelId">The voice channel for playback.</param>
    /// <param name="voiceId">Optional custom ElevenLabs voice ID.</param>
    Task SetChannelsAsync(ulong guildId, ulong textChannelId, ulong voiceChannelId, string? voiceId = null);

    /// <summary>
    /// Gets the TTS configuration for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The guild configuration, or null if not configured.</returns>
    Task<GuildTtsConfiguration?> GetConfigurationAsync(ulong guildId);

    /// <summary>
    /// Removes the TTS configuration for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    Task RemoveConfigurationAsync(ulong guildId);

    /// <summary>
    /// Sets the TTS provider for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="providerName">The provider name, or null to use the default.</param>
    Task SetTtsProviderAsync(ulong guildId, string? providerName);

    /// <summary>
    /// Gets all configured guilds.
    /// </summary>
    /// <returns>A collection of all guild configurations.</returns>
    Task<IReadOnlyCollection<GuildTtsConfiguration>> GetAllConfigurationsAsync();
}
