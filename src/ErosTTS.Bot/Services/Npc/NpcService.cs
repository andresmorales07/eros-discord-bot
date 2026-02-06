using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErosTTS.Bot.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErosTTS.Bot.Services.Npc;

/// <summary>
/// In-memory implementation of NPC management.
/// </summary>
public sealed class NpcService : INpcService
{
    private readonly ConcurrentDictionary<ulong, GuildState> _guilds = new();
    private readonly NpcConfiguration _config;
    private readonly ILogger<NpcService> _logger;
    private int _nextId;

    public NpcService(
        IOptions<NpcConfiguration> config,
        ILogger<NpcService> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    private GuildState GetOrCreateGuild(ulong guildId) =>
        _guilds.GetOrAdd(guildId, id => new GuildState(id));

    // NPC CRUD

    public Task<NpcDefinition> CreateNpcAsync(ulong guildId, string name, string personality, string? voiceId = null)
    {
        var guild = GetOrCreateGuild(guildId);
        lock (guild)
        {
            if (guild.Npcs.Values.Any(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"An NPC named '{name}' already exists in this guild.");

            if (guild.Npcs.Count >= _config.MaxNpcsPerGuild)
                throw new InvalidOperationException($"Maximum of {_config.MaxNpcsPerGuild} NPCs per guild reached.");

            var id = Interlocked.Increment(ref _nextId);
            var now = DateTimeOffset.UtcNow;
            var npc = new NpcDefinition
            {
                Id = id,
                GuildId = guildId,
                Name = name,
                Personality = personality,
                VoiceId = voiceId,
                CreatedAt = now,
                UpdatedAt = now
            };
            guild.Npcs[id] = npc;

            _logger.LogInformation("Created NPC '{NpcName}' (ID {NpcId}) in guild {GuildId}", name, id, guildId);
            return Task.FromResult(npc);
        }
    }

    public Task<NpcDefinition?> GetNpcAsync(ulong guildId, string name)
    {
        if (!_guilds.TryGetValue(guildId, out var guild))
            return Task.FromResult<NpcDefinition?>(null);

        lock (guild)
        {
            var npc = guild.Npcs.Values.FirstOrDefault(
                n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(npc);
        }
    }

    public Task<NpcDefinition?> GetNpcByIdAsync(int npcId)
    {
        foreach (var guild in _guilds.Values)
        {
            lock (guild)
            {
                if (guild.Npcs.TryGetValue(npcId, out var npc))
                    return Task.FromResult<NpcDefinition?>(npc);
            }
        }
        return Task.FromResult<NpcDefinition?>(null);
    }

    public Task<IReadOnlyList<NpcDefinition>> ListNpcsAsync(ulong guildId)
    {
        if (!_guilds.TryGetValue(guildId, out var guild))
            return Task.FromResult<IReadOnlyList<NpcDefinition>>([]);

        lock (guild)
        {
            return Task.FromResult<IReadOnlyList<NpcDefinition>>(
                guild.Npcs.Values.OrderBy(n => n.Name).ToList());
        }
    }

    public Task<NpcDefinition> UpdateNpcAsync(ulong guildId, string name, string? newName = null, string? personality = null, string? voiceId = null, bool clearVoice = false)
    {
        var guild = GetOrCreateGuild(guildId);
        lock (guild)
        {
            var npc = guild.Npcs.Values.FirstOrDefault(
                n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (npc is null)
                throw new InvalidOperationException($"NPC '{name}' not found.");

            if (newName is not null && !newName.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                guild.Npcs.Values.Any(n => n.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"An NPC named '{newName}' already exists in this guild.");

            var updated = npc with
            {
                Name = newName ?? npc.Name,
                Personality = personality ?? npc.Personality,
                VoiceId = clearVoice ? null : (voiceId ?? npc.VoiceId),
                UpdatedAt = DateTimeOffset.UtcNow
            };
            guild.Npcs[npc.Id] = updated;

            _logger.LogInformation("Updated NPC '{NpcName}' (ID {NpcId}) in guild {GuildId}", updated.Name, npc.Id, guildId);
            return Task.FromResult(updated);
        }
    }

    public Task<bool> DeleteNpcAsync(ulong guildId, string name)
    {
        if (!_guilds.TryGetValue(guildId, out var guild))
            return Task.FromResult(false);

        lock (guild)
        {
            var npc = guild.Npcs.Values.FirstOrDefault(
                n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (npc is null)
                return Task.FromResult(false);

            guild.Npcs.Remove(npc.Id);

            // Clear active NPC if it was the deleted one
            if (guild.Settings.ActiveNpcId == npc.Id)
            {
                guild.Settings = guild.Settings with { ActiveNpcId = null, UpdatedAt = DateTimeOffset.UtcNow };
            }

            // Remove conversation messages for this NPC
            guild.Messages.RemoveAll(m => m.NpcId == npc.Id);

            _logger.LogInformation("Deleted NPC '{NpcName}' (ID {NpcId}) from guild {GuildId}", name, npc.Id, guildId);
            return Task.FromResult(true);
        }
    }

    public Task<int> GetNpcCountAsync(ulong guildId)
    {
        if (!_guilds.TryGetValue(guildId, out var guild))
            return Task.FromResult(0);

        lock (guild)
        {
            return Task.FromResult(guild.Npcs.Count);
        }
    }

    // Guild NPC Settings

    public Task<GuildNpcSettings> GetSettingsAsync(ulong guildId)
    {
        var guild = GetOrCreateGuild(guildId);
        lock (guild)
        {
            return Task.FromResult(guild.Settings);
        }
    }

    public Task SetActiveNpcAsync(ulong guildId, string npcName)
    {
        var guild = GetOrCreateGuild(guildId);
        lock (guild)
        {
            var npc = guild.Npcs.Values.FirstOrDefault(
                n => n.Name.Equals(npcName, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"NPC '{npcName}' not found.");

            guild.Settings = guild.Settings with
            {
                ActiveNpcId = npc.Id,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _logger.LogInformation("Set active NPC to '{NpcName}' in guild {GuildId}", npcName, guildId);
            return Task.CompletedTask;
        }
    }

    public Task SetAutoSwitchAsync(ulong guildId, bool enabled)
    {
        var guild = GetOrCreateGuild(guildId);
        lock (guild)
        {
            guild.Settings = guild.Settings with
            {
                AutoSwitchEnabled = enabled,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            return Task.CompletedTask;
        }
    }

    public Task SetHistoryModeAsync(ulong guildId, bool shared)
    {
        var guild = GetOrCreateGuild(guildId);
        lock (guild)
        {
            if (guild.Settings.SharedHistory != shared)
            {
                guild.Messages.Clear();
                guild.Settings = guild.Settings with
                {
                    SharedHistory = shared,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                _logger.LogInformation("Changed history mode to {Mode} for guild {GuildId}, history cleared",
                    shared ? "shared" : "per-NPC", guildId);
            }
            return Task.CompletedTask;
        }
    }

    // Conversation History

    public Task AddMessageAsync(ulong guildId, int? npcId, string? npcName, string role, string content)
    {
        var guild = GetOrCreateGuild(guildId);
        lock (guild)
        {
            guild.Messages.Add(new NpcConversationMessage
            {
                Role = role,
                Content = content,
                NpcName = npcName,
                NpcId = npcId,
                Timestamp = DateTimeOffset.UtcNow
            });

            // Trim history
            TrimHistory(guild, npcId);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<NpcConversationMessage>> GetHistoryAsync(ulong guildId, int? npcId = null)
    {
        if (!_guilds.TryGetValue(guildId, out var guild))
            return Task.FromResult<IReadOnlyList<NpcConversationMessage>>([]);

        lock (guild)
        {
            var messages = guild.Settings.SharedHistory || npcId is null
                ? guild.Messages.ToList()
                : guild.Messages.Where(m => m.NpcId == npcId).ToList();

            return Task.FromResult<IReadOnlyList<NpcConversationMessage>>(messages);
        }
    }

    public Task ClearHistoryAsync(ulong guildId, int? npcId = null)
    {
        if (!_guilds.TryGetValue(guildId, out var guild))
            return Task.CompletedTask;

        lock (guild)
        {
            if (npcId is null)
                guild.Messages.Clear();
            else
                guild.Messages.RemoveAll(m => m.NpcId == npcId);
        }

        _logger.LogInformation("Cleared history for guild {GuildId}, npcId={NpcId}", guildId, npcId?.ToString() ?? "all");
        return Task.CompletedTask;
    }

    // Import/Export

    public Task<ImportResult> ImportNpcsAsync(ulong guildId, string json)
    {
        var data = JsonSerializer.Deserialize<NpcExportData>(json, JsonOptions)
            ?? throw new InvalidOperationException("Invalid JSON format.");

        if (data.Version != 1)
            throw new InvalidOperationException($"Unsupported import version: {data.Version}");

        if (data.Npcs is null || data.Npcs.Count == 0)
            throw new InvalidOperationException("No NPCs found in import data.");

        var guild = GetOrCreateGuild(guildId);
        var created = 0;
        var skipped = new List<string>();

        lock (guild)
        {
            foreach (var import in data.Npcs)
            {
                if (string.IsNullOrWhiteSpace(import.Name))
                    continue;

                if (guild.Npcs.Values.Any(n => n.Name.Equals(import.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    skipped.Add(import.Name);
                    continue;
                }

                if (guild.Npcs.Count >= _config.MaxNpcsPerGuild)
                {
                    skipped.Add(import.Name);
                    continue;
                }

                var id = Interlocked.Increment(ref _nextId);
                var now = DateTimeOffset.UtcNow;
                guild.Npcs[id] = new NpcDefinition
                {
                    Id = id,
                    GuildId = guildId,
                    Name = import.Name,
                    Personality = import.Personality ?? string.Empty,
                    VoiceId = import.VoiceId,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                created++;
            }
        }

        _logger.LogInformation("Imported {Created} NPCs for guild {GuildId}, skipped {Skipped}",
            created, guildId, skipped.Count);

        return Task.FromResult(new ImportResult(created, skipped));
    }

    public Task<string> ExportNpcsAsync(ulong guildId)
    {
        if (!_guilds.TryGetValue(guildId, out var guild))
            return Task.FromResult(JsonSerializer.Serialize(new NpcExportData { Version = 1, Npcs = [] }, JsonOptions));

        lock (guild)
        {
            var data = new NpcExportData
            {
                Version = 1,
                Npcs = guild.Npcs.Values
                    .OrderBy(n => n.Name)
                    .Select(n => new NpcExportItem
                    {
                        Name = n.Name,
                        Personality = n.Personality,
                        VoiceId = n.VoiceId
                    })
                    .ToList()
            };
            return Task.FromResult(JsonSerializer.Serialize(data, JsonOptions));
        }
    }

    private void TrimHistory(GuildState guild, int? npcId)
    {
        var max = _config.MaxHistoryMessages;
        if (guild.Settings.SharedHistory || npcId is null)
        {
            if (guild.Messages.Count > max)
            {
                guild.Messages.RemoveRange(0, guild.Messages.Count - max);
            }
        }
        else
        {
            var npcMessages = guild.Messages.Where(m => m.NpcId == npcId).ToList();
            if (npcMessages.Count > max)
            {
                var toRemove = npcMessages.Take(npcMessages.Count - max).ToHashSet();
                guild.Messages.RemoveAll(m => toRemove.Contains(m));
            }
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private sealed class GuildState
    {
        public GuildState(ulong guildId)
        {
            Settings = new GuildNpcSettings
            {
                GuildId = guildId,
                ActiveNpcId = null,
                AutoSwitchEnabled = false,
                SharedHistory = false,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }

        public Dictionary<int, NpcDefinition> Npcs { get; } = new();
        public List<NpcConversationMessage> Messages { get; } = [];
        public GuildNpcSettings Settings { get; set; }
    }
}

internal sealed class NpcExportData
{
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("npcs")]
    public List<NpcExportItem> Npcs { get; set; } = [];
}

internal sealed class NpcExportItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("personality")]
    public string? Personality { get; set; }

    [JsonPropertyName("voiceId")]
    public string? VoiceId { get; set; }
}
