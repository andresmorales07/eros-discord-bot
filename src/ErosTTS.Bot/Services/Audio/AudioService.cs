using System.Collections.Concurrent;
using System.Diagnostics;
using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord.Gateway;
using NetCord.Gateway.Voice;

namespace ErosTTS.Bot.Services.Audio;

/// <summary>
/// Audio service for playing audio in Discord voice channels using FFmpeg and NetCord.
/// </summary>
public sealed class AudioService : IAudioService, IAsyncDisposable
{
    private readonly ConcurrentDictionary<ulong, VoiceClient> _voiceClients = new();
    private readonly VoiceConfiguration _config;
    private readonly GatewayClient _gatewayClient;
    private readonly ILogger<AudioService> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    public AudioService(
        GatewayClient gatewayClient,
        IOptions<VoiceConfiguration> config,
        ILogger<AudioService> logger)
    {
        _gatewayClient = gatewayClient;
        _config = config.Value;
        _logger = logger;
    }

    public async Task PlayAudioAsync(
        ulong guildId,
        ulong voiceChannelId,
        Stream audioStream,
        CancellationToken ct = default)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["GuildId"] = guildId,
            ["VoiceChannelId"] = voiceChannelId
        });

        // Get or create voice client connection
        var voiceClient = await GetOrConnectAsync(guildId, voiceChannelId, ct);

        Process? ffmpeg = null;
        try
        {
            // Start FFmpeg process for MP3 to PCM conversion
            ffmpeg = CreateFfmpegProcess();

            if (ffmpeg.StandardInput.BaseStream == null || ffmpeg.StandardOutput.BaseStream == null)
            {
                throw new VoiceConnectionException("Failed to initialize FFmpeg streams");
            }

            _logger.LogDebug("Starting audio playback");

            // Copy audio to FFmpeg input in background
            var inputTask = Task.Run(async () =>
            {
                try
                {
                    await audioStream.CopyToAsync(ffmpeg.StandardInput.BaseStream, ct);
                }
                finally
                {
                    ffmpeg.StandardInput.Close();
                }
            }, ct);

            // Create output stream for sending voice to Discord
            var outStream = voiceClient.CreateOutputStream();

            // Create Opus encode stream wrapping the output stream
            // This converts PCM to Opus format that Discord expects
            await using var opusStream = new OpusEncodeStream(outStream, PcmFormat.Short, VoiceChannels.Stereo, OpusApplication.Audio);

            // Set speaking state
            await voiceClient.EnterSpeakingStateAsync(new SpeakingProperties(SpeakingFlags.Microphone));

            // Stream from FFmpeg output (PCM) to Discord
            await ffmpeg.StandardOutput.BaseStream.CopyToAsync(opusStream, ct);
            await opusStream.FlushAsync(ct);

            // Wait for input copy to complete
            await inputTask;

            _logger.LogDebug("Finished audio playback");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error during audio playback");
            throw new VoiceConnectionException("Audio playback failed", ex);
        }
        finally
        {
            if (ffmpeg != null && !ffmpeg.HasExited)
            {
                try
                {
                    ffmpeg.Kill();
                }
                catch
                {
                    // Ignore kill errors
                }
            }
            ffmpeg?.Dispose();
        }
    }

    private Process CreateFfmpegProcess()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _config.FFmpegPath,
            Arguments = "-hide_banner -loglevel panic -i pipe:0 -ac 2 -f s16le -ar 48000 pipe:1",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = Process.Start(startInfo);

        if (process == null)
        {
            _logger.LogError("Failed to start FFmpeg process at path: {Path}", _config.FFmpegPath);
            throw new VoiceConnectionException($"Failed to start FFmpeg at '{_config.FFmpegPath}'");
        }

        _logger.LogDebug("Started FFmpeg process {ProcessId}", process.Id);
        return process;
    }

    private async Task<VoiceClient> GetOrConnectAsync(
        ulong guildId,
        ulong voiceChannelId,
        CancellationToken ct)
    {
        const int maxRetries = 3;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                // Check for existing valid connection
                if (_voiceClients.TryGetValue(guildId, out var existing))
                {
                    return existing;
                }

                await _connectionLock.WaitAsync(ct);
                try
                {
                    // Double-check after acquiring lock
                    if (_voiceClients.TryGetValue(guildId, out existing))
                    {
                        return existing;
                    }

                    _logger.LogInformation(
                        "Connecting to voice channel {ChannelId} in guild {GuildId} (attempt {Attempt}/{MaxRetries})",
                        voiceChannelId, guildId, attempt, maxRetries);

                    // Use NetCord's voice connection
                    var voiceClient = await _gatewayClient.JoinVoiceChannelAsync(
                        guildId,
                        voiceChannelId);

                    await voiceClient.StartAsync();

                    // Set self-deafen state (bot doesn't need to hear voice chat)
                    await _gatewayClient.UpdateVoiceStateAsync(new VoiceStateProperties(guildId, voiceChannelId)
                    {
                        SelfDeaf = true
                    });

                    _voiceClients[guildId] = voiceClient;

                    _logger.LogInformation("Successfully connected to voice in guild {GuildId}", guildId);
                    return voiceClient;
                }
                finally
                {
                    _connectionLock.Release();
                }
            }
            catch (Exception ex) when (
                ex.Message.Contains("4006") ||
                ex.Message.Contains("Session") ||
                ex.InnerException?.Message.Contains("4006") == true ||
                ex.InnerException?.Message.Contains("Session") == true)
            {
                // Handle 4006 "Session is no longer valid" error
                _logger.LogWarning(
                    "Voice session invalid for guild {GuildId} (attempt {Attempt}/{MaxRetries}): {Message}",
                    guildId, attempt, maxRetries, ex.Message);

                // Clear the stale client
                _voiceClients.TryRemove(guildId, out _);

                if (attempt < maxRetries)
                {
                    // Exponential backoff: 2s, 4s, 8s
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    _logger.LogInformation("Retrying voice connection in {Delay}...", delay);
                    await Task.Delay(delay, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to voice channel {ChannelId} (attempt {Attempt})",
                    voiceChannelId, attempt);

                if (attempt >= maxRetries)
                {
                    throw new VoiceConnectionException($"Failed to connect to voice channel after {maxRetries} attempts", ex);
                }

                // Brief delay before retry for non-4006 errors
                await Task.Delay(1000, ct);
            }
        }

        throw new VoiceConnectionException($"Failed to connect to voice channel after {maxRetries} attempts");
    }

    public async Task DisconnectAsync(ulong guildId)
    {
        if (_voiceClients.TryRemove(guildId, out var client))
        {
            try
            {
                // Send voice state update to Discord to leave the channel
                await _gatewayClient.UpdateVoiceStateAsync(new VoiceStateProperties(guildId, null));

                // Close the local voice client
                await client.CloseAsync();
                _logger.LogInformation("Disconnected from voice in guild {GuildId}", guildId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disconnecting from guild {GuildId}", guildId);
            }
        }
    }

    public bool IsConnected(ulong guildId)
    {
        return _voiceClients.ContainsKey(guildId);
    }

    public IReadOnlyCollection<ulong> GetConnectedGuildIds()
    {
        return _voiceClients.Keys.ToList();
    }

    public async ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing AudioService, disconnecting from all guilds");

        foreach (var (guildId, client) in _voiceClients)
        {
            try
            {
                // Send voice state update to Discord to leave the channel
                await _gatewayClient.UpdateVoiceStateAsync(new VoiceStateProperties(guildId, null));

                await client.CloseAsync();
                _logger.LogDebug("Disconnected from guild {GuildId}", guildId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disconnecting from guild {GuildId} during dispose", guildId);
            }
        }

        _voiceClients.Clear();
        _connectionLock.Dispose();
    }
}
