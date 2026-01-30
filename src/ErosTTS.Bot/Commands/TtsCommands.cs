using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Services.Audio;
using ErosTTS.Bot.Services.Guild;
using ErosTTS.Bot.Services.Queue;
using ErosTTS.Bot.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace ErosTTS.Bot.Commands;

/// <summary>
/// Slash commands for TTS configuration and control.
/// </summary>
public sealed class TtsCommands : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly IGuildConfigurationService _guildConfig;
    private readonly IAudioService _audioService;
    private readonly ITtsQueue _queue;
    private readonly GatewayClient _gatewayClient;
    private readonly BotConfiguration _botConfig;
    private readonly ILogger<TtsCommands> _logger;

    public TtsCommands(
        IGuildConfigurationService guildConfig,
        IAudioService audioService,
        ITtsQueue queue,
        GatewayClient gatewayClient,
        IOptions<BotConfiguration> botConfig,
        ILogger<TtsCommands> logger)
    {
        _guildConfig = guildConfig;
        _audioService = audioService;
        _queue = queue;
        _gatewayClient = gatewayClient;
        _botConfig = botConfig.Value;
        _logger = logger;
    }

    [SlashCommand("say", "Speak text in a voice channel using TTS")]
    public async Task<string> SayAsync(
        [SlashCommandParameter(Name = "text", Description = "The text to speak", MaxLength = 500)]
        string text,
        [SlashCommandParameter(Name = "voice-channel", Description = "Voice channel to speak in (defaults to your current channel)")]
        VoiceGuildChannel? voiceChannel = null)
    {
        var guildId = Context.Interaction.GuildId;
        if (guildId is null)
        {
            return "This command can only be used in a server.";
        }

        // Resolve voice channel
        ulong resolvedVoiceChannelId;
        if (voiceChannel != null)
        {
            resolvedVoiceChannelId = voiceChannel.Id;
        }
        else
        {
            // Try to get user's current voice channel from cache
            var userVoiceChannelId = GetUserVoiceChannel(guildId.Value, Context.User.Id);
            if (userVoiceChannelId.HasValue)
            {
                resolvedVoiceChannelId = userVoiceChannelId.Value;
            }
            else
            {
                // Fall back to configured default
                var config = await _guildConfig.GetConfigurationAsync(guildId.Value);
                if (config?.VoiceChannelId.HasValue == true)
                {
                    resolvedVoiceChannelId = config.VoiceChannelId.Value;
                }
                else
                {
                    return "Please join a voice channel or specify one with the voice-channel parameter.";
                }
            }
        }

        // Sanitize text
        var sanitizedText = TextSanitizer.Sanitize(text);
        if (string.IsNullOrWhiteSpace(sanitizedText))
        {
            return "The text cannot be empty after removing special characters.";
        }

        // Truncate if needed
        if (sanitizedText.Length > _botConfig.MaxMessageLength)
        {
            sanitizedText = sanitizedText[.._botConfig.MaxMessageLength];
        }

        // Create queue item (no "Username says:" prefix for slash commands)
        var queueItem = new TtsQueueItem
        {
            Id = Guid.NewGuid(),
            GuildId = guildId.Value,
            TextChannelId = 0, // Not applicable for slash commands
            VoiceChannelId = resolvedVoiceChannelId,
            Text = sanitizedText,
            Username = Context.User.Username
        };

        await _queue.EnqueueAsync(queueItem);

        _logger.LogInformation(
            "User {UserId} ({Username}) queued TTS via /say in guild {GuildId}: {Preview}",
            Context.User.Id, Context.User.Username, guildId.Value,
            sanitizedText.Length > 50 ? sanitizedText[..50] + "..." : sanitizedText);

        var preview = sanitizedText.Length > 100 ? sanitizedText[..100] + "..." : sanitizedText;
        return $"Queued TTS in <#{resolvedVoiceChannelId}>: \"{preview}\"";
    }

    private ulong? GetUserVoiceChannel(ulong guildId, ulong userId)
    {
        if (_gatewayClient.Cache.Guilds.TryGetValue(guildId, out var guild))
        {
            if (guild.VoiceStates.TryGetValue(userId, out var voiceState))
            {
                return voiceState.ChannelId;
            }
        }
        return null;
    }

    [SlashCommand("tts-setup", "Configure default TTS voice channel for this server")]
    public async Task<string> SetupAsync(
        [SlashCommandParameter(Name = "voice-channel", Description = "Default voice channel for TTS playback")]
        VoiceGuildChannel voiceChannel,
        [SlashCommandParameter(Name = "text-channel", Description = "Channel to monitor for auto-TTS (only works if text monitoring is enabled)")]
        TextGuildChannel? textChannel = null)
    {
        // Check permissions
        var guildId = Context.Interaction.GuildId;
        if (guildId is null)
        {
            return "This command can only be used in a server.";
        }

        var member = Context.User as GuildInteractionUser;
        if (member is null || !member.Permissions.HasFlag(Permissions.ManageGuild))
        {
            return "You need the Manage Server permission to use this command.";
        }

        await _guildConfig.SetChannelsAsync(
            guildId.Value,
            textChannel?.Id ?? 0,
            voiceChannel.Id);

        _logger.LogInformation(
            "User {UserId} ({Username}) configured TTS for guild {GuildId}: text={TextChannel}, voice={VoiceChannel}",
            Context.User.Id, Context.User.Username, guildId.Value, textChannel?.Id ?? 0, voiceChannel.Id);

        var response = $"**TTS Configuration Updated**\nDefault Voice Channel: <#{voiceChannel.Id}>";
        if (textChannel != null)
        {
            response += $"\nText Channel Monitoring: <#{textChannel.Id}>";
            if (!_botConfig.EnableTextChannelMonitoring)
            {
                response += " (note: text monitoring is currently disabled in bot config)";
            }
        }
        return response;
    }

    [SlashCommand("tts-stop", "Stop TTS and disconnect from voice")]
    public async Task<string> StopAsync()
    {
        var guildId = Context.Interaction.GuildId;
        if (guildId is null)
        {
            return "This command can only be used in a server.";
        }

        var wasConnected = _audioService.IsConnected(guildId.Value);

        await _audioService.DisconnectAsync(guildId.Value);

        _logger.LogInformation(
            "User {UserId} ({Username}) stopped TTS for guild {GuildId}",
            Context.User.Id, Context.User.Username, guildId.Value);

        return wasConnected
            ? "Disconnected from voice channel."
            : "Not currently connected to a voice channel.";
    }

    [SlashCommand("tts-status", "Check TTS bot status")]
    public async Task<string> StatusAsync()
    {
        var guildId = Context.Interaction.GuildId;
        if (guildId is null)
        {
            return "This command can only be used in a server.";
        }

        var config = await _guildConfig.GetConfigurationAsync(guildId.Value);
        var isConnected = _audioService.IsConnected(guildId.Value);
        var queueCount = _queue.Count;

        var mode = _botConfig.EnableTextChannelMonitoring
            ? "Slash Commands + Channel Monitoring"
            : "Slash Commands Only";

        if (config == null)
        {
            return $"**TTS Bot Status**\n" +
                   $"Mode: {mode}\n" +
                   $"Configuration: Not set up\n\n" +
                   "Use `/say` to speak text, or `/tts-setup` to configure a default voice channel.";
        }

        var textChannel = config.TextChannelId.HasValue && config.TextChannelId.Value != 0
            ? $"<#{config.TextChannelId}>"
            : "Not set";
        var voiceChannel = config.VoiceChannelId.HasValue ? $"<#{config.VoiceChannelId}>" : "Not set";
        var connectedStatus = isConnected ? "Yes" : "No";

        var response = $"**TTS Bot Status**\n" +
                       $"Mode: {mode}\n" +
                       $"Default Voice Channel: {voiceChannel}\n" +
                       $"Voice Connected: {connectedStatus}\n" +
                       $"Queue Size: {queueCount}";

        if (_botConfig.EnableTextChannelMonitoring)
        {
            response += $"\nText Channel Monitoring: {textChannel}";
        }

        response += $"\nLast Updated: {config.UpdatedAt:g}";

        return response;
    }

    [SlashCommand("tts-clear", "Clear the TTS configuration for this server")]
    public async Task<string> ClearAsync()
    {
        var guildId = Context.Interaction.GuildId;
        if (guildId is null)
        {
            return "This command can only be used in a server.";
        }

        var member = Context.User as GuildInteractionUser;
        if (member is null || !member.Permissions.HasFlag(Permissions.ManageGuild))
        {
            return "You need the Manage Server permission to use this command.";
        }

        await _audioService.DisconnectAsync(guildId.Value);
        await _guildConfig.RemoveConfigurationAsync(guildId.Value);

        _logger.LogInformation(
            "User {UserId} ({Username}) cleared TTS configuration for guild {GuildId}",
            Context.User.Id, Context.User.Username, guildId.Value);

        return "TTS configuration has been cleared for this server.";
    }
}
