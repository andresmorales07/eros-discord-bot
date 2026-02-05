using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Data;
using ErosTTS.Bot.Data.Converters;
using ErosTTS.Bot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErosTTS.Bot.Services.Character;

/// <summary>
/// EF Core implementation of character state storage.
/// </summary>
public sealed class EfCharacterStateService : ICharacterStateService
{
    private readonly IDbContextFactory<ErosTtsDbContext> _factory;
    private readonly OpenRouterConfiguration _config;
    private readonly ILogger<EfCharacterStateService> _logger;

    public EfCharacterStateService(
        IDbContextFactory<ErosTtsDbContext> factory,
        IOptions<OpenRouterConfiguration> config,
        ILogger<EfCharacterStateService> logger)
    {
        _factory = factory;
        _config = config.Value;
        _logger = logger;
    }

    public async Task SetContextAsync(ulong guildId, string context, bool append = false)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var storedId = DiscordIdConverter.ToLong(guildId);
        var entity = await db.GuildCharacterStates.FindAsync(storedId);

        if (entity is null)
        {
            entity = new GuildCharacterStateEntity
            {
                GuildId = storedId,
                Context = context,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.GuildCharacterStates.Add(entity);
        }
        else
        {
            entity.Context = append ? $"{entity.Context}\n{context}".Trim() : context;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync();

        _logger.LogInformation(
            "Updated character context for guild {GuildId}, append={Append}",
            guildId, append);
    }

    public async Task<GuildCharacterState?> GetStateAsync(ulong guildId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var entity = await db.GuildCharacterStates
            .Include(e => e.ConversationHistory.OrderBy(m => m.Id))
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.GuildId == DiscordIdConverter.ToLong(guildId));

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task AddMessageAsync(ulong guildId, string role, string content)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var storedId = DiscordIdConverter.ToLong(guildId);

        var entity = await db.GuildCharacterStates
            .Include(e => e.ConversationHistory)
            .FirstOrDefaultAsync(e => e.GuildId == storedId);

        if (entity is null)
        {
            entity = new GuildCharacterStateEntity
            {
                GuildId = storedId,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.GuildCharacterStates.Add(entity);
        }

        entity.ConversationHistory.Add(new ConversationMessageEntity
        {
            GuildId = storedId,
            Role = role,
            Content = content,
            Timestamp = DateTimeOffset.UtcNow
        });
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        // Save first so new messages get their IDs assigned
        await db.SaveChangesAsync();

        // Trim history beyond max (requires a second save if trimming occurred)
        var maxHistory = _config.MaxHistoryMessages;
        if (entity.ConversationHistory.Count > maxHistory)
        {
            var toRemove = entity.ConversationHistory
                .OrderBy(m => m.Id)
                .Take(entity.ConversationHistory.Count - maxHistory)
                .ToList();
            db.ConversationMessages.RemoveRange(toRemove);
            await db.SaveChangesAsync();
        }
    }

    public async Task ClearStateAsync(ulong guildId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var entity = await db.GuildCharacterStates
            .Include(e => e.ConversationHistory)
            .FirstOrDefaultAsync(e => e.GuildId == DiscordIdConverter.ToLong(guildId));

        if (entity is not null)
        {
            db.GuildCharacterStates.Remove(entity);
            await db.SaveChangesAsync();
            _logger.LogInformation("Cleared character state for guild {GuildId}", guildId);
        }
    }

    private static GuildCharacterState MapToDomain(GuildCharacterStateEntity e) => new()
    {
        GuildId = DiscordIdConverter.ToULong(e.GuildId),
        Context = e.Context,
        ConversationHistory = e.ConversationHistory
            .Select(m => new ConversationMessage
            {
                Role = m.Role,
                Content = m.Content,
                Timestamp = m.Timestamp
            })
            .ToList(),
        UpdatedAt = e.UpdatedAt
    };
}
