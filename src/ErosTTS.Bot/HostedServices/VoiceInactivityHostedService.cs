using System.Collections.Concurrent;
using ErosTTS.Bot.Services.Audio;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;

namespace ErosTTS.Bot.HostedServices;

/// <summary>
/// Hosted service that monitors voice channels and automatically disconnects
/// the bot when it is alone in a voice channel for a configurable duration.
/// </summary>
internal sealed class VoiceInactivityHostedService : IHostedService, IDisposable
{
    internal static readonly TimeSpan DefaultDisconnectDelay = TimeSpan.FromMinutes(1);

    private readonly GatewayClient? _gatewayClient;
    private readonly IVoiceChannelInspector _inspector;
    private readonly ILogger<VoiceInactivityHostedService> _logger;
    private readonly TimeSpan _disconnectDelay;
    private readonly ConcurrentDictionary<ulong, CancellationTokenSource> _timers = new();

    public VoiceInactivityHostedService(
        GatewayClient gatewayClient,
        IVoiceChannelInspector inspector,
        ILogger<VoiceInactivityHostedService> logger)
        : this(inspector, logger, gatewayClient, DefaultDisconnectDelay)
    {
    }

    /// <summary>
    /// Internal constructor for testing — allows injecting a custom delay and omitting GatewayClient.
    /// </summary>
    internal VoiceInactivityHostedService(
        IVoiceChannelInspector inspector,
        ILogger<VoiceInactivityHostedService> logger,
        GatewayClient? gatewayClient = null,
        TimeSpan? disconnectDelay = null)
    {
        _gatewayClient = gatewayClient;
        _inspector = inspector;
        _logger = logger;
        _disconnectDelay = disconnectDelay ?? DefaultDisconnectDelay;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_gatewayClient is not null)
            _gatewayClient.VoiceStateUpdate += OnVoiceStateUpdate;
        _logger.LogInformation("Voice inactivity monitor started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_gatewayClient is not null)
            _gatewayClient.VoiceStateUpdate -= OnVoiceStateUpdate;
        CancelAllTimers();
        _logger.LogInformation("Voice inactivity monitor stopped");
        return Task.CompletedTask;
    }

    private ValueTask OnVoiceStateUpdate(VoiceState voiceState)
    {
        HandleVoiceStateChange(voiceState.GuildId);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Core logic for handling a voice state change in a guild.
    /// Exposed as internal for testability without requiring a real VoiceState object.
    /// </summary>
    internal void HandleVoiceStateChange(ulong guildId)
    {
        try
        {
            if (!_inspector.IsBotConnected(guildId))
            {
                // Bot isn't connected in this guild — cancel any stale timer and skip
                CancelTimer(guildId);
                return;
            }

            var botChannelId = _inspector.GetBotVoiceChannelId(guildId);
            if (botChannelId is null)
            {
                // Can't determine bot's channel (cache not ready) — skip
                return;
            }

            var userCount = _inspector.CountNonBotUsersInChannel(guildId, botChannelId.Value);

            if (userCount == 0)
            {
                StartTimer(guildId);
            }
            else
            {
                CancelTimer(guildId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling voice state update for guild {GuildId}", guildId);
        }
    }

    internal void StartTimer(ulong guildId)
    {
        // Only start a new timer if one isn't already running
        var cts = new CancellationTokenSource();
        if (!_timers.TryAdd(guildId, cts))
        {
            cts.Dispose();
            return;
        }

        _logger.LogInformation(
            "Voice channel empty in guild {GuildId}, scheduling disconnect in {Delay}",
            guildId, _disconnectDelay);

        _ = RunTimerAsync(guildId, cts.Token);
    }

    private async Task RunTimerAsync(ulong guildId, CancellationToken ct)
    {
        try
        {
            await Task.Delay(_disconnectDelay, ct);

            // Re-verify: channel might have been refilled during the delay
            var botChannelId = _inspector.GetBotVoiceChannelId(guildId);
            if (botChannelId is null || !_inspector.IsBotConnected(guildId))
            {
                _logger.LogDebug(
                    "Bot no longer connected in guild {GuildId}, skipping disconnect", guildId);
                return;
            }

            var userCount = _inspector.CountNonBotUsersInChannel(guildId, botChannelId.Value);
            if (userCount > 0)
            {
                _logger.LogDebug(
                    "Voice channel in guild {GuildId} is no longer empty, skipping disconnect", guildId);
                return;
            }

            _logger.LogInformation(
                "Disconnecting from empty voice channel in guild {GuildId}", guildId);
            await _inspector.DisconnectBotAsync(guildId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Disconnect timer cancelled for guild {GuildId}", guildId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in disconnect timer for guild {GuildId}", guildId);
        }
        finally
        {
            if (_timers.TryRemove(guildId, out var cts))
            {
                cts.Dispose();
            }
        }
    }

    internal bool HasPendingTimer(ulong guildId) => _timers.ContainsKey(guildId);

    internal void CancelTimer(ulong guildId)
    {
        if (_timers.TryRemove(guildId, out var cts))
        {
            _logger.LogDebug("Disconnect timer cancelled for guild {GuildId}", guildId);
            cts.Cancel();
            cts.Dispose();
        }
    }

    private void CancelAllTimers()
    {
        foreach (var guildId in _timers.Keys.ToList())
        {
            CancelTimer(guildId);
        }
    }

    public void Dispose()
    {
        CancelAllTimers();
    }
}
