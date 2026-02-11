using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ErosTTS.Bot.Services.Guild;

/// <summary>
/// In-memory implementation of guild configuration storage.
/// </summary>
public sealed class GuildConfigurationService : IGuildConfigurationService
{
    private readonly ConcurrentDictionary<ulong, GuildTtsConfiguration> _configurations = new();
    private readonly ILogger<GuildConfigurationService> _logger;

    public GuildConfigurationService(ILogger<GuildConfigurationService> logger)
    {
        _logger = logger;
    }

    public Task SetChannelsAsync(ulong guildId, ulong textChannelId, ulong voiceChannelId, string? voiceId = null)
    {
        var config = new GuildTtsConfiguration
        {
            GuildId = guildId,
            TextChannelId = textChannelId,
            VoiceChannelId = voiceChannelId,
            VoiceId = voiceId,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _configurations[guildId] = config;

        _logger.LogInformation(
            "Updated TTS configuration for guild {GuildId}: text={TextChannelId}, voice={VoiceChannelId}, voiceId={VoiceId}",
            guildId, textChannelId, voiceChannelId, voiceId ?? "(default)");

        return Task.CompletedTask;
    }

    public Task<GuildTtsConfiguration?> GetConfigurationAsync(ulong guildId)
    {
        _configurations.TryGetValue(guildId, out var config);
        return Task.FromResult(config);
    }

    public Task SetTtsProviderAsync(ulong guildId, string? providerName)
    {
        _configurations.AddOrUpdate(
            guildId,
            _ => new GuildTtsConfiguration
            {
                GuildId = guildId,
                TtsProvider = providerName,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            (_, existing) => existing with
            {
                TtsProvider = providerName,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        _logger.LogInformation("Updated TTS provider for guild {GuildId}: {Provider}",
            guildId, providerName ?? "(default)");

        return Task.CompletedTask;
    }

    public Task UpdateConfigurationAsync(ulong guildId, ulong? voiceChannelId, ulong? textChannelId, string? voiceId, string? providerName)
    {
        _configurations.AddOrUpdate(
            guildId,
            _ => new GuildTtsConfiguration
            {
                GuildId = guildId,
                VoiceChannelId = voiceChannelId,
                TextChannelId = textChannelId,
                VoiceId = voiceId,
                TtsProvider = providerName,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            (_, existing) => existing with
            {
                VoiceChannelId = voiceChannelId ?? existing.VoiceChannelId,
                TextChannelId = textChannelId ?? existing.TextChannelId,
                VoiceId = voiceId ?? existing.VoiceId,
                TtsProvider = providerName ?? existing.TtsProvider,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        _logger.LogInformation(
            "Updated TTS configuration for guild {GuildId}: voiceChannel={VoiceChannel}, textChannel={TextChannel}, voiceId={VoiceId}, provider={Provider}",
            guildId, voiceChannelId, textChannelId, voiceId, providerName);

        return Task.CompletedTask;
    }

    public Task RemoveConfigurationAsync(ulong guildId)
    {
        if (_configurations.TryRemove(guildId, out _))
        {
            _logger.LogInformation("Removed TTS configuration for guild {GuildId}", guildId);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<GuildTtsConfiguration>> GetAllConfigurationsAsync()
    {
        var configs = _configurations.Values.ToList();
        return Task.FromResult<IReadOnlyCollection<GuildTtsConfiguration>>(configs);
    }
}
