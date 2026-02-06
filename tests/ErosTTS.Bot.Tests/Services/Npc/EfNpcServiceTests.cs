using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Services.Npc;
using ErosTTS.Bot.Tests.Data;

namespace ErosTTS.Bot.Tests.Services.Npc;

public class EfNpcServiceTests : EfTestBase
{
    private readonly EfNpcService _sut;

    public EfNpcServiceTests()
    {
        var logger = Substitute.For<ILogger<EfNpcService>>();
        var config = Options.Create(new NpcConfiguration
        {
            MaxNpcsPerGuild = 5,
            MaxHistoryMessages = 5,
            AutoSwitchContextMessages = 3
        });
        _sut = new EfNpcService(Factory, config, logger);
    }

    // --- NPC CRUD ---

    [Fact]
    public async Task CreateNpcAsync_CreatesNpc()
    {
        var npc = await _sut.CreateNpcAsync(1UL, "Gandalf", "You are a wizard.", "voice123");

        npc.Name.Should().Be("Gandalf");
        npc.Personality.Should().Be("You are a wizard.");
        npc.VoiceId.Should().Be("voice123");
        npc.GuildId.Should().Be(1UL);
        npc.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateNpcAsync_DuplicateName_Throws()
    {
        await _sut.CreateNpcAsync(1UL, "Gandalf", "Wizard");

        var act = async () => await _sut.CreateNpcAsync(1UL, "gandalf", "Different wizard");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task CreateNpcAsync_ExceedsMaxPerGuild_Throws()
    {
        for (int i = 0; i < 5; i++)
            await _sut.CreateNpcAsync(1UL, $"NPC{i}", "Personality");

        var act = async () => await _sut.CreateNpcAsync(1UL, "NPC5", "Personality");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Maximum*");
    }

    [Fact]
    public async Task GetNpcAsync_ReturnsNullWhenNotFound()
    {
        var result = await _sut.GetNpcAsync(1UL, "Unknown");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetNpcAsync_FindsByNameCaseInsensitive()
    {
        await _sut.CreateNpcAsync(1UL, "Gandalf", "Wizard");

        var result = await _sut.GetNpcAsync(1UL, "GANDALF");

        result.Should().NotBeNull();
        result!.Name.Should().Be("Gandalf");
    }

    [Fact]
    public async Task GetNpcByIdAsync_ReturnsNpc()
    {
        var created = await _sut.CreateNpcAsync(1UL, "Gandalf", "Wizard");

        var result = await _sut.GetNpcByIdAsync(created.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Gandalf");
    }

    [Fact]
    public async Task GetNpcByIdAsync_ReturnsNullWhenNotFound()
    {
        var result = await _sut.GetNpcByIdAsync(999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ListNpcsAsync_ReturnsAllSorted()
    {
        await _sut.CreateNpcAsync(1UL, "Zoe", "Z");
        await _sut.CreateNpcAsync(1UL, "Alice", "A");
        await _sut.CreateNpcAsync(1UL, "Mia", "M");

        var result = await _sut.ListNpcsAsync(1UL);

        result.Should().HaveCount(3);
        result[0].Name.Should().Be("Alice");
        result[1].Name.Should().Be("Mia");
        result[2].Name.Should().Be("Zoe");
    }

    [Fact]
    public async Task ListNpcsAsync_EmptyGuild_ReturnsEmpty()
    {
        var result = await _sut.ListNpcsAsync(999UL);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateNpcAsync_UpdatesPersonality()
    {
        await _sut.CreateNpcAsync(1UL, "Gandalf", "Old personality");

        var updated = await _sut.UpdateNpcAsync(1UL, "Gandalf", personality: "New personality");

        updated.Personality.Should().Be("New personality");
        updated.Name.Should().Be("Gandalf");
    }

    [Fact]
    public async Task UpdateNpcAsync_RenamesNpc()
    {
        await _sut.CreateNpcAsync(1UL, "Gandalf", "Wizard");

        var updated = await _sut.UpdateNpcAsync(1UL, "Gandalf", newName: "Saruman");

        updated.Name.Should().Be("Saruman");
        (await _sut.GetNpcAsync(1UL, "Gandalf")).Should().BeNull();
        (await _sut.GetNpcAsync(1UL, "Saruman")).Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateNpcAsync_DuplicateNewName_Throws()
    {
        await _sut.CreateNpcAsync(1UL, "Gandalf", "Wizard");
        await _sut.CreateNpcAsync(1UL, "Saruman", "Evil wizard");

        var act = async () => await _sut.UpdateNpcAsync(1UL, "Gandalf", newName: "Saruman");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task UpdateNpcAsync_NotFound_Throws()
    {
        var act = async () => await _sut.UpdateNpcAsync(1UL, "Unknown");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task UpdateNpcAsync_ClearVoice_RemovesVoiceId()
    {
        await _sut.CreateNpcAsync(1UL, "Gandalf", "Wizard", "voice123");

        var updated = await _sut.UpdateNpcAsync(1UL, "Gandalf", clearVoice: true);

        updated.VoiceId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateNpcAsync_SetVoice()
    {
        await _sut.CreateNpcAsync(1UL, "Gandalf", "Wizard");

        var updated = await _sut.UpdateNpcAsync(1UL, "Gandalf", voiceId: "newVoice");

        updated.VoiceId.Should().Be("newVoice");
    }

    [Fact]
    public async Task DeleteNpcAsync_RemovesNpc()
    {
        await _sut.CreateNpcAsync(1UL, "Gandalf", "Wizard");

        var result = await _sut.DeleteNpcAsync(1UL, "Gandalf");

        result.Should().BeTrue();
        (await _sut.GetNpcAsync(1UL, "Gandalf")).Should().BeNull();
    }

    [Fact]
    public async Task DeleteNpcAsync_NotFound_ReturnsFalse()
    {
        var result = await _sut.DeleteNpcAsync(1UL, "Unknown");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteNpcAsync_ClearsActiveNpcIfDeleted()
    {
        await _sut.CreateNpcAsync(1UL, "Gandalf", "Wizard");
        await _sut.SetActiveNpcAsync(1UL, "Gandalf");

        await _sut.DeleteNpcAsync(1UL, "Gandalf");

        var settings = await _sut.GetSettingsAsync(1UL);
        settings.ActiveNpcId.Should().BeNull();
    }

    [Fact]
    public async Task GetNpcCountAsync_ReturnsCorrectCount()
    {
        await _sut.CreateNpcAsync(1UL, "A", "a");
        await _sut.CreateNpcAsync(1UL, "B", "b");

        var count = await _sut.GetNpcCountAsync(1UL);

        count.Should().Be(2);
    }

    [Fact]
    public async Task GetNpcCountAsync_EmptyGuild_ReturnsZero()
    {
        var count = await _sut.GetNpcCountAsync(999UL);

        count.Should().Be(0);
    }

    // --- Guild NPC Settings ---

    [Fact]
    public async Task GetSettingsAsync_DefaultSettings()
    {
        var settings = await _sut.GetSettingsAsync(1UL);

        settings.ActiveNpcId.Should().BeNull();
        settings.AutoSwitchEnabled.Should().BeFalse();
        settings.SharedHistory.Should().BeFalse();
    }

    [Fact]
    public async Task SetActiveNpcAsync_SetsActive()
    {
        var npc = await _sut.CreateNpcAsync(1UL, "Gandalf", "Wizard");

        await _sut.SetActiveNpcAsync(1UL, "Gandalf");

        var settings = await _sut.GetSettingsAsync(1UL);
        settings.ActiveNpcId.Should().Be(npc.Id);
    }

    [Fact]
    public async Task SetActiveNpcAsync_NotFound_Throws()
    {
        var act = async () => await _sut.SetActiveNpcAsync(1UL, "Unknown");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SetAutoSwitchAsync_TogglesValue()
    {
        await _sut.SetAutoSwitchAsync(1UL, true);
        (await _sut.GetSettingsAsync(1UL)).AutoSwitchEnabled.Should().BeTrue();

        await _sut.SetAutoSwitchAsync(1UL, false);
        (await _sut.GetSettingsAsync(1UL)).AutoSwitchEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task SetHistoryModeAsync_ClearsHistoryOnChange()
    {
        var npc = await _sut.CreateNpcAsync(1UL, "Gandalf", "Wizard");
        await _sut.AddMessageAsync(1UL, npc.Id, "Gandalf", "user", "Hello");

        await _sut.SetHistoryModeAsync(1UL, true);

        var history = await _sut.GetHistoryAsync(1UL);
        history.Should().BeEmpty();

        var settings = await _sut.GetSettingsAsync(1UL);
        settings.SharedHistory.Should().BeTrue();
    }

    [Fact]
    public async Task SetHistoryModeAsync_NoChangeDoesNotClearHistory()
    {
        var npc = await _sut.CreateNpcAsync(1UL, "Gandalf", "Wizard");
        await _sut.AddMessageAsync(1UL, npc.Id, "Gandalf", "user", "Hello");

        await _sut.SetHistoryModeAsync(1UL, false);

        var history = await _sut.GetHistoryAsync(1UL, npc.Id);
        history.Should().HaveCount(1);
    }

    // --- Conversation History ---

    [Fact]
    public async Task AddMessageAsync_AddsMessage()
    {
        var npc = await _sut.CreateNpcAsync(1UL, "Gandalf", "Wizard");

        await _sut.AddMessageAsync(1UL, npc.Id, "Gandalf", "user", "Hello");

        var history = await _sut.GetHistoryAsync(1UL, npc.Id);
        history.Should().HaveCount(1);
        history[0].Role.Should().Be("user");
        history[0].Content.Should().Be("Hello");
        history[0].NpcName.Should().Be("Gandalf");
    }

    [Fact]
    public async Task GetHistoryAsync_PerNpcMode_FiltersPerNpc()
    {
        var gandalf = await _sut.CreateNpcAsync(1UL, "Gandalf", "Wizard");
        var saruman = await _sut.CreateNpcAsync(1UL, "Saruman", "Evil wizard");

        await _sut.AddMessageAsync(1UL, gandalf.Id, "Gandalf", "user", "Hello Gandalf");
        await _sut.AddMessageAsync(1UL, saruman.Id, "Saruman", "user", "Hello Saruman");

        var gandalfHistory = await _sut.GetHistoryAsync(1UL, gandalf.Id);
        gandalfHistory.Should().HaveCount(1);
        gandalfHistory[0].Content.Should().Be("Hello Gandalf");
    }

    [Fact]
    public async Task GetHistoryAsync_SharedMode_ReturnsAll()
    {
        var gandalf = await _sut.CreateNpcAsync(1UL, "Gandalf", "Wizard");
        var saruman = await _sut.CreateNpcAsync(1UL, "Saruman", "Evil wizard");

        await _sut.SetHistoryModeAsync(1UL, true);

        await _sut.AddMessageAsync(1UL, gandalf.Id, "Gandalf", "user", "Hello Gandalf");
        await _sut.AddMessageAsync(1UL, saruman.Id, "Saruman", "user", "Hello Saruman");

        var history = await _sut.GetHistoryAsync(1UL);
        history.Should().HaveCount(2);
    }

    [Fact]
    public async Task AddMessageAsync_TrimsHistory()
    {
        var npc = await _sut.CreateNpcAsync(1UL, "Gandalf", "Wizard");
        // Config has MaxHistoryMessages = 5

        for (int i = 1; i <= 7; i++)
            await _sut.AddMessageAsync(1UL, npc.Id, "Gandalf", "user", $"Message {i}");

        var history = await _sut.GetHistoryAsync(1UL, npc.Id);
        history.Should().HaveCount(5);
        history[0].Content.Should().Be("Message 3");
        history[4].Content.Should().Be("Message 7");
    }

    [Fact]
    public async Task ClearHistoryAsync_ClearsAllHistory()
    {
        var npc = await _sut.CreateNpcAsync(1UL, "Gandalf", "Wizard");
        await _sut.AddMessageAsync(1UL, npc.Id, "Gandalf", "user", "Hello");

        await _sut.ClearHistoryAsync(1UL);

        var history = await _sut.GetHistoryAsync(1UL);
        history.Should().BeEmpty();
    }

    [Fact]
    public async Task ClearHistoryAsync_ClearsPerNpcHistory()
    {
        var gandalf = await _sut.CreateNpcAsync(1UL, "Gandalf", "Wizard");
        var saruman = await _sut.CreateNpcAsync(1UL, "Saruman", "Evil");

        await _sut.AddMessageAsync(1UL, gandalf.Id, "Gandalf", "user", "Hello");
        await _sut.AddMessageAsync(1UL, saruman.Id, "Saruman", "user", "Hello");

        await _sut.ClearHistoryAsync(1UL, gandalf.Id);

        var gandalfHistory = await _sut.GetHistoryAsync(1UL, gandalf.Id);
        gandalfHistory.Should().BeEmpty();

        var sarumanHistory = await _sut.GetHistoryAsync(1UL, saruman.Id);
        sarumanHistory.Should().HaveCount(1);
    }

    [Fact]
    public async Task ClearHistoryAsync_OnEmptyGuild_DoesNotThrow()
    {
        var act = async () => await _sut.ClearHistoryAsync(999UL);

        await act.Should().NotThrowAsync();
    }

    // --- Import/Export ---

    [Fact]
    public async Task ExportNpcsAsync_ExportsJson()
    {
        await _sut.CreateNpcAsync(1UL, "Gandalf", "Wizard", "voice1");
        await _sut.CreateNpcAsync(1UL, "Saruman", "Evil wizard");

        var json = await _sut.ExportNpcsAsync(1UL);

        json.Should().Contain("Gandalf");
        json.Should().Contain("Saruman");
        json.Should().Contain("voice1");
        json.Should().Contain("\"version\": 1");
    }

    [Fact]
    public async Task ImportNpcsAsync_ImportsNewNpcs()
    {
        var json = """
        {
            "version": 1,
            "npcs": [
                { "name": "Gandalf", "personality": "Wizard" },
                { "name": "Saruman", "personality": "Evil wizard", "voiceId": "v2" }
            ]
        }
        """;

        var result = await _sut.ImportNpcsAsync(1UL, json);

        result.CreatedCount.Should().Be(2);
        result.SkippedNames.Should().BeEmpty();

        var npcs = await _sut.ListNpcsAsync(1UL);
        npcs.Should().HaveCount(2);
    }

    [Fact]
    public async Task ImportNpcsAsync_SkipsExistingNames()
    {
        await _sut.CreateNpcAsync(1UL, "Gandalf", "Existing wizard");

        var json = """
        {
            "version": 1,
            "npcs": [
                { "name": "Gandalf", "personality": "New wizard" },
                { "name": "Saruman", "personality": "Evil wizard" }
            ]
        }
        """;

        var result = await _sut.ImportNpcsAsync(1UL, json);

        result.CreatedCount.Should().Be(1);
        result.SkippedNames.Should().Contain("Gandalf");

        var gandalf = await _sut.GetNpcAsync(1UL, "Gandalf");
        gandalf!.Personality.Should().Be("Existing wizard");
    }

    [Fact]
    public async Task RoundTrip_ExportImport()
    {
        await _sut.CreateNpcAsync(1UL, "Gandalf", "Wizard", "v1");
        await _sut.CreateNpcAsync(1UL, "Saruman", "Evil wizard");

        var json = await _sut.ExportNpcsAsync(1UL);

        var result = await _sut.ImportNpcsAsync(2UL, json);

        result.CreatedCount.Should().Be(2);
        var npcs = await _sut.ListNpcsAsync(2UL);
        npcs.Should().HaveCount(2);
        npcs.First(n => n.Name == "Gandalf").VoiceId.Should().Be("v1");
    }

    // --- Multi-guild isolation ---

    [Fact]
    public async Task MultipleGuilds_HaveIndependentState()
    {
        await _sut.CreateNpcAsync(1UL, "Gandalf", "Guild 1 wizard");
        await _sut.CreateNpcAsync(2UL, "Gandalf", "Guild 2 wizard");

        var g1 = await _sut.GetNpcAsync(1UL, "Gandalf");
        var g2 = await _sut.GetNpcAsync(2UL, "Gandalf");

        g1!.Personality.Should().Be("Guild 1 wizard");
        g2!.Personality.Should().Be("Guild 2 wizard");
        g1.Id.Should().NotBe(g2.Id);
    }

    [Fact]
    public async Task DeleteNpc_RemovesItsConversationMessages()
    {
        var npc = await _sut.CreateNpcAsync(1UL, "Gandalf", "Wizard");
        await _sut.AddMessageAsync(1UL, npc.Id, "Gandalf", "user", "Hello");
        await _sut.AddMessageAsync(1UL, npc.Id, "Gandalf", "assistant", "Hi");

        await _sut.DeleteNpcAsync(1UL, "Gandalf");

        var history = await _sut.GetHistoryAsync(1UL, npc.Id);
        history.Should().BeEmpty();
    }
}
