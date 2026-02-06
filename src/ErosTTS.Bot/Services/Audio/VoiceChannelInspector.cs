using Microsoft.Extensions.Logging;
using NetCord.Gateway;

namespace ErosTTS.Bot.Services.Audio;

/// <summary>
/// Inspects voice channel state using the GatewayClient cache and IAudioService.
/// </summary>
internal sealed class VoiceChannelInspector : IVoiceChannelInspector
{
    private readonly GatewayClient _gatewayClient;
    private readonly IAudioService _audioService;
    private readonly ILogger<VoiceChannelInspector> _logger;

    public VoiceChannelInspector(
        GatewayClient gatewayClient,
        IAudioService audioService,
        ILogger<VoiceChannelInspector> logger)
    {
        _gatewayClient = gatewayClient;
        _audioService = audioService;
        _logger = logger;
    }

    public bool IsBotConnected(ulong guildId) => _audioService.IsConnected(guildId);

    public ulong? GetBotVoiceChannelId(ulong guildId)
    {
        if (!_gatewayClient.Cache.Guilds.TryGetValue(guildId, out var guild))
            return null;

        var botId = _gatewayClient.Id;
        if (guild.VoiceStates.TryGetValue(botId, out var voiceState))
            return voiceState.ChannelId;

        return null;
    }

    public int CountNonBotUsersInChannel(ulong guildId, ulong channelId)
    {
        if (!_gatewayClient.Cache.Guilds.TryGetValue(guildId, out var guild))
            return 0;

        var botId = _gatewayClient.Id;

        return guild.VoiceStates.Values
            .Count(vs => vs.ChannelId == channelId && vs.UserId != botId);
    }

    public Task DisconnectBotAsync(ulong guildId) => _audioService.DisconnectAsync(guildId);

    public IReadOnlyCollection<ulong> GetConnectedGuildIds() => _audioService.GetConnectedGuildIds();
}
