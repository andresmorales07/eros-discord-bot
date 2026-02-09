using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Services;
using ErosTTS.Bot.Services.Queue;
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
    private readonly IMessageProcessingService _messageProcessor;
    private readonly BotConfiguration _botConfig;
    private readonly ILogger<GatewayEventHostedService> _logger;

    public GatewayEventHostedService(
        GatewayClient gatewayClient,
        ITtsQueue queue,
        IMessageProcessingService messageProcessor,
        IOptions<BotConfiguration> botConfig,
        ILogger<GatewayEventHostedService> logger)
    {
        _gatewayClient = gatewayClient;
        _queue = queue;
        _messageProcessor = messageProcessor;
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
            // Only handle guild (server) messages
            if (message.GuildId is not { } guildId)
                return;

            var queueItem = await _messageProcessor.ProcessMessageAsync(
                guildId,
                message.ChannelId,
                message.Content,
                message.Author.Username,
                message.Author.IsBot);

            if (queueItem is null)
                return;

            await _queue.EnqueueAsync(queueItem);

            var text = queueItem.Text;
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
