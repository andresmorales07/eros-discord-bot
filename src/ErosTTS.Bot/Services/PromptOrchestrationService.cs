using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Services.Guild;
using ErosTTS.Bot.Services.LLM;
using ErosTTS.Bot.Services.Npc;
using ErosTTS.Bot.Services.Queue;
using ErosTTS.Bot.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErosTTS.Bot.Services;

/// <summary>
/// Result of processing a prompt through the NPC orchestration pipeline.
/// </summary>
public sealed record PromptResult
{
    /// <summary>
    /// The name of the NPC that responded.
    /// </summary>
    public required string NpcName { get; init; }

    /// <summary>
    /// The full LLM response text (for display to users).
    /// </summary>
    public required string Response { get; init; }

    /// <summary>
    /// The TTS queue item created for this response.
    /// </summary>
    public required TtsQueueItem QueueItem { get; init; }
}

/// <summary>
/// Orchestrates the full NPC prompt pipeline: NPC selection, LLM call,
/// history management, and TTS queue item creation.
/// </summary>
public interface IPromptOrchestrationService
{
    /// <summary>
    /// Handles a user prompt by selecting an NPC, getting an LLM response,
    /// storing history, and creating a TTS queue item.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="voiceChannelId">The voice channel to play audio in.</param>
    /// <param name="userMessage">The user's message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The prompt result containing the NPC response and queue item.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no NPCs exist.</exception>
    Task<PromptResult> HandlePromptAsync(
        ulong guildId,
        ulong voiceChannelId,
        string userMessage,
        CancellationToken ct = default);
}

/// <summary>
/// Orchestrates the full NPC prompt pipeline.
/// </summary>
internal sealed class PromptOrchestrationService : IPromptOrchestrationService
{
    private readonly INpcService _npcService;
    private readonly INpcSelectionService _selectionService;
    private readonly ILlmService _llmService;
    private readonly BotConfiguration _botConfig;
    private readonly ILogger<PromptOrchestrationService> _logger;

    public PromptOrchestrationService(
        INpcService npcService,
        INpcSelectionService selectionService,
        ILlmService llmService,
        IOptions<BotConfiguration> botConfig,
        ILogger<PromptOrchestrationService> logger)
    {
        _npcService = npcService;
        _selectionService = selectionService;
        _llmService = llmService;
        _botConfig = botConfig.Value;
        _logger = logger;
    }

    public async Task<PromptResult> HandlePromptAsync(
        ulong guildId,
        ulong voiceChannelId,
        string userMessage,
        CancellationToken ct)
    {
        var settings = await _npcService.GetSettingsAsync(guildId);
        var npcs = await _npcService.ListNpcsAsync(guildId);

        if (npcs.Count == 0)
            throw new InvalidOperationException("No NPCs created yet. Use `/npc-create` to add one.");

        // Determine which NPC responds
        NpcDefinition respondingNpc;
        if (settings.AutoSwitchEnabled && npcs.Count > 1)
        {
            var history = await _npcService.GetHistoryAsync(guildId);
            respondingNpc = await _selectionService.SelectNpcAsync(
                guildId, userMessage, npcs, history, ct);
        }
        else if (settings.ActiveNpcId is not null)
        {
            respondingNpc = npcs.FirstOrDefault(n => n.Id == settings.ActiveNpcId)
                ?? npcs[0];
        }
        else
        {
            respondingNpc = npcs[0];
        }

        // Get history for LLM context
        var npcHistory = await _npcService.GetHistoryAsync(guildId,
            settings.SharedHistory ? null : respondingNpc.Id);

        // Map to LLM conversation messages
        var conversationMessages = npcHistory.Select(m =>
        {
            var content = settings.SharedHistory && m.NpcName is not null && m.Role == "assistant"
                ? $"[{m.NpcName}]: {m.Content}"
                : m.Content;
            return new ConversationMessage { Role = m.Role, Content = content, Timestamp = m.Timestamp };
        }).ToList();

        // Get LLM response
        var response = await _llmService.GetCompletionAsync(
            respondingNpc.Personality, conversationMessages, userMessage, ct);

        // Store messages in history
        await _npcService.AddMessageAsync(guildId, null, null, "user", userMessage);
        await _npcService.AddMessageAsync(guildId, respondingNpc.Id, respondingNpc.Name, "assistant", response);

        // Sanitize and truncate for TTS
        var sanitizedResponse = TextSanitizer.Sanitize(response);
        if (string.IsNullOrWhiteSpace(sanitizedResponse))
            sanitizedResponse = "I have nothing to say.";

        if (sanitizedResponse.Length > _botConfig.MaxMessageLength)
            sanitizedResponse = sanitizedResponse[.._botConfig.MaxMessageLength];

        // Create TTS queue item with NPC's voice
        var queueItem = new TtsQueueItem
        {
            Id = Guid.NewGuid(),
            GuildId = guildId,
            TextChannelId = 0,
            VoiceChannelId = voiceChannelId,
            Text = sanitizedResponse,
            Username = respondingNpc.Name,
            VoiceId = respondingNpc.VoiceId
        };

        _logger.LogInformation(
            "Prompt handled by NPC '{NpcName}' in guild {GuildId}, response queued for TTS",
            respondingNpc.Name, guildId);

        return new PromptResult
        {
            NpcName = respondingNpc.Name,
            Response = response,
            QueueItem = queueItem
        };
    }
}
