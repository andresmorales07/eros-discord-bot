namespace ErosTTS.Bot.Services.Audio;

/// <summary>
/// Interface for audio playback services.
/// </summary>
public interface IAudioService
{
    /// <summary>
    /// Plays audio in the specified voice channel.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="voiceChannelId">The voice channel ID to play audio in.</param>
    /// <param name="audioStream">The audio stream to play.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PlayAudioAsync(ulong guildId, ulong voiceChannelId, Stream audioStream, CancellationToken ct = default);

    /// <summary>
    /// Disconnects from the voice channel in the specified guild.
    /// </summary>
    /// <param name="guildId">The guild ID to disconnect from.</param>
    Task DisconnectAsync(ulong guildId);

    /// <summary>
    /// Checks if the bot is connected to voice in the specified guild.
    /// </summary>
    /// <param name="guildId">The guild ID to check.</param>
    /// <returns>True if connected to a voice channel in the guild.</returns>
    bool IsConnected(ulong guildId);

    /// <summary>
    /// Gets the IDs of all guilds where the bot is currently connected to voice.
    /// </summary>
    IReadOnlyCollection<ulong> GetConnectedGuildIds();
}
