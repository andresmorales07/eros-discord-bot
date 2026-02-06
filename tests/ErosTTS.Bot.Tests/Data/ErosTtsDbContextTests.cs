using ErosTTS.Bot.Data;
using ErosTTS.Bot.Data.Converters;
using ErosTTS.Bot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ErosTTS.Bot.Tests.Data;

public class ErosTtsDbContextTests : EfTestBase
{
    [Fact]
    public void SchemaCreation_Succeeds()
    {
        using var db = Factory.CreateDbContext();
        db.Database.CanConnect().Should().BeTrue();
    }

    [Fact]
    public async Task GuildConfiguration_CanBeInsertedAndRetrieved()
    {
        using var db = Factory.CreateDbContext();
        db.GuildConfigurations.Add(new GuildTtsConfigurationEntity
        {
            GuildId = 12345L,
            TextChannelId = 111L,
            VoiceChannelId = 222L,
            VoiceId = "test-voice",
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        using var db2 = Factory.CreateDbContext();
        var entity = await db2.GuildConfigurations.FindAsync(12345L);
        entity.Should().NotBeNull();
        entity!.VoiceId.Should().Be("test-voice");
    }

    [Fact]
    public async Task NpcEntity_CanBeInsertedAndRetrieved()
    {
        using var db = Factory.CreateDbContext();
        var npc = new NpcEntity
        {
            GuildId = 12345L,
            Name = "Gandalf",
            Personality = "A wise wizard",
            VoiceId = "voice123",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Npcs.Add(npc);
        await db.SaveChangesAsync();

        using var db2 = Factory.CreateDbContext();
        var retrieved = await db2.Npcs.FirstOrDefaultAsync(n => n.GuildId == 12345L);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Gandalf");
        retrieved.Personality.Should().Be("A wise wizard");
        retrieved.VoiceId.Should().Be("voice123");
    }

    [Fact]
    public async Task NpcEntity_GuildIdNameUnique()
    {
        using var db = Factory.CreateDbContext();
        db.Npcs.Add(new NpcEntity
        {
            GuildId = 12345L, Name = "Gandalf", Personality = "Wizard",
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        using var db2 = Factory.CreateDbContext();
        db2.Npcs.Add(new NpcEntity
        {
            GuildId = 12345L, Name = "Gandalf", Personality = "Another wizard",
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        });

        var act = async () => await db2.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task NpcDelete_SetNullOnConversationMessages()
    {
        using var db = Factory.CreateDbContext();
        var npc = new NpcEntity
        {
            GuildId = 12345L, Name = "Gandalf", Personality = "Wizard",
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Npcs.Add(npc);
        await db.SaveChangesAsync();

        db.NpcConversationMessages.Add(new NpcConversationMessageEntity
        {
            GuildId = 12345L, NpcId = npc.Id, NpcName = "Gandalf",
            Role = "user", Content = "Hello", Timestamp = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        db.Npcs.Remove(npc);
        await db.SaveChangesAsync();

        using var db2 = Factory.CreateDbContext();
        var messages = await db2.NpcConversationMessages.ToListAsync();
        messages.Should().HaveCount(1);
        messages[0].NpcId.Should().BeNull();
    }

    [Fact]
    public async Task GuildNpcSettings_ActiveNpcSetNull_OnNpcDelete()
    {
        using var db = Factory.CreateDbContext();
        var npc = new NpcEntity
        {
            GuildId = 12345L, Name = "Gandalf", Personality = "Wizard",
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Npcs.Add(npc);
        await db.SaveChangesAsync();

        db.GuildNpcSettings.Add(new GuildNpcSettingsEntity
        {
            GuildId = 12345L, ActiveNpcId = npc.Id, UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        db.Npcs.Remove(npc);
        await db.SaveChangesAsync();

        using var db2 = Factory.CreateDbContext();
        var settings = await db2.GuildNpcSettings.FindAsync(12345L);
        settings.Should().NotBeNull();
        settings!.ActiveNpcId.Should().BeNull();
    }

    [Fact]
    public void DiscordIdConverter_RoundTrip_PreservesAllBits()
    {
        // Test with max ulong value
        var maxId = ulong.MaxValue;
        var stored = DiscordIdConverter.ToLong(maxId);
        var restored = DiscordIdConverter.ToULong(stored);
        restored.Should().Be(maxId);

        // Test with typical Discord snowflake
        var typicalId = 1234567890123456789UL;
        stored = DiscordIdConverter.ToLong(typicalId);
        restored = DiscordIdConverter.ToULong(stored);
        restored.Should().Be(typicalId);

        // Test with zero
        stored = DiscordIdConverter.ToLong(0UL);
        restored = DiscordIdConverter.ToULong(stored);
        restored.Should().Be(0UL);
    }

    [Fact]
    public async Task NpcConversationMessages_OrderedById()
    {
        using var db = Factory.CreateDbContext();
        var npc = new NpcEntity
        {
            GuildId = 12345L, Name = "Gandalf", Personality = "Wizard",
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Npcs.Add(npc);
        await db.SaveChangesAsync();

        db.NpcConversationMessages.AddRange(
            new NpcConversationMessageEntity { GuildId = 12345L, NpcId = npc.Id, NpcName = "Gandalf", Role = "user", Content = "First", Timestamp = DateTimeOffset.UtcNow },
            new NpcConversationMessageEntity { GuildId = 12345L, NpcId = npc.Id, NpcName = "Gandalf", Role = "assistant", Content = "Second", Timestamp = DateTimeOffset.UtcNow },
            new NpcConversationMessageEntity { GuildId = 12345L, NpcId = npc.Id, NpcName = "Gandalf", Role = "user", Content = "Third", Timestamp = DateTimeOffset.UtcNow }
        );
        await db.SaveChangesAsync();

        using var db2 = Factory.CreateDbContext();
        var messages = await db2.NpcConversationMessages
            .Where(m => m.GuildId == 12345L)
            .OrderBy(m => m.Id)
            .ToListAsync();

        messages.Should().HaveCount(3);
        messages[0].Content.Should().Be("First");
        messages[1].Content.Should().Be("Second");
        messages[2].Content.Should().Be("Third");
    }

    [Fact]
    public async Task GuildConfiguration_GuildIdIsUnique()
    {
        using var db = Factory.CreateDbContext();
        db.GuildConfigurations.Add(new GuildTtsConfigurationEntity
        {
            GuildId = 12345L,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        using var db2 = Factory.CreateDbContext();
        db2.GuildConfigurations.Add(new GuildTtsConfigurationEntity
        {
            GuildId = 12345L,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var act = async () => await db2.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
