using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Services.Guild;
using ErosTTS.Bot.Services.Queue;
using ErosTTS.Bot.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErosTTS.Bot.Services;

/// <summary>
/// Processes incoming text messages into TTS queue items.
/// </summary>
public interface IMessageProcessingService
{
    /// <summary>
    /// Processes a guild message and returns a TTS queue item if it should be spoken.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID where the message was sent.</param>
    /// <param name="content">The raw message content.</param>
    /// <param name="username">The message author's username.</param>
    /// <param name="isBot">Whether the message author is a bot.</param>
    /// <returns>A queue item to enqueue, or null if the message should be skipped.</returns>
    Task<TtsQueueItem?> ProcessMessageAsync(ulong guildId, ulong channelId, string content, string username, bool isBot);
}

/// <summary>
/// Processes incoming text messages into TTS queue items by sanitizing,
/// validating, and resolving guild configuration.
/// </summary>
internal sealed class MessageProcessingService : IMessageProcessingService
{
    private readonly IGuildConfigurationService _guildConfig;
    private readonly BotConfiguration _botConfig;
    private readonly ILogger<MessageProcessingService> _logger;

    public MessageProcessingService(
        IGuildConfigurationService guildConfig,
        IOptions<BotConfiguration> botConfig,
        ILogger<MessageProcessingService> logger)
    {
        _guildConfig = guildConfig;
        _botConfig = botConfig.Value;
        _logger = logger;
    }

    public async Task<TtsQueueItem?> ProcessMessageAsync(
        ulong guildId,
        ulong channelId,
        string content,
        string username,
        bool isBot)
    {
        // Ignore bot messages unless configured to process them
        if (isBot && !_botConfig.ProcessBotMessages)
            return null;

        // Check if this channel is monitored
        var config = await _guildConfig.GetConfigurationAsync(guildId);

        if (config?.TextChannelId != channelId)
            return null;

        if (config.VoiceChannelId is null)
        {
            _logger.LogWarning("No voice channel configured for guild {GuildId}", guildId);
            return null;
        }

        // Sanitize and validate text
        var text = TextSanitizer.Sanitize(content);
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogDebug("Skipping empty message from {Username} after sanitization", username);
            return null;
        }

        // Truncate long messages
        if (text.Length > _botConfig.MaxMessageLength)
        {
            text = text[.._botConfig.MaxMessageLength];
            _logger.LogDebug("Truncated message from {Username} to {Length} chars",
                username, _botConfig.MaxMessageLength);
        }

        return new TtsQueueItem
        {
            Id = Guid.NewGuid(),
            GuildId = guildId,
            TextChannelId = channelId,
            VoiceChannelId = config.VoiceChannelId.Value,
            Text = $"{username} says: {text}",
            Username = username
        };
    }
}
