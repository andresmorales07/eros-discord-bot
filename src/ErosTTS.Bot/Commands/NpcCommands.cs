using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Exceptions;
using ErosTTS.Bot.Services;
using ErosTTS.Bot.Services.Guild;
using ErosTTS.Bot.Services.Npc;
using ErosTTS.Bot.Services.Queue;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace ErosTTS.Bot.Commands;

/// <summary>
/// Slash commands for NPC management and AI roleplaying.
/// </summary>
public sealed class NpcCommands : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly INpcService _npcService;
    private readonly IGuildConfigurationService _guildConfig;
    private readonly IPromptOrchestrationService _promptService;
    private readonly ITtsQueue _queue;
    private readonly NpcConfiguration _npcConfig;
    private readonly ILogger<NpcCommands> _logger;

    public NpcCommands(
        INpcService npcService,
        IGuildConfigurationService guildConfig,
        IPromptOrchestrationService promptService,
        ITtsQueue queue,
        IOptions<NpcConfiguration> npcConfig,
        ILogger<NpcCommands> logger)
    {
        _npcService = npcService;
        _guildConfig = guildConfig;
        _promptService = promptService;
        _queue = queue;
        _npcConfig = npcConfig.Value;
        _logger = logger;
    }

    private Task RespondEphemeralAsync(string content) =>
        RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
        {
            Content = content,
            Flags = MessageFlags.Ephemeral
        }));

    /// <summary>
    /// Validates the interaction is in a guild. Returns false and sends an ephemeral
    /// error response if not. Callers should return immediately when false is returned.
    /// </summary>
    private bool TryGetGuildId(out ulong guildId)
    {
        var id = Context.Interaction.GuildId;
        if (id is not null)
        {
            guildId = id.Value;
            return true;
        }

        guildId = default;
        return false;
    }

    [SlashCommand("npc-create", "Create a new NPC")]
    public async Task CreateNpcAsync(
        [SlashCommandParameter(Name = "name", Description = "NPC name (unique per guild)", MaxLength = 100)]
        string name,
        [SlashCommandParameter(Name = "personality", Description = "NPC personality/system prompt", MaxLength = 2000)]
        string personality,
        [SlashCommandParameter(Name = "voice-id", Description = "ElevenLabs voice ID override")]
        string? voiceId = null)
    {
        if (!TryGetGuildId(out var guildId))
        {
            await RespondEphemeralAsync("This command can only be used in a server.");
            return;
        }

        try
        {
            var npc = await _npcService.CreateNpcAsync(guildId, name, personality, voiceId);

            var voiceInfo = voiceId is not null ? $"\nVoice ID: `{voiceId}`" : "";
            await RespondEphemeralAsync(
                $"Created NPC **{npc.Name}**.{voiceInfo}\n\n" +
                $"**Personality preview:** {(personality.Length > 200 ? personality[..200] + "..." : personality)}");
        }
        catch (InvalidOperationException ex)
        {
            await RespondEphemeralAsync(ex.Message);
        }
    }

    [SlashCommand("npc-edit", "Edit an existing NPC")]
    public async Task EditNpcAsync(
        [SlashCommandParameter(Name = "name", Description = "NPC name to edit", MaxLength = 100)]
        string name,
        [SlashCommandParameter(Name = "new-name", Description = "New name for the NPC", MaxLength = 100)]
        string? newName = null,
        [SlashCommandParameter(Name = "personality", Description = "New personality/system prompt", MaxLength = 2000)]
        string? personality = null,
        [SlashCommandParameter(Name = "voice-id", Description = "New ElevenLabs voice ID")]
        string? voiceId = null,
        [SlashCommandParameter(Name = "clear-voice", Description = "Clear the voice ID override")]
        bool clearVoice = false)
    {
        if (!TryGetGuildId(out var guildId))
        {
            await RespondEphemeralAsync("This command can only be used in a server.");
            return;
        }

        if (newName is null && personality is null && voiceId is null && !clearVoice)
        {
            await RespondEphemeralAsync("Provide at least one field to update.");
            return;
        }

        try
        {
            var npc = await _npcService.UpdateNpcAsync(guildId, name, newName, personality, voiceId, clearVoice);
            await RespondEphemeralAsync($"Updated NPC **{npc.Name}**.");
        }
        catch (InvalidOperationException ex)
        {
            await RespondEphemeralAsync(ex.Message);
        }
    }

    [SlashCommand("npc-delete", "Delete an NPC and its history")]
    public async Task DeleteNpcAsync(
        [SlashCommandParameter(Name = "name", Description = "NPC name to delete", MaxLength = 100)]
        string name)
    {
        if (!TryGetGuildId(out var guildId))
        {
            await RespondEphemeralAsync("This command can only be used in a server.");
            return;
        }

        var deleted = await _npcService.DeleteNpcAsync(guildId, name);
        await RespondEphemeralAsync(deleted
            ? $"Deleted NPC **{name}** and its conversation history."
            : $"NPC '{name}' not found.");
    }

    [SlashCommand("npc-list", "List all NPCs in this guild")]
    public async Task ListNpcsAsync()
    {
        if (!TryGetGuildId(out var guildId))
        {
            await RespondEphemeralAsync("This command can only be used in a server.");
            return;
        }

        var npcs = await _npcService.ListNpcsAsync(guildId);
        if (npcs.Count == 0)
        {
            await RespondEphemeralAsync("No NPCs created yet. Use `/npc-create` to add one.");
            return;
        }

        var settings = await _npcService.GetSettingsAsync(guildId);

        var lines = npcs.Select(n =>
        {
            var active = settings.ActiveNpcId == n.Id ? " **(active)**" : "";
            var voice = n.VoiceId is not null ? $" | voice: `{n.VoiceId}`" : "";
            var preview = n.Personality.Length > 80 ? n.Personality[..80] + "..." : n.Personality;
            return $"- **{n.Name}**{active}{voice}\n  {preview}";
        });

        await RespondEphemeralAsync($"**NPCs ({npcs.Count}):**\n{string.Join("\n", lines)}");
    }

    [SlashCommand("npc-select", "Set the active NPC")]
    public async Task SelectNpcAsync(
        [SlashCommandParameter(Name = "name", Description = "NPC name to activate", MaxLength = 100)]
        string name)
    {
        if (!TryGetGuildId(out var guildId))
        {
            await RespondEphemeralAsync("This command can only be used in a server.");
            return;
        }

        try
        {
            await _npcService.SetActiveNpcAsync(guildId, name);
            await RespondEphemeralAsync($"Active NPC set to **{name}**.");
        }
        catch (InvalidOperationException ex)
        {
            await RespondEphemeralAsync(ex.Message);
        }
    }

    [SlashCommand("npc-auto-switch", "Toggle automatic NPC selection")]
    public async Task ToggleAutoSwitchAsync()
    {
        if (!TryGetGuildId(out var guildId))
        {
            await RespondEphemeralAsync("This command can only be used in a server.");
            return;
        }

        var settings = await _npcService.GetSettingsAsync(guildId);
        var newValue = !settings.AutoSwitchEnabled;
        await _npcService.SetAutoSwitchAsync(guildId, newValue);
        await RespondEphemeralAsync($"Auto-switch is now **{(newValue ? "enabled" : "disabled")}**.");
    }

    [SlashCommand("npc-history-mode", "Toggle shared or per-NPC conversation history")]
    public async Task SetHistoryModeAsync(
        [SlashCommandParameter(Name = "shared", Description = "true = shared timeline, false = per-NPC isolation")]
        bool shared)
    {
        if (!TryGetGuildId(out var guildId))
        {
            await RespondEphemeralAsync("This command can only be used in a server.");
            return;
        }

        var settings = await _npcService.GetSettingsAsync(guildId);
        if (settings.SharedHistory == shared)
        {
            await RespondEphemeralAsync($"History mode is already **{(shared ? "shared" : "per-NPC")}**.");
            return;
        }

        await _npcService.SetHistoryModeAsync(guildId, shared);
        await RespondEphemeralAsync(
            $"History mode set to **{(shared ? "shared" : "per-NPC")}**. All conversation history has been cleared.");
    }

    [SlashCommand("npc-clear-history", "Clear conversation history")]
    public async Task ClearHistoryAsync(
        [SlashCommandParameter(Name = "name", Description = "NPC name (omit to clear all)", MaxLength = 100)]
        string? name = null)
    {
        if (!TryGetGuildId(out var guildId))
        {
            await RespondEphemeralAsync("This command can only be used in a server.");
            return;
        }

        int? npcId = null;
        if (name is not null)
        {
            var npc = await _npcService.GetNpcAsync(guildId, name);
            if (npc is null)
            {
                await RespondEphemeralAsync($"NPC '{name}' not found.");
                return;
            }
            npcId = npc.Id;
        }

        await _npcService.ClearHistoryAsync(guildId, npcId);
        await RespondEphemeralAsync(name is not null
            ? $"Cleared conversation history for **{name}**."
            : "Cleared all conversation history.");
    }

    [SlashCommand("npc-status", "View NPC settings and active NPC")]
    public async Task StatusAsync()
    {
        if (!TryGetGuildId(out var guildId))
        {
            await RespondEphemeralAsync("This command can only be used in a server.");
            return;
        }

        var settings = await _npcService.GetSettingsAsync(guildId);
        var npcCount = await _npcService.GetNpcCountAsync(guildId);

        string activeNpcName = "None";
        if (settings.ActiveNpcId is not null)
        {
            var activeNpc = await _npcService.GetNpcByIdAsync(settings.ActiveNpcId.Value);
            activeNpcName = activeNpc?.Name ?? "Unknown";
        }

        await RespondEphemeralAsync(
            $"**NPC Status**\n" +
            $"NPCs: {npcCount}/{_npcConfig.MaxNpcsPerGuild}\n" +
            $"Active NPC: {activeNpcName}\n" +
            $"Auto-switch: {(settings.AutoSwitchEnabled ? "Enabled" : "Disabled")}\n" +
            $"History mode: {(settings.SharedHistory ? "Shared" : "Per-NPC")}\n" +
            $"Max history: {_npcConfig.MaxHistoryMessages} messages");
    }

    [SlashCommand("npc-import", "Import NPCs from JSON")]
    public async Task ImportNpcsAsync(
        [SlashCommandParameter(Name = "json", Description = "JSON data to import", MaxLength = 2000)]
        string json)
    {
        if (!TryGetGuildId(out var guildId))
        {
            await RespondEphemeralAsync("This command can only be used in a server.");
            return;
        }

        try
        {
            var result = await _npcService.ImportNpcsAsync(guildId, json);
            var msg = $"Imported **{result.CreatedCount}** NPC(s).";
            if (result.SkippedNames.Count > 0)
                msg += $"\nSkipped (already exist or limit reached): {string.Join(", ", result.SkippedNames)}";
            await RespondEphemeralAsync(msg);
        }
        catch (InvalidOperationException ex)
        {
            await RespondEphemeralAsync($"Import failed: {ex.Message}");
        }
    }

    [SlashCommand("npc-export", "Export all NPCs as JSON")]
    public async Task ExportNpcsAsync()
    {
        if (!TryGetGuildId(out var guildId))
        {
            await RespondEphemeralAsync("This command can only be used in a server.");
            return;
        }

        var json = await _npcService.ExportNpcsAsync(guildId);
        await RespondEphemeralAsync($"```json\n{json}\n```");
    }

    [SlashCommand("prompt", "Send a prompt to the AI character and hear the response")]
    public async Task PromptAsync(
        [SlashCommandParameter(Name = "message", Description = "Your message to the character", MaxLength = 1000)]
        string message)
    {
        if (!TryGetGuildId(out var guildId))
        {
            await RespondEphemeralAsync("This command can only be used in a server.");
            return;
        }

        var config = await _guildConfig.GetConfigurationAsync(guildId);
        if (config?.VoiceChannelId is null || config.VoiceChannelId.Value == 0)
        {
            await RespondEphemeralAsync(
                "No voice channel configured. Please run `/tts-setup` first to configure a default voice channel.");
            return;
        }

        var voiceChannelId = config.VoiceChannelId.Value;

        // Defer since LLM call may take time (visible to all, not ephemeral)
        await RespondAsync(InteractionCallback.DeferredMessage());

        try
        {
            var result = await _promptService.HandlePromptAsync(guildId, voiceChannelId, message);

            await _queue.EnqueueAsync(result.QueueItem);

            _logger.LogInformation(
                "User {UserId} prompted NPC '{NpcName}' in guild {GuildId}, response queued for TTS",
                Context.User.Id, result.NpcName, guildId);

            // Show the conversation (visible to all)
            var displayResponse = result.Response.Length > 1500
                ? result.Response[..1500] + "..."
                : result.Response;

            await FollowupAsync(new InteractionMessageProperties
            {
                Content = $"**{Context.User.Username}:** {message}\n\n**{result.NpcName}:** {displayResponse}"
            });
        }
        catch (InvalidOperationException ex)
        {
            await FollowupAsync(new InteractionMessageProperties
            {
                Content = ex.Message
            });
        }
        catch (LlmServiceException ex)
        {
            _logger.LogError(ex, "LLM service error for guild {GuildId}", guildId);
            await FollowupAsync(new InteractionMessageProperties
            {
                Content = $"Failed to get AI response: {ex.Message}"
            });
        }
    }
}
