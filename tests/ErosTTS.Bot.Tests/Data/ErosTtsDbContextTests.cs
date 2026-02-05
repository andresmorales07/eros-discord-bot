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
    public async Task CascadeDelete_RemovesConversationMessages_WhenCharacterStateDeleted()
    {
        using var db = Factory.CreateDbContext();
        var state = new GuildCharacterStateEntity
        {
            GuildId = 12345L,
            Context = "Test",
            UpdatedAt = DateTimeOffset.UtcNow,
            ConversationHistory =
            [
                new() { GuildId = 12345L, Role = "user", Content = "Hello", Timestamp = DateTimeOffset.UtcNow },
                new() { GuildId = 12345L, Role = "assistant", Content = "Hi", Timestamp = DateTimeOffset.UtcNow }
            ]
        };
        db.GuildCharacterStates.Add(state);
        await db.SaveChangesAsync();

        // Verify messages exist
        (await db.ConversationMessages.CountAsync()).Should().Be(2);

        // Delete the parent
        db.GuildCharacterStates.Remove(state);
        await db.SaveChangesAsync();

        // Verify cascade delete removed messages
        using var db2 = Factory.CreateDbContext();
        (await db2.ConversationMessages.CountAsync()).Should().Be(0);
        (await db2.GuildCharacterStates.CountAsync()).Should().Be(0);
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
    public async Task ConversationMessages_OrderedById()
    {
        using var db = Factory.CreateDbContext();
        var state = new GuildCharacterStateEntity
        {
            GuildId = 12345L,
            Context = "Test",
            UpdatedAt = DateTimeOffset.UtcNow,
            ConversationHistory =
            [
                new() { GuildId = 12345L, Role = "user", Content = "First", Timestamp = DateTimeOffset.UtcNow },
                new() { GuildId = 12345L, Role = "assistant", Content = "Second", Timestamp = DateTimeOffset.UtcNow },
                new() { GuildId = 12345L, Role = "user", Content = "Third", Timestamp = DateTimeOffset.UtcNow }
            ]
        };
        db.GuildCharacterStates.Add(state);
        await db.SaveChangesAsync();

        using var db2 = Factory.CreateDbContext();
        var messages = await db2.ConversationMessages
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
