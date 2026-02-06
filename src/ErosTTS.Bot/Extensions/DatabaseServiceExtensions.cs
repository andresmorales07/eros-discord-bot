using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Data;
using ErosTTS.Bot.Services.Guild;
using ErosTTS.Bot.Services.Npc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ErosTTS.Bot.Extensions;

/// <summary>
/// Extension methods for registering persistence services.
/// </summary>
public static class DatabaseServiceExtensions
{
    /// <summary>
    /// Registers guild configuration and NPC services based on the configured database provider.
    /// </summary>
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbConfig = configuration.GetSection(DatabaseConfiguration.SectionName)
            .Get<DatabaseConfiguration>() ?? new DatabaseConfiguration();

        switch (dbConfig.Provider.ToLowerInvariant())
        {
            case "sqlite":
                services.AddDbContextFactory<ErosTtsDbContext>(options =>
                    options.UseSqlite(dbConfig.ConnectionString));
                services.AddSingleton<IGuildConfigurationService, EfGuildConfigurationService>();
                services.AddSingleton<INpcService, EfNpcService>();
                break;

            case "postgres":
            case "postgresql":
                // PostgreSQL support requires adding Npgsql.EntityFrameworkCore.PostgreSQL package
                throw new InvalidOperationException(
                    "PostgreSQL provider requires the Npgsql.EntityFrameworkCore.PostgreSQL package. " +
                    "Add it to the project and update this switch case.");

            case "inmemory":
            default:
                services.AddSingleton<IGuildConfigurationService, GuildConfigurationService>();
                services.AddSingleton<INpcService, NpcService>();
                break;
        }

        return services;
    }
}
