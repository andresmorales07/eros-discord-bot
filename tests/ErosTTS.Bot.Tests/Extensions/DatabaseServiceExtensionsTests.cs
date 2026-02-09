using ErosTTS.Bot.Extensions;
using ErosTTS.Bot.Services.Guild;
using ErosTTS.Bot.Services.Npc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ErosTTS.Bot.Tests.Extensions;

public sealed class DatabaseServiceExtensionsTests
{
    private static IConfiguration CreateConfig(string provider)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = provider,
                ["Database:ConnectionString"] = "Data Source=:memory:"
            })
            .Build();

        return config;
    }

    [Fact]
    public void AddPersistence_InMemory_RegistersInMemoryServices()
    {
        var services = new ServiceCollection();
        var config = CreateConfig("InMemory");

        services.AddPersistence(config);

        var guildConfigDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IGuildConfigurationService));
        var npcServiceDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(INpcService));

        guildConfigDescriptor.Should().NotBeNull();
        guildConfigDescriptor!.ImplementationType.Should().Be(typeof(GuildConfigurationService));

        npcServiceDescriptor.Should().NotBeNull();
        npcServiceDescriptor!.ImplementationType.Should().Be(typeof(NpcService));
    }

    [Fact]
    public void AddPersistence_Sqlite_RegistersEfServices()
    {
        var services = new ServiceCollection();
        var config = CreateConfig("Sqlite");

        services.AddPersistence(config);

        var guildConfigDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IGuildConfigurationService));
        var npcServiceDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(INpcService));

        guildConfigDescriptor.Should().NotBeNull();
        guildConfigDescriptor!.ImplementationType.Should().Be(typeof(EfGuildConfigurationService));

        npcServiceDescriptor.Should().NotBeNull();
        npcServiceDescriptor!.ImplementationType.Should().Be(typeof(EfNpcService));
    }

    [Fact]
    public void AddPersistence_Postgres_Throws()
    {
        var services = new ServiceCollection();
        var config = CreateConfig("Postgres");

        var act = () => services.AddPersistence(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*PostgreSQL*");
    }

    [Fact]
    public void AddPersistence_PostgreSql_Throws()
    {
        var services = new ServiceCollection();
        var config = CreateConfig("PostgreSql");

        var act = () => services.AddPersistence(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*PostgreSQL*");
    }

    [Fact]
    public void AddPersistence_CaseInsensitive_SqliteWorks()
    {
        var services = new ServiceCollection();
        var config = CreateConfig("SQLITE");

        services.AddPersistence(config);

        var guildConfigDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IGuildConfigurationService));
        guildConfigDescriptor.Should().NotBeNull();
        guildConfigDescriptor!.ImplementationType.Should().Be(typeof(EfGuildConfigurationService));
    }

    [Fact]
    public void AddPersistence_UnknownProvider_DefaultsToInMemory()
    {
        var services = new ServiceCollection();
        var config = CreateConfig("Unknown");

        services.AddPersistence(config);

        var guildConfigDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IGuildConfigurationService));
        guildConfigDescriptor.Should().NotBeNull();
        guildConfigDescriptor!.ImplementationType.Should().Be(typeof(GuildConfigurationService));
    }

    [Fact]
    public void AddPersistence_NoDatabaseSection_DefaultsToInMemory()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        services.AddPersistence(config);

        var guildConfigDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IGuildConfigurationService));
        guildConfigDescriptor.Should().NotBeNull();
        guildConfigDescriptor!.ImplementationType.Should().Be(typeof(GuildConfigurationService));
    }
}
