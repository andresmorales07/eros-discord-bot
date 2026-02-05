using ErosTTS.Bot.Data;
using ErosTTS.Bot.Data.Converters;
using ErosTTS.Bot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErosTTS.Bot.Services.Guild;

/// <summary>
/// EF Core implementation of guild configuration storage.
/// </summary>
public sealed class EfGuildConfigurationService : IGuildConfigurationService
{
    private readonly IDbContextFactory<ErosTtsDbContext> _factory;
    private readonly ILogger<EfGuildConfigurationService> _logger;

    public EfGuildConfigurationService(
        IDbContextFactory<ErosTtsDbContext> factory,
        ILogger<EfGuildConfigurationService> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task SetChannelsAsync(ulong guildId, ulong textChannelId, ulong voiceChannelId, string? voiceId = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var storedId = DiscordIdConverter.ToLong(guildId);

        var entity = await db.GuildConfigurations.FindAsync(storedId);
        if (entity is null)
        {
            entity = new GuildTtsConfigurationEntity { GuildId = storedId };
            db.GuildConfigurations.Add(entity);
        }

        entity.TextChannelId = DiscordIdConverter.ToLong(textChannelId);
        entity.VoiceChannelId = DiscordIdConverter.ToLong(voiceChannelId);
        entity.VoiceId = voiceId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();

        _logger.LogInformation(
            "Updated TTS configuration for guild {GuildId}: text={TextChannelId}, voice={VoiceChannelId}, voiceId={VoiceId}",
            guildId, textChannelId, voiceChannelId, voiceId ?? "(default)");
    }

    public async Task<GuildTtsConfiguration?> GetConfigurationAsync(ulong guildId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var entity = await db.GuildConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.GuildId == DiscordIdConverter.ToLong(guildId));

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task RemoveConfigurationAsync(ulong guildId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var entity = await db.GuildConfigurations.FindAsync(DiscordIdConverter.ToLong(guildId));
        if (entity is not null)
        {
            db.GuildConfigurations.Remove(entity);
            await db.SaveChangesAsync();
            _logger.LogInformation("Removed TTS configuration for guild {GuildId}", guildId);
        }
    }

    public async Task<IReadOnlyCollection<GuildTtsConfiguration>> GetAllConfigurationsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var entities = await db.GuildConfigurations.AsNoTracking().ToListAsync();
        return entities.Select(MapToDomain).ToList();
    }

    private static GuildTtsConfiguration MapToDomain(GuildTtsConfigurationEntity e) => new()
    {
        GuildId = DiscordIdConverter.ToULong(e.GuildId),
        TextChannelId = e.TextChannelId.HasValue ? DiscordIdConverter.ToULong(e.TextChannelId.Value) : null,
        VoiceChannelId = e.VoiceChannelId.HasValue ? DiscordIdConverter.ToULong(e.VoiceChannelId.Value) : null,
        VoiceId = e.VoiceId,
        UpdatedAt = e.UpdatedAt
    };
}
