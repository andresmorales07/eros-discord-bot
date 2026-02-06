using System.Text.Json;
using System.Text.Json.Serialization;
using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Data;
using ErosTTS.Bot.Data.Converters;
using ErosTTS.Bot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErosTTS.Bot.Services.Npc;

/// <summary>
/// EF Core implementation of NPC management.
/// </summary>
public sealed class EfNpcService : INpcService
{
    private readonly IDbContextFactory<ErosTtsDbContext> _factory;
    private readonly NpcConfiguration _config;
    private readonly ILogger<EfNpcService> _logger;

    public EfNpcService(
        IDbContextFactory<ErosTtsDbContext> factory,
        IOptions<NpcConfiguration> config,
        ILogger<EfNpcService> logger)
    {
        _factory = factory;
        _config = config.Value;
        _logger = logger;
    }

    // NPC CRUD

    public async Task<NpcDefinition> CreateNpcAsync(ulong guildId, string name, string personality, string? voiceId = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var storedGuildId = DiscordIdConverter.ToLong(guildId);

        var exists = await db.Npcs.AnyAsync(n =>
            n.GuildId == storedGuildId && EF.Functions.Collate(n.Name, "NOCASE") == name);
        if (exists)
            throw new InvalidOperationException($"An NPC named '{name}' already exists in this guild.");

        var count = await db.Npcs.CountAsync(n => n.GuildId == storedGuildId);
        if (count >= _config.MaxNpcsPerGuild)
            throw new InvalidOperationException($"Maximum of {_config.MaxNpcsPerGuild} NPCs per guild reached.");

        var now = DateTimeOffset.UtcNow;
        var entity = new NpcEntity
        {
            GuildId = storedGuildId,
            Name = name,
            Personality = personality,
            VoiceId = voiceId,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Npcs.Add(entity);
        await db.SaveChangesAsync();

        _logger.LogInformation("Created NPC '{NpcName}' (ID {NpcId}) in guild {GuildId}", name, entity.Id, guildId);
        return MapToDomain(entity, guildId);
    }

    public async Task<NpcDefinition?> GetNpcAsync(ulong guildId, string name)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var storedGuildId = DiscordIdConverter.ToLong(guildId);

        var entity = await db.Npcs
            .AsNoTracking()
            .FirstOrDefaultAsync(n =>
                n.GuildId == storedGuildId && EF.Functions.Collate(n.Name, "NOCASE") == name);

        return entity is null ? null : MapToDomain(entity, guildId);
    }

    public async Task<NpcDefinition?> GetNpcByIdAsync(int npcId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var entity = await db.Npcs.AsNoTracking().FirstOrDefaultAsync(n => n.Id == npcId);
        return entity is null ? null : MapToDomain(entity, DiscordIdConverter.ToULong(entity.GuildId));
    }

    public async Task<IReadOnlyList<NpcDefinition>> ListNpcsAsync(ulong guildId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var storedGuildId = DiscordIdConverter.ToLong(guildId);

        var entities = await db.Npcs
            .AsNoTracking()
            .Where(n => n.GuildId == storedGuildId)
            .OrderBy(n => n.Name)
            .ToListAsync();

        return entities.Select(e => MapToDomain(e, guildId)).ToList();
    }

    public async Task<NpcDefinition> UpdateNpcAsync(ulong guildId, string name, string? newName = null, string? personality = null, string? voiceId = null, bool clearVoice = false)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var storedGuildId = DiscordIdConverter.ToLong(guildId);

        var entity = await db.Npcs.FirstOrDefaultAsync(n =>
            n.GuildId == storedGuildId && EF.Functions.Collate(n.Name, "NOCASE") == name)
            ?? throw new InvalidOperationException($"NPC '{name}' not found.");

        if (newName is not null && !newName.Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            var nameExists = await db.Npcs.AnyAsync(n =>
                n.GuildId == storedGuildId && EF.Functions.Collate(n.Name, "NOCASE") == newName);
            if (nameExists)
                throw new InvalidOperationException($"An NPC named '{newName}' already exists in this guild.");

            entity.Name = newName;
        }

        if (personality is not null)
            entity.Personality = personality;

        if (clearVoice)
            entity.VoiceId = null;
        else if (voiceId is not null)
            entity.VoiceId = voiceId;

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        _logger.LogInformation("Updated NPC '{NpcName}' (ID {NpcId}) in guild {GuildId}", entity.Name, entity.Id, guildId);
        return MapToDomain(entity, guildId);
    }

    public async Task<bool> DeleteNpcAsync(ulong guildId, string name)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var storedGuildId = DiscordIdConverter.ToLong(guildId);

        var entity = await db.Npcs.FirstOrDefaultAsync(n =>
            n.GuildId == storedGuildId && EF.Functions.Collate(n.Name, "NOCASE") == name);

        if (entity is null)
            return false;

        // Clear active NPC reference if it points to this NPC
        var settings = await db.GuildNpcSettings.FirstOrDefaultAsync(s => s.GuildId == storedGuildId);
        if (settings?.ActiveNpcId == entity.Id)
        {
            settings.ActiveNpcId = null;
            settings.UpdatedAt = DateTimeOffset.UtcNow;
        }

        // Delete conversation messages for this NPC
        var messages = await db.NpcConversationMessages
            .Where(m => m.NpcId == entity.Id)
            .ToListAsync();
        db.NpcConversationMessages.RemoveRange(messages);

        db.Npcs.Remove(entity);
        await db.SaveChangesAsync();

        _logger.LogInformation("Deleted NPC '{NpcName}' (ID {NpcId}) from guild {GuildId}", name, entity.Id, guildId);
        return true;
    }

    public async Task<int> GetNpcCountAsync(ulong guildId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Npcs.CountAsync(n => n.GuildId == DiscordIdConverter.ToLong(guildId));
    }

    // Guild NPC Settings

    public async Task<GuildNpcSettings> GetSettingsAsync(ulong guildId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var storedGuildId = DiscordIdConverter.ToLong(guildId);

        var entity = await db.GuildNpcSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.GuildId == storedGuildId);

        if (entity is null)
        {
            return new GuildNpcSettings
            {
                GuildId = guildId,
                ActiveNpcId = null,
                AutoSwitchEnabled = false,
                SharedHistory = false,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }

        return new GuildNpcSettings
        {
            GuildId = guildId,
            ActiveNpcId = entity.ActiveNpcId,
            AutoSwitchEnabled = entity.AutoSwitchEnabled,
            SharedHistory = entity.SharedHistory,
            UpdatedAt = entity.UpdatedAt
        };
    }

    public async Task SetActiveNpcAsync(ulong guildId, string npcName)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var storedGuildId = DiscordIdConverter.ToLong(guildId);

        var npc = await db.Npcs.FirstOrDefaultAsync(n =>
            n.GuildId == storedGuildId && EF.Functions.Collate(n.Name, "NOCASE") == npcName)
            ?? throw new InvalidOperationException($"NPC '{npcName}' not found.");

        var settings = await db.GuildNpcSettings.FirstOrDefaultAsync(s => s.GuildId == storedGuildId);
        if (settings is null)
        {
            settings = new GuildNpcSettingsEntity
            {
                GuildId = storedGuildId,
                ActiveNpcId = npc.Id,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.GuildNpcSettings.Add(settings);
        }
        else
        {
            settings.ActiveNpcId = npc.Id;
            settings.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync();
        _logger.LogInformation("Set active NPC to '{NpcName}' in guild {GuildId}", npcName, guildId);
    }

    public async Task SetAutoSwitchAsync(ulong guildId, bool enabled)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var storedGuildId = DiscordIdConverter.ToLong(guildId);

        var settings = await GetOrCreateSettingsEntity(db, storedGuildId);
        settings.AutoSwitchEnabled = enabled;
        settings.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task SetHistoryModeAsync(ulong guildId, bool shared)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var storedGuildId = DiscordIdConverter.ToLong(guildId);

        var settings = await GetOrCreateSettingsEntity(db, storedGuildId);
        if (settings.SharedHistory != shared)
        {
            // Clear history on mode change
            var messages = await db.NpcConversationMessages
                .Where(m => m.GuildId == storedGuildId)
                .ToListAsync();
            db.NpcConversationMessages.RemoveRange(messages);

            settings.SharedHistory = shared;
            settings.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();

            _logger.LogInformation("Changed history mode to {Mode} for guild {GuildId}, history cleared",
                shared ? "shared" : "per-NPC", guildId);
        }
    }

    // Conversation History

    public async Task AddMessageAsync(ulong guildId, int? npcId, string? npcName, string role, string content)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var storedGuildId = DiscordIdConverter.ToLong(guildId);

        db.NpcConversationMessages.Add(new NpcConversationMessageEntity
        {
            GuildId = storedGuildId,
            NpcId = npcId,
            NpcName = npcName,
            Role = role,
            Content = content,
            Timestamp = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        // Trim history
        var settings = await db.GuildNpcSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.GuildId == storedGuildId);

        var isShared = settings?.SharedHistory ?? false;

        IQueryable<NpcConversationMessageEntity> query;
        if (isShared || npcId is null)
        {
            query = db.NpcConversationMessages.Where(m => m.GuildId == storedGuildId);
        }
        else
        {
            query = db.NpcConversationMessages.Where(m => m.NpcId == npcId);
        }

        var count = await query.CountAsync();
        if (count > _config.MaxHistoryMessages)
        {
            var toRemove = await query
                .OrderBy(m => m.Id)
                .Take(count - _config.MaxHistoryMessages)
                .ToListAsync();
            db.NpcConversationMessages.RemoveRange(toRemove);
            await db.SaveChangesAsync();
        }
    }

    public async Task<IReadOnlyList<NpcConversationMessage>> GetHistoryAsync(ulong guildId, int? npcId = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var storedGuildId = DiscordIdConverter.ToLong(guildId);

        var settings = await db.GuildNpcSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.GuildId == storedGuildId);

        var isShared = settings?.SharedHistory ?? false;

        IQueryable<NpcConversationMessageEntity> query;
        if (isShared || npcId is null)
        {
            query = db.NpcConversationMessages.Where(m => m.GuildId == storedGuildId);
        }
        else
        {
            query = db.NpcConversationMessages.Where(m => m.NpcId == npcId);
        }

        var entities = await query
            .AsNoTracking()
            .OrderBy(m => m.Id)
            .ToListAsync();

        return entities.Select(e => new NpcConversationMessage
        {
            Role = e.Role,
            Content = e.Content,
            NpcName = e.NpcName,
            NpcId = e.NpcId,
            Timestamp = e.Timestamp
        }).ToList();
    }

    public async Task ClearHistoryAsync(ulong guildId, int? npcId = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var storedGuildId = DiscordIdConverter.ToLong(guildId);

        var query = npcId is null
            ? db.NpcConversationMessages.Where(m => m.GuildId == storedGuildId)
            : db.NpcConversationMessages.Where(m => m.NpcId == npcId);

        var messages = await query.ToListAsync();
        db.NpcConversationMessages.RemoveRange(messages);
        await db.SaveChangesAsync();

        _logger.LogInformation("Cleared history for guild {GuildId}, npcId={NpcId}", guildId, npcId?.ToString() ?? "all");
    }

    // Import/Export

    public async Task<ImportResult> ImportNpcsAsync(ulong guildId, string json)
    {
        var data = JsonSerializer.Deserialize<NpcExportData>(json, JsonOptions)
            ?? throw new InvalidOperationException("Invalid JSON format.");

        if (data.Version != 1)
            throw new InvalidOperationException($"Unsupported import version: {data.Version}");

        if (data.Npcs is null || data.Npcs.Count == 0)
            throw new InvalidOperationException("No NPCs found in import data.");

        await using var db = await _factory.CreateDbContextAsync();
        var storedGuildId = DiscordIdConverter.ToLong(guildId);

        var existingNames = await db.Npcs
            .Where(n => n.GuildId == storedGuildId)
            .Select(n => n.Name.ToLower())
            .ToListAsync();

        var currentCount = existingNames.Count;
        var created = 0;
        var skipped = new List<string>();

        foreach (var import in data.Npcs)
        {
            if (string.IsNullOrWhiteSpace(import.Name))
                continue;

            if (existingNames.Contains(import.Name.ToLower()))
            {
                skipped.Add(import.Name);
                continue;
            }

            if (currentCount + created >= _config.MaxNpcsPerGuild)
            {
                skipped.Add(import.Name);
                continue;
            }

            var now = DateTimeOffset.UtcNow;
            db.Npcs.Add(new NpcEntity
            {
                GuildId = storedGuildId,
                Name = import.Name,
                Personality = import.Personality ?? string.Empty,
                VoiceId = import.VoiceId,
                CreatedAt = now,
                UpdatedAt = now
            });
            existingNames.Add(import.Name.ToLower());
            created++;
        }

        await db.SaveChangesAsync();

        _logger.LogInformation("Imported {Created} NPCs for guild {GuildId}, skipped {Skipped}",
            created, guildId, skipped.Count);

        return new ImportResult(created, skipped);
    }

    public async Task<string> ExportNpcsAsync(ulong guildId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var storedGuildId = DiscordIdConverter.ToLong(guildId);

        var entities = await db.Npcs
            .AsNoTracking()
            .Where(n => n.GuildId == storedGuildId)
            .OrderBy(n => n.Name)
            .ToListAsync();

        var data = new NpcExportData
        {
            Version = 1,
            Npcs = entities.Select(e => new NpcExportItem
            {
                Name = e.Name,
                Personality = e.Personality,
                VoiceId = e.VoiceId
            }).ToList()
        };

        return JsonSerializer.Serialize(data, JsonOptions);
    }

    private static async Task<GuildNpcSettingsEntity> GetOrCreateSettingsEntity(ErosTtsDbContext db, long storedGuildId)
    {
        var settings = await db.GuildNpcSettings.FirstOrDefaultAsync(s => s.GuildId == storedGuildId);
        if (settings is null)
        {
            settings = new GuildNpcSettingsEntity
            {
                GuildId = storedGuildId,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.GuildNpcSettings.Add(settings);
        }
        return settings;
    }

    private static NpcDefinition MapToDomain(NpcEntity entity, ulong guildId) => new()
    {
        Id = entity.Id,
        GuildId = guildId,
        Name = entity.Name,
        Personality = entity.Personality,
        VoiceId = entity.VoiceId,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };
}
