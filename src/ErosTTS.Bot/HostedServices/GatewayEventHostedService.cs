using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Services.Guild;
using ErosTTS.Bot.Services.Queue;
using ErosTTS.Bot.Utilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord.Gateway;

namespace ErosTTS.Bot.HostedServices;

/// <summary>
/// Hosted service that wires up gateway event handlers.
/// </summary>
public sealed class GatewayEventHostedService : IHostedService
{
    private readonly GatewayClient _gatewayClient;
    private readonly ITtsQueue _queue;
    private readonly IGuildConfigurationService _guildConfig;
    private readonly BotConfiguration _botConfig;
    private readonly ILogger<GatewayEventHostedService> _logger;

    public GatewayEventHostedService(
        GatewayClient gatewayClient,
        ITtsQueue queue,
        IGuildConfigurationService guildConfig,
        IOptions<BotConfiguration> botConfig,
        ILogger<GatewayEventHostedService> logger)
    {
        _gatewayClient = gatewayClient;
        _queue = queue;
        _guildConfig = guildConfig;
        _botConfig = botConfig.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _gatewayClient.Ready += OnReady;

        if (_botConfig.EnableTextChannelMonitoring)
        {
            _gatewayClient.MessageCreate += OnMessageCreate;
            _logger.LogInformation("Text channel monitoring enabled");
        }
        else
        {
            _logger.LogInformation("Text channel monitoring disabled (slash command mode)");
        }

        _logger.LogInformation("Gateway event handlers registered");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _gatewayClient.Ready -= OnReady;

        if (_botConfig.EnableTextChannelMonitoring)
        {
            _gatewayClient.MessageCreate -= OnMessageCreate;
        }

        _logger.LogInformation("Gateway event handlers unregistered");
        return Task.CompletedTask;
    }

    private ValueTask OnReady(ReadyEventArgs args)
    {
        _logger.LogInformation(
            "Discord client ready. Connected as {Username} ({UserId})",
            args.User.Username,
            args.User.Id);

        return ValueTask.CompletedTask;
    }

    private async ValueTask OnMessageCreate(Message message)
    {
        try
        {
            // Ignore bot messages unless configured to process them
            if (message.Author.IsBot && !_botConfig.ProcessBotMessages)
                return;

            // Only handle guild (server) messages
            if (message.GuildId is not { } guildId)
                return;

            // Check if this channel is monitored
            var config = await _guildConfig.GetConfigurationAsync(guildId);

            if (config?.TextChannelId != message.ChannelId)
                return;

            if (config.VoiceChannelId is null)
            {
                _logger.LogWarning("No voice channel configured for guild {GuildId}", guildId);
                return;
            }

            // Sanitize and validate text
            var text = TextSanitizer.Sanitize(message.Content);
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogDebug("Skipping empty message from {Username} after sanitization",
                    message.Author.Username);
                return;
            }

            // Truncate long messages
            if (text.Length > _botConfig.MaxMessageLength)
            {
                text = text[.._botConfig.MaxMessageLength];
                _logger.LogDebug("Truncated message from {Username} to {Length} chars",
                    message.Author.Username, _botConfig.MaxMessageLength);
            }

            // Create queue item with username prefix
            var queueItem = new TtsQueueItem
            {
                Id = Guid.NewGuid(),
                GuildId = guildId,
                TextChannelId = message.ChannelId,
                VoiceChannelId = config.VoiceChannelId.Value,
                Text = $"{message.Author.Username} says: {text}",
                Username = message.Author.Username
            };

            await _queue.EnqueueAsync(queueItem);

            _logger.LogInformation(
                "Queued message from {Username} in guild {GuildId}: {Preview}",
                message.Author.Username,
                guildId,
                text.Length > 50 ? text[..50] + "..." : text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message from {Username}", message.Author.Username);
        }
    }
}
