namespace ErosTTS.Bot.Services.Npc;

/// <summary>
/// Service for automatically selecting which NPC should respond to a user message.
/// </summary>
public interface INpcSelectionService
{
    /// <summary>
    /// Selects the most appropriate NPC to respond based on the user message and conversation context.
    /// </summary>
    Task<NpcDefinition> SelectNpcAsync(
        ulong guildId,
        string userMessage,
        IReadOnlyList<NpcDefinition> availableNpcs,
        IReadOnlyList<NpcConversationMessage> recentHistory,
        CancellationToken ct = default);
}
