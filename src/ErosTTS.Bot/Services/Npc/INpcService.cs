namespace ErosTTS.Bot.Services.Npc;

/// <summary>
/// Service for managing per-guild NPCs, settings, and conversation history.
/// </summary>
public interface INpcService
{
    // NPC CRUD
    Task<NpcDefinition> CreateNpcAsync(ulong guildId, string name, string personality, string? voiceId = null);
    Task<NpcDefinition?> GetNpcAsync(ulong guildId, string name);
    Task<NpcDefinition?> GetNpcByIdAsync(int npcId);
    Task<IReadOnlyList<NpcDefinition>> ListNpcsAsync(ulong guildId);
    Task<NpcDefinition> UpdateNpcAsync(ulong guildId, string name, string? newName = null, string? personality = null, string? voiceId = null, bool clearVoice = false);
    Task<bool> DeleteNpcAsync(ulong guildId, string name);
    Task<int> GetNpcCountAsync(ulong guildId);

    // Guild NPC Settings
    Task<GuildNpcSettings> GetSettingsAsync(ulong guildId);
    Task SetActiveNpcAsync(ulong guildId, string npcName);
    Task SetAutoSwitchAsync(ulong guildId, bool enabled);
    Task SetHistoryModeAsync(ulong guildId, bool shared);

    // Conversation History
    Task AddMessageAsync(ulong guildId, int? npcId, string? npcName, string role, string content);
    Task<IReadOnlyList<NpcConversationMessage>> GetHistoryAsync(ulong guildId, int? npcId = null);
    Task ClearHistoryAsync(ulong guildId, int? npcId = null);

    // Import/Export
    Task<ImportResult> ImportNpcsAsync(ulong guildId, string json);
    Task<string> ExportNpcsAsync(ulong guildId);
}

/// <summary>
/// Result of an NPC import operation.
/// </summary>
public sealed record ImportResult(int CreatedCount, IReadOnlyList<string> SkippedNames);
