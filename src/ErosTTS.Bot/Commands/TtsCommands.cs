using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Services;
using ErosTTS.Bot.Services.Audio;
using ErosTTS.Bot.Services.Guild;
using ErosTTS.Bot.Services.Queue;
using ErosTTS.Bot.Services.TTS;
using ErosTTS.Bot.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord;
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
    private readonly IVoiceChannelResolverService _voiceResolver;
    private readonly ITtsProviderFactory _providerFactory;
    private readonly BotConfiguration _botConfig;
    private readonly ILogger<TtsCommands> _logger;

    public TtsCommands(
        IGuildConfigurationService guildConfig,
        IAudioService audioService,
        ITtsQueue queue,
        IVoiceChannelResolverService voiceResolver,
        ITtsProviderFactory providerFactory,
        IOptions<BotConfiguration> botConfig,
        ILogger<TtsCommands> logger)
    {
        _guildConfig = guildConfig;
        _audioService = audioService;
        _queue = queue;
        _voiceResolver = voiceResolver;
        _providerFactory = providerFactory;
        _botConfig = botConfig.Value;
        _logger = logger;
    }

    private Task RespondEphemeralAsync(string content) =>
        RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
        {
            Content = content,
            Flags = MessageFlags.Ephemeral
        }));

    [SlashCommand("say", "Speak text in a voice channel using TTS")]
    public async Task SayAsync(
        [SlashCommandParameter(Name = "text", Description = "The text to speak", MaxLength = 500)]
        string text,
        [SlashCommandParameter(Name = "voice-channel", Description = "Voice channel to speak in (defaults to your current channel)")]
        VoiceGuildChannel? voiceChannel = null)
    {
        var guildId = Context.Interaction.GuildId;
        if (guildId is null)
        {
            await RespondEphemeralAsync("This command can only be used in a server.");
            return;
        }

        // Resolve voice channel via three-step fallback
        var resolvedChannelId = await _voiceResolver.ResolveVoiceChannelAsync(
            guildId.Value,
            Context.User.Id,
            voiceChannel?.Id);

        if (!resolvedChannelId.HasValue)
        {
            await RespondEphemeralAsync("Please join a voice channel or specify one with the voice-channel parameter.");
            return;
        }

        var resolvedVoiceChannelId = resolvedChannelId.Value;

        // Sanitize text
        var sanitizedText = TextSanitizer.Sanitize(text);
        if (string.IsNullOrWhiteSpace(sanitizedText))
        {
            await RespondEphemeralAsync("The text cannot be empty after removing special characters.");
            return;
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
        await RespondEphemeralAsync($"Queued TTS in <#{resolvedVoiceChannelId}>: \"{preview}\"");
    }

    [SlashCommand("tts-config", "Configure TTS settings for this server (provide at least one option)")]
    public async Task ConfigAsync(
        [SlashCommandParameter(Name = "voice-channel", Description = "Default voice channel for TTS playback")]
        VoiceGuildChannel? voiceChannel = null,
        [SlashCommandParameter(Name = "text-channel", Description = "Channel to monitor for auto-TTS (only works if text monitoring is enabled)")]
        TextGuildChannel? textChannel = null,
        [SlashCommandParameter(Name = "voice-id", Description = "TTS voice ID (leave empty for default)")]
        string? voiceId = null,
        [SlashCommandParameter(Name = "provider", Description = "TTS provider name (e.g. ElevenLabs, OpenAI)")]
        string? provider = null)
    {
        var guildId = Context.Interaction.GuildId;
        if (guildId is null)
        {
            await RespondEphemeralAsync("This command can only be used in a server.");
            return;
        }

        if (voiceChannel is null && textChannel is null && voiceId is null && provider is null)
        {
            await RespondEphemeralAsync("Please provide at least one option to update.");
            return;
        }

        var member = Context.User as GuildInteractionUser;
        if (member is null || !member.Permissions.HasFlag(Permissions.ManageGuild))
        {
            await RespondEphemeralAsync("You need the Manage Server permission to use this command.");
            return;
        }

        // Validate provider if specified
        if (provider is not null)
        {
            var resolved = _providerFactory.GetProviderByName(provider);
            if (resolved is null)
            {
                var available = string.Join(", ", _providerFactory.GetAvailableProviders());
                await RespondEphemeralAsync($"Unknown provider `{provider}`. Available providers: {available}");
                return;
            }
            provider = resolved.ProviderName;
        }

        await _guildConfig.UpdateConfigurationAsync(
            guildId.Value,
            voiceChannel?.Id,
            textChannel?.Id,
            voiceId,
            provider);

        _logger.LogInformation(
            "User {UserId} ({Username}) updated TTS config for guild {GuildId}: voiceChannel={VoiceChannel}, textChannel={TextChannel}, voiceId={VoiceId}, provider={Provider}",
            Context.User.Id, Context.User.Username, guildId.Value, voiceChannel?.Id, textChannel?.Id, voiceId, provider);

        var response = "**TTS Configuration Updated**";
        if (voiceChannel is not null)
            response += $"\nVoice Channel: <#{voiceChannel.Id}>";
        if (textChannel is not null)
        {
            response += $"\nText Channel: <#{textChannel.Id}>";
            if (!_botConfig.EnableTextChannelMonitoring)
                response += " (note: text monitoring is currently disabled in bot config)";
        }
        if (voiceId is not null)
            response += $"\nVoice ID: `{voiceId}`";
        if (provider is not null)
            response += $"\nTTS Provider: **{provider}**";

        await RespondEphemeralAsync(response);
    }

    [SlashCommand("tts-stop", "Stop TTS and disconnect from voice")]
    public async Task StopAsync()
    {
        var guildId = Context.Interaction.GuildId;
        if (guildId is null)
        {
            await RespondEphemeralAsync("This command can only be used in a server.");
            return;
        }

        var wasConnected = _audioService.IsConnected(guildId.Value);

        await _audioService.DisconnectAsync(guildId.Value);

        _logger.LogInformation(
            "User {UserId} ({Username}) stopped TTS for guild {GuildId}",
            Context.User.Id, Context.User.Username, guildId.Value);

        await RespondEphemeralAsync(wasConnected
            ? "Disconnected from voice channel."
            : "Not currently connected to a voice channel.");
    }

    [SlashCommand("tts-status", "Check TTS bot status")]
    public async Task StatusAsync()
    {
        var guildId = Context.Interaction.GuildId;
        if (guildId is null)
        {
            await RespondEphemeralAsync("This command can only be used in a server.");
            return;
        }

        var config = await _guildConfig.GetConfigurationAsync(guildId.Value);
        var isConnected = _audioService.IsConnected(guildId.Value);
        var queueCount = _queue.Count;

        var mode = _botConfig.EnableTextChannelMonitoring
            ? "Slash Commands + Channel Monitoring"
            : "Slash Commands Only";

        if (config == null)
        {
            await RespondEphemeralAsync($"**TTS Bot Status**\n" +
                   $"Mode: {mode}\n" +
                   $"Configuration: Not set up\n\n" +
                   "Use `/say` to speak text, or `/tts-config` to configure TTS settings.");
            return;
        }

        var voiceChannel = config.VoiceChannelId.HasValue ? $"<#{config.VoiceChannelId}>" : "Not set";
        var textChannel = config.TextChannelId.HasValue && config.TextChannelId.Value != 0
            ? $"<#{config.TextChannelId}>"
            : "Not set";
        var voiceIdDisplay = config.VoiceId ?? "Default";
        var providerDisplay = config.TtsProvider ?? "ElevenLabs";
        var connectedStatus = isConnected ? "Yes" : "No";

        var monitoringNote = _botConfig.EnableTextChannelMonitoring ? "" : " (monitoring disabled)";

        var response = $"**TTS Bot Status**\n" +
                       $"Mode: {mode}\n" +
                       $"TTS Provider: **{providerDisplay}**\n" +
                       $"Default Voice Channel: {voiceChannel}\n" +
                       $"Text Channel: {textChannel}{monitoringNote}\n" +
                       $"Voice ID: `{voiceIdDisplay}`\n" +
                       $"Voice Connected: {connectedStatus}\n" +
                       $"Queue Size: {queueCount}\n" +
                       $"Last Updated: {config.UpdatedAt:g}";

        await RespondEphemeralAsync(response);
    }

    [SlashCommand("tts-clear", "Clear the TTS configuration for this server")]
    public async Task ClearAsync()
    {
        var guildId = Context.Interaction.GuildId;
        if (guildId is null)
        {
            await RespondEphemeralAsync("This command can only be used in a server.");
            return;
        }

        var member = Context.User as GuildInteractionUser;
        if (member is null || !member.Permissions.HasFlag(Permissions.ManageGuild))
        {
            await RespondEphemeralAsync("You need the Manage Server permission to use this command.");
            return;
        }

        await _audioService.DisconnectAsync(guildId.Value);
        await _guildConfig.RemoveConfigurationAsync(guildId.Value);

        _logger.LogInformation(
            "User {UserId} ({Username}) cleared TTS configuration for guild {GuildId}",
            Context.User.Id, Context.User.Username, guildId.Value);

        await RespondEphemeralAsync("TTS configuration has been cleared for this server.");
    }
}
