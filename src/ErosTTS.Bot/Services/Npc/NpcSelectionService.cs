using System.Text;
using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Services.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErosTTS.Bot.Services.Npc;

/// <summary>
/// Uses an LLM to automatically select which NPC should respond to a user message.
/// </summary>
public sealed class NpcSelectionService : INpcSelectionService
{
    private readonly ILlmService _llmService;
    private readonly NpcConfiguration _config;
    private readonly ILogger<NpcSelectionService> _logger;

    public NpcSelectionService(
        ILlmService llmService,
        IOptions<NpcConfiguration> config,
        ILogger<NpcSelectionService> logger)
    {
        _llmService = llmService;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<NpcDefinition> SelectNpcAsync(
        ulong guildId,
        string userMessage,
        IReadOnlyList<NpcDefinition> availableNpcs,
        IReadOnlyList<NpcConversationMessage> recentHistory,
        CancellationToken ct = default)
    {
        if (availableNpcs.Count == 0)
            throw new InvalidOperationException("No NPCs available for selection.");

        if (availableNpcs.Count == 1)
            return availableNpcs[0];

        var systemPrompt = BuildSelectionPrompt(availableNpcs);
        var contextMessages = recentHistory
            .TakeLast(_config.AutoSwitchContextMessages)
            .Select(m => new ConversationMessage
            {
                Role = m.Role,
                Content = m.NpcName is not null && m.Role == "assistant"
                    ? $"[{m.NpcName}]: {m.Content}"
                    : m.Content,
                Timestamp = m.Timestamp
            })
            .ToList();

        try
        {
            var response = await _llmService.GetCompletionAsync(
                systemPrompt, contextMessages, userMessage, ct);

            var selected = ParseNpcSelection(response, availableNpcs);
            if (selected is not null)
            {
                _logger.LogInformation("Auto-switch selected NPC '{NpcName}' for guild {GuildId}", selected.Name, guildId);
                return selected;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auto-switch NPC selection failed for guild {GuildId}, falling back", guildId);
        }

        // Fallback: return first NPC
        _logger.LogDebug("Auto-switch falling back to first NPC for guild {GuildId}", guildId);
        return availableNpcs[0];
    }

    private static string BuildSelectionPrompt(IReadOnlyList<NpcDefinition> npcs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an NPC selector for a roleplaying game. Given a user message and available NPCs, respond with ONLY the name of the most appropriate NPC to respond.");
        sb.AppendLine();
        sb.AppendLine("Available NPCs:");
        foreach (var npc in npcs)
        {
            var personalitySummary = npc.Personality.Length > 100
                ? npc.Personality[..100] + "..."
                : npc.Personality;
            sb.AppendLine($"- {npc.Name}: {personalitySummary}");
        }
        sb.AppendLine();
        sb.AppendLine("Respond with ONLY the NPC name, nothing else.");
        return sb.ToString();
    }

    private static NpcDefinition? ParseNpcSelection(string response, IReadOnlyList<NpcDefinition> npcs)
    {
        var trimmed = response.Trim();

        // Try exact match first (case-insensitive)
        var exact = npcs.FirstOrDefault(n => n.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact;

        // Try contains match (response may include extra text)
        var contains = npcs.FirstOrDefault(n =>
            trimmed.Contains(n.Name, StringComparison.OrdinalIgnoreCase));
        return contains;
    }
}
