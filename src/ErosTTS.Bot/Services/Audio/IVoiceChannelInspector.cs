namespace ErosTTS.Bot.Services.Audio;

/// <summary>
/// Abstraction for inspecting voice channel state, enabling testability
/// of voice inactivity detection logic without depending on sealed GatewayClient.
/// </summary>
internal interface IVoiceChannelInspector
{
    /// <summary>
    /// Checks if the bot is currently connected to a voice channel in the specified guild.
    /// </summary>
    bool IsBotConnected(ulong guildId);

    /// <summary>
    /// Gets the voice channel ID that the bot is currently in for the specified guild.
    /// </summary>
    /// <returns>The channel ID, or null if the bot is not in a voice channel.</returns>
    ulong? GetBotVoiceChannelId(ulong guildId);

    /// <summary>
    /// Counts the number of non-bot users in the specified voice channel.
    /// </summary>
    int CountNonBotUsersInChannel(ulong guildId, ulong channelId);

    /// <summary>
    /// Disconnects the bot from voice in the specified guild.
    /// </summary>
    Task DisconnectBotAsync(ulong guildId);

    /// <summary>
    /// Gets the IDs of all guilds where the bot is currently connected to voice.
    /// </summary>
    IReadOnlyCollection<ulong> GetConnectedGuildIds();

    /// <summary>
    /// Gets the voice channel ID that a user is currently in for the specified guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>The channel ID, or null if the user is not in a voice channel.</returns>
    ulong? GetUserVoiceChannelId(ulong guildId, ulong userId);
}
