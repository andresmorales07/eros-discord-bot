using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Exceptions;
using ErosTTS.Bot.Services.Character;
using ErosTTS.Bot.Services.Guild;
using ErosTTS.Bot.Services.LLM;
using ErosTTS.Bot.Services.Queue;
using ErosTTS.Bot.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace ErosTTS.Bot.Commands;

/// <summary>
/// Slash commands for character/AI roleplay functionality.
/// </summary>
public sealed class CharacterCommands : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly ICharacterStateService _characterState;
    private readonly IGuildConfigurationService _guildConfig;
    private readonly ILlmService _llmService;
    private readonly ITtsQueue _queue;
    private readonly BotConfiguration _botConfig;
    private readonly ILogger<CharacterCommands> _logger;

    public CharacterCommands(
        ICharacterStateService characterState,
        IGuildConfigurationService guildConfig,
        ILlmService llmService,
        ITtsQueue queue,
        IOptions<BotConfiguration> botConfig,
        ILogger<CharacterCommands> logger)
    {
        _characterState = characterState;
        _guildConfig = guildConfig;
        _llmService = llmService;
        _queue = queue;
        _botConfig = botConfig.Value;
        _logger = logger;
    }

    private Task RespondEphemeralAsync(string content) =>
        RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
        {
            Content = content,
            Flags = MessageFlags.Ephemeral
        }));

    [SlashCommand("character-context", "Set or append character context for the AI")]
    public async Task SetContextAsync(
        [SlashCommandParameter(Name = "context", Description = "Character context/system prompt", MaxLength = 2000)]
        string context,
        [SlashCommandParameter(Name = "append", Description = "Append to existing context instead of replacing")]
        bool append = false)
    {
        var guildId = Context.Interaction.GuildId;
        if (guildId is null)
        {
            await RespondEphemeralAsync("This command can only be used in a server.");
            return;
        }

        await _characterState.SetContextAsync(guildId.Value, context, append);

        var action = append ? "appended to" : "set";
        _logger.LogInformation(
            "User {UserId} ({Username}) {Action} character context for guild {GuildId}",
            Context.User.Id, Context.User.Username, action, guildId.Value);

        var state = await _characterState.GetStateAsync(guildId.Value);
        var preview = state?.Context.Length > 100
            ? state.Context[..100] + "..."
            : state?.Context ?? "";

        await RespondEphemeralAsync($"Character context {action}.\n\n**Current context:**\n{preview}");
    }

    [SlashCommand("prompt", "Send a prompt to the AI character and hear the response")]
    public async Task PromptAsync(
        [SlashCommandParameter(Name = "message", Description = "Your message to the character", MaxLength = 1000)]
        string message)
    {
        var guildId = Context.Interaction.GuildId;
        if (guildId is null)
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Content = "This command can only be used in a server.",
                Flags = MessageFlags.Ephemeral
            }));
            return;
        }

        // Get guild's configured voice channel
        var config = await _guildConfig.GetConfigurationAsync(guildId.Value);
        if (config?.VoiceChannelId is null || config.VoiceChannelId.Value == 0)
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Content = "No voice channel configured. Please run `/tts-setup` first to configure a default voice channel.",
                Flags = MessageFlags.Ephemeral
            }));
            return;
        }

        var voiceChannelId = config.VoiceChannelId.Value;

        // Defer response since LLM call may take time (NOT ephemeral - visible to all)
        await RespondAsync(InteractionCallback.DeferredMessage());

        try
        {
            // Get character state
            var state = await _characterState.GetStateAsync(guildId.Value);
            var systemPrompt = state?.Context ?? "";
            var history = state?.ConversationHistory ?? [];

            // Get LLM response
            var response = await _llmService.GetCompletionAsync(
                systemPrompt, history, message);

            // Add user message and assistant response to history
            await _characterState.AddMessageAsync(guildId.Value, "user", message);
            await _characterState.AddMessageAsync(guildId.Value, "assistant", response);

            // Sanitize and truncate for TTS
            var sanitizedResponse = TextSanitizer.Sanitize(response);
            if (string.IsNullOrWhiteSpace(sanitizedResponse))
            {
                sanitizedResponse = "I have nothing to say.";
            }

            if (sanitizedResponse.Length > _botConfig.MaxMessageLength)
            {
                sanitizedResponse = sanitizedResponse[.._botConfig.MaxMessageLength];
            }

            // Queue TTS
            var queueItem = new TtsQueueItem
            {
                Id = Guid.NewGuid(),
                GuildId = guildId.Value,
                TextChannelId = 0,
                VoiceChannelId = voiceChannelId,
                Text = sanitizedResponse,
                Username = "AI Character"
            };

            await _queue.EnqueueAsync(queueItem);

            _logger.LogInformation(
                "User {UserId} prompted AI in guild {GuildId}, response queued for TTS",
                Context.User.Id, guildId.Value);

            // Show the conversation (visible to all)
            var displayResponse = response.Length > 1500
                ? response[..1500] + "..."
                : response;

            await FollowupAsync(new InteractionMessageProperties
            {
                Content = $"**{Context.User.Username}:** {message}\n\n**Character:** {displayResponse}"
            });
        }
        catch (LlmServiceException ex)
        {
            _logger.LogError(ex, "LLM service error for guild {GuildId}", guildId.Value);
            await FollowupAsync(new InteractionMessageProperties
            {
                Content = $"Failed to get AI response: {ex.Message}"
            });
        }
    }

    [SlashCommand("character-clear", "Clear character context and conversation history")]
    public async Task ClearAsync()
    {
        var guildId = Context.Interaction.GuildId;
        if (guildId is null)
        {
            await RespondEphemeralAsync("This command can only be used in a server.");
            return;
        }

        await _characterState.ClearStateAsync(guildId.Value);

        _logger.LogInformation(
            "User {UserId} ({Username}) cleared character state for guild {GuildId}",
            Context.User.Id, Context.User.Username, guildId.Value);

        await RespondEphemeralAsync("Character context and conversation history have been cleared.");
    }

    [SlashCommand("character-status", "View current character context and history size")]
    public async Task StatusAsync()
    {
        var guildId = Context.Interaction.GuildId;
        if (guildId is null)
        {
            await RespondEphemeralAsync("This command can only be used in a server.");
            return;
        }

        var state = await _characterState.GetStateAsync(guildId.Value);

        if (state == null)
        {
            await RespondEphemeralAsync("**Character Status**\nNo character context set. Use `/character-context` to set one.");
            return;
        }

        var contextPreview = state.Context.Length > 200
            ? state.Context[..200] + "..."
            : state.Context;

        await RespondEphemeralAsync($"**Character Status**\n" +
               $"Context Length: {state.Context.Length} characters\n" +
               $"Conversation History: {state.ConversationHistory.Count} messages\n" +
               $"Last Updated: {state.UpdatedAt:g}\n\n" +
               $"**Context Preview:**\n{contextPreview}");
    }
}
