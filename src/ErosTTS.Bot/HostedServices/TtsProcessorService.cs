using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Exceptions;
using ErosTTS.Bot.Services.Audio;
using ErosTTS.Bot.Services.Guild;
using ErosTTS.Bot.Services.Queue;
using ErosTTS.Bot.Services.TTS;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord.Gateway;

namespace ErosTTS.Bot.HostedServices;

/// <summary>
/// Background service that processes TTS queue items.
/// </summary>
public sealed class TtsProcessorService : BackgroundService
{
    private readonly ITtsQueue _queue;
    private readonly ITtsProviderFactory _providerFactory;
    private readonly IAudioService _audioService;
    private readonly IGuildConfigurationService _guildConfig;
    private readonly GatewayClient _gatewayClient;
    private readonly ElevenLabsConfiguration _config;
    private readonly ILogger<TtsProcessorService> _logger;

    public TtsProcessorService(
        ITtsQueue queue,
        ITtsProviderFactory providerFactory,
        IAudioService audioService,
        IGuildConfigurationService guildConfig,
        GatewayClient gatewayClient,
        IOptions<ElevenLabsConfiguration> config,
        ILogger<TtsProcessorService> logger)
    {
        _queue = queue;
        _providerFactory = providerFactory;
        _audioService = audioService;
        _guildConfig = guildConfig;
        _gatewayClient = gatewayClient;
        _config = config.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TTS Processor Service starting");

        // Wait for Discord client to be READY
        // This ensures the gateway has fully initialized and we can join voice channels
        var readyTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        ValueTask OnReady(ReadyEventArgs args)
        {
            _logger.LogInformation("Discord client Ready event received");
            readyTcs.TrySetResult();
            return ValueTask.CompletedTask;
        }

        _gatewayClient.Ready += OnReady;

        try
        {
            // Check if already ready (client may have fired Ready before we subscribed)
            if (_gatewayClient.Cache.Guilds.Count > 0)
            {
                _logger.LogInformation("Discord client already ready with {GuildCount} guilds", _gatewayClient.Cache.Guilds.Count);
                readyTcs.TrySetResult();
            }

            // Wait for Ready event or timeout after 30 seconds
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

            try
            {
                await readyTcs.Task.WaitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("Timeout waiting for Discord Ready event, proceeding anyway");
            }
        }
        finally
        {
            _gatewayClient.Ready -= OnReady;
        }

        _logger.LogInformation("TTS Processor Service started and ready to process");

        await foreach (var item in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessItemAsync(item, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("TTS Processor Service stopping due to cancellation");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing TTS item {ItemId}", item.Id);
                await HandleFailureAsync(item, ex, stoppingToken);
            }
        }

        _logger.LogInformation("TTS Processor Service stopped");
    }

    internal async Task ProcessItemAsync(TtsQueueItem item, CancellationToken ct)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["ItemId"] = item.Id,
            ["GuildId"] = item.GuildId,
            ["Username"] = item.Username
        });

        _logger.LogDebug("Processing TTS: '{Text}'",
            item.Text.Length > 50 ? item.Text[..50] + "..." : item.Text);

        // Get configuration for voice channel
        var config = await _guildConfig.GetConfigurationAsync(item.GuildId);
        if (config is null || config.VoiceChannelId is null)
        {
            _logger.LogWarning("No voice channel configured for guild {GuildId}", item.GuildId);
            return;
        }

        // Resolve TTS provider for this guild
        var provider = await _providerFactory.GetProviderAsync(item.GuildId);

        // Generate TTS audio — NPC voice overrides guild voice
        Stream? audioStream = null;
        try
        {
            var voiceId = item.VoiceId ?? config.VoiceId;
            _logger.LogDebug("Generating TTS audio for {CharCount} characters using voice {VoiceId} via {Provider}",
                item.Text.Length, voiceId ?? "(default)", provider.ProviderName);
            audioStream = await provider.SynthesizeAsync(item.Text, voiceId, ct);

            // Play the audio
            _logger.LogDebug("Playing audio in voice channel {ChannelId}", item.VoiceChannelId);
            await _audioService.PlayAudioAsync(item.GuildId, item.VoiceChannelId, audioStream, ct);

            _logger.LogInformation(
                "Successfully played TTS for {Username} in guild {GuildId}",
                item.Username, item.GuildId);
        }
        finally
        {
            if (audioStream != null)
            {
                await audioStream.DisposeAsync();
            }
        }
    }

    internal async Task HandleFailureAsync(TtsQueueItem item, Exception ex, CancellationToken ct)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["ItemId"] = item.Id,
            ["GuildId"] = item.GuildId,
            ["RetryCount"] = item.RetryCount
        });

        if (ex is RateLimitException rle && item.RetryCount < _config.MaxRetries)
        {
            _logger.LogWarning(
                "Rate limited for item {ItemId}, retry {RetryCount}/{MaxRetries} after {Delay}",
                item.Id, item.RetryCount + 1, _config.MaxRetries, rle.RetryAfter);

            // Add jitter to the retry delay
            var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));
            await Task.Delay(rle.RetryAfter + jitter, ct);

            item.RetryCount++;
            await _queue.EnqueueAsync(item, ct);
        }
        else if (ex is AuthenticationException)
        {
            _logger.LogCritical("TTS API authentication failed. Check your API key.");
            // Don't retry auth failures
        }
        else if (ex is InvalidTextException ite)
        {
            _logger.LogWarning("Invalid text for TTS: {Error}", ite.Message);
            // Don't retry invalid text
        }
        else if (ex is VoiceConnectionException vce)
        {
            _logger.LogWarning("Voice connection error: {Error}", vce.Message);

            // Retry voice connection errors once
            if (item.RetryCount < 1)
            {
                _logger.LogInformation("Retrying item {ItemId} after voice connection error", item.Id);
                await Task.Delay(2000, ct);
                item.RetryCount++;
                await _queue.EnqueueAsync(item, ct);
            }
        }
        else if (item.RetryCount >= _config.MaxRetries)
        {
            _logger.LogError("Max retries ({MaxRetries}) exceeded for item {ItemId}",
                _config.MaxRetries, item.Id);
        }
        else
        {
            // Generic retry with exponential backoff
            var delay = TimeSpan.FromSeconds(Math.Pow(2, item.RetryCount + 1));
            _logger.LogWarning(
                "Retrying item {ItemId}, attempt {RetryCount}/{MaxRetries} after {Delay}",
                item.Id, item.RetryCount + 1, _config.MaxRetries, delay);

            await Task.Delay(delay, ct);
            item.RetryCount++;
            await _queue.EnqueueAsync(item, ct);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("TTS Processor Service stopping");
        _queue.Complete();
        await base.StopAsync(cancellationToken);
    }
}
