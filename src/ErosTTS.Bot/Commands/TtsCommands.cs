using ErosTTS.Bot.Services.Audio;
using ErosTTS.Bot.Services.Guild;
using ErosTTS.Bot.Services.Queue;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<TtsCommands> _logger;

    public TtsCommands(
        IGuildConfigurationService guildConfig,
        IAudioService audioService,
        ITtsQueue queue,
        ILogger<TtsCommands> logger)
    {
        _guildConfig = guildConfig;
        _audioService = audioService;
        _queue = queue;
        _logger = logger;
    }

    [SlashCommand("tts-setup", "Configure TTS channels for this server")]
    public async Task<string> SetupAsync(
        [SlashCommandParameter(Name = "text-channel", Description = "Channel to monitor for messages")]
        TextGuildChannel textChannel,
        [SlashCommandParameter(Name = "voice-channel", Description = "Channel to play TTS audio")]
        VoiceGuildChannel voiceChannel)
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
            textChannel.Id,
            voiceChannel.Id);

        _logger.LogInformation(
            "User {UserId} ({Username}) configured TTS for guild {GuildId}: text={TextChannel}, voice={VoiceChannel}",
            Context.User.Id, Context.User.Username, guildId.Value, textChannel.Id, voiceChannel.Id);

        return $"**TTS Configuration Updated**\nMonitoring: <#{textChannel.Id}>\nPlaying in: <#{voiceChannel.Id}>";
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

        if (config == null)
        {
            return "TTS is not configured for this server.\nUse `/tts-setup` to configure the text and voice channels.";
        }

        var textChannel = config.TextChannelId.HasValue ? $"<#{config.TextChannelId}>" : "Not set";
        var voiceChannel = config.VoiceChannelId.HasValue ? $"<#{config.VoiceChannelId}>" : "Not set";
        var connectedStatus = isConnected ? "Yes" : "No";

        return $"**TTS Bot Status**\n" +
               $"Text Channel: {textChannel}\n" +
               $"Voice Channel: {voiceChannel}\n" +
               $"Voice Connected: {connectedStatus}\n" +
               $"Queue Size: {queueCount}\n" +
               $"Last Updated: {config.UpdatedAt:g}";
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
