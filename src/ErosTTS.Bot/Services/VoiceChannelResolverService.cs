using ErosTTS.Bot.Services.Audio;
using ErosTTS.Bot.Services.Guild;

namespace ErosTTS.Bot.Services;

/// <summary>
/// Resolves which voice channel to use for TTS playback.
/// </summary>
public interface IVoiceChannelResolverService
{
    /// <summary>
    /// Resolves the voice channel for TTS playback using a three-step fallback:
    /// 1. Explicit channel ID (if provided)
    /// 2. User's current voice channel
    /// 3. Guild's configured default voice channel
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID (for voice state lookup).</param>
    /// <param name="explicitChannelId">An explicitly specified channel ID, or null.</param>
    /// <returns>The resolved voice channel ID, or null if none could be determined.</returns>
    Task<ulong?> ResolveVoiceChannelAsync(ulong guildId, ulong userId, ulong? explicitChannelId);
}

/// <summary>
/// Resolves voice channels using a three-step fallback strategy.
/// </summary>
internal sealed class VoiceChannelResolverService : IVoiceChannelResolverService
{
    private readonly IVoiceChannelInspector _inspector;
    private readonly IGuildConfigurationService _guildConfig;

    public VoiceChannelResolverService(
        IVoiceChannelInspector inspector,
        IGuildConfigurationService guildConfig)
    {
        _inspector = inspector;
        _guildConfig = guildConfig;
    }

    public async Task<ulong?> ResolveVoiceChannelAsync(ulong guildId, ulong userId, ulong? explicitChannelId)
    {
        // Step 1: Explicit channel
        if (explicitChannelId.HasValue)
            return explicitChannelId.Value;

        // Step 2: User's current voice channel
        var userChannel = _inspector.GetUserVoiceChannelId(guildId, userId);
        if (userChannel.HasValue)
            return userChannel.Value;

        // Step 3: Guild default
        var config = await _guildConfig.GetConfigurationAsync(guildId);
        if (config?.VoiceChannelId.HasValue == true)
            return config.VoiceChannelId.Value;

        return null;
    }
}
