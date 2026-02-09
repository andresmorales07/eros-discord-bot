using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Exceptions;
using ErosTTS.Bot.Services;
using ErosTTS.Bot.Services.LLM;
using ErosTTS.Bot.Services.Npc;

namespace ErosTTS.Bot.Tests.Services;

public sealed class PromptOrchestrationServiceTests
{
    private const ulong GuildId = 100;
    private const ulong VoiceChannelId = 300;

    private readonly INpcService _npcService = Substitute.For<INpcService>();
    private readonly INpcSelectionService _selectionService = Substitute.For<INpcSelectionService>();
    private readonly ILlmService _llmService = Substitute.For<ILlmService>();
    private readonly ILogger<PromptOrchestrationService> _logger = Substitute.For<ILogger<PromptOrchestrationService>>();

    private PromptOrchestrationService CreateService(int maxMessageLength = 500)
    {
        var botConfig = Options.Create(new BotConfiguration
        {
            Token = "test-token",
            MaxMessageLength = maxMessageLength
        });

        return new PromptOrchestrationService(
            _npcService,
            _selectionService,
            _llmService,
            botConfig,
            _logger);
    }

    private static NpcDefinition CreateNpc(int id, string name, string? voiceId = null) => new()
    {
        Id = id,
        GuildId = GuildId,
        Name = name,
        Personality = $"You are {name}",
        VoiceId = voiceId
    };

    private void SetupDefaultNpcs(params NpcDefinition[] npcs)
    {
        _npcService.ListNpcsAsync(GuildId).Returns(npcs.ToList().AsReadOnly());
        _npcService.GetHistoryAsync(GuildId, Arg.Any<int?>())
            .Returns(Array.Empty<NpcConversationMessage>().AsReadOnly());
    }

    private void SetupSettings(int? activeNpcId = null, bool autoSwitch = false, bool sharedHistory = false)
    {
        _npcService.GetSettingsAsync(GuildId).Returns(new GuildNpcSettings
        {
            GuildId = GuildId,
            ActiveNpcId = activeNpcId,
            AutoSwitchEnabled = autoSwitch,
            SharedHistory = sharedHistory
        });
    }

    // ─── NPC Selection Logic ────────────────────────────────────────────

    [Fact]
    public async Task HandlePromptAsync_NoNpcs_ThrowsInvalidOperationException()
    {
        var sut = CreateService();
        SetupSettings();
        _npcService.ListNpcsAsync(GuildId).Returns(Array.Empty<NpcDefinition>().AsReadOnly());

        var act = () => sut.HandlePromptAsync(GuildId, VoiceChannelId, "Hello", default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No NPCs*");
    }

    [Fact]
    public async Task HandlePromptAsync_NoActiveNpc_UsesFirstNpc()
    {
        var sut = CreateService();
        var npc1 = CreateNpc(1, "Alice");
        var npc2 = CreateNpc(2, "Bob");
        SetupDefaultNpcs(npc1, npc2);
        SetupSettings(activeNpcId: null, autoSwitch: false);
        _llmService.GetCompletionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ConversationMessage>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Response from Alice");

        var result = await sut.HandlePromptAsync(GuildId, VoiceChannelId, "Hello", default);

        result.NpcName.Should().Be("Alice");
    }

    [Fact]
    public async Task HandlePromptAsync_ActiveNpc_UsesActiveNpc()
    {
        var sut = CreateService();
        var npc1 = CreateNpc(1, "Alice");
        var npc2 = CreateNpc(2, "Bob");
        SetupDefaultNpcs(npc1, npc2);
        SetupSettings(activeNpcId: 2, autoSwitch: false);
        _llmService.GetCompletionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ConversationMessage>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Response from Bob");

        var result = await sut.HandlePromptAsync(GuildId, VoiceChannelId, "Hello", default);

        result.NpcName.Should().Be("Bob");
    }

    [Fact]
    public async Task HandlePromptAsync_ActiveNpcNotInList_FallsBackToFirst()
    {
        var sut = CreateService();
        var npc1 = CreateNpc(1, "Alice");
        SetupDefaultNpcs(npc1);
        SetupSettings(activeNpcId: 999, autoSwitch: false);
        _llmService.GetCompletionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ConversationMessage>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Response");

        var result = await sut.HandlePromptAsync(GuildId, VoiceChannelId, "Hello", default);

        result.NpcName.Should().Be("Alice");
    }

    [Fact]
    public async Task HandlePromptAsync_AutoSwitchWithMultipleNpcs_UsesSelectionService()
    {
        var sut = CreateService();
        var npc1 = CreateNpc(1, "Alice");
        var npc2 = CreateNpc(2, "Bob");
        SetupDefaultNpcs(npc1, npc2);
        SetupSettings(autoSwitch: true);
        _selectionService.SelectNpcAsync(Arg.Any<ulong>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<NpcDefinition>>(), Arg.Any<IReadOnlyList<NpcConversationMessage>>(), Arg.Any<CancellationToken>())
            .Returns(npc2);
        _llmService.GetCompletionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ConversationMessage>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Response from Bob");

        var result = await sut.HandlePromptAsync(GuildId, VoiceChannelId, "Hello", default);

        result.NpcName.Should().Be("Bob");
        await _selectionService.Received(1).SelectNpcAsync(
            GuildId, "Hello", Arg.Any<IReadOnlyList<NpcDefinition>>(), Arg.Any<IReadOnlyList<NpcConversationMessage>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandlePromptAsync_AutoSwitchWithSingleNpc_SkipsSelectionService()
    {
        var sut = CreateService();
        var npc1 = CreateNpc(1, "Alice");
        SetupDefaultNpcs(npc1);
        SetupSettings(autoSwitch: true);
        _llmService.GetCompletionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ConversationMessage>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Response");

        var result = await sut.HandlePromptAsync(GuildId, VoiceChannelId, "Hello", default);

        result.NpcName.Should().Be("Alice");
        await _selectionService.DidNotReceive().SelectNpcAsync(
            Arg.Any<ulong>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<NpcDefinition>>(), Arg.Any<IReadOnlyList<NpcConversationMessage>>(), Arg.Any<CancellationToken>());
    }

    // ─── History ────────────────────────────────────────────────────────

    [Fact]
    public async Task HandlePromptAsync_PerNpcHistory_GetsHistoryForSelectedNpc()
    {
        var sut = CreateService();
        var npc1 = CreateNpc(1, "Alice");
        SetupDefaultNpcs(npc1);
        SetupSettings(activeNpcId: 1, sharedHistory: false);
        _llmService.GetCompletionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ConversationMessage>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Response");

        await sut.HandlePromptAsync(GuildId, VoiceChannelId, "Hello", default);

        // Should get history with npcId = 1
        await _npcService.Received(1).GetHistoryAsync(GuildId, 1);
    }

    [Fact]
    public async Task HandlePromptAsync_SharedHistory_GetsAllHistory()
    {
        var sut = CreateService();
        var npc1 = CreateNpc(1, "Alice");
        SetupDefaultNpcs(npc1);
        SetupSettings(activeNpcId: 1, sharedHistory: true);
        _llmService.GetCompletionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ConversationMessage>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Response");

        await sut.HandlePromptAsync(GuildId, VoiceChannelId, "Hello", default);

        // Should get history with null npcId (shared)
        await _npcService.Received(1).GetHistoryAsync(GuildId, null);
    }

    [Fact]
    public async Task HandlePromptAsync_SharedHistory_PrefixesAssistantMessages()
    {
        var sut = CreateService();
        var npc1 = CreateNpc(1, "Alice");
        SetupDefaultNpcs(npc1);
        SetupSettings(activeNpcId: 1, sharedHistory: true);

        var historyMessages = new List<NpcConversationMessage>
        {
            new() { Role = "assistant", Content = "I am Alice", NpcName = "Alice" },
            new() { Role = "user", Content = "Hello" }
        };
        _npcService.GetHistoryAsync(GuildId, null).Returns(historyMessages.AsReadOnly());

        IReadOnlyList<ConversationMessage>? capturedHistory = null;
        _llmService.GetCompletionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ConversationMessage>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                capturedHistory = ci.ArgAt<IReadOnlyList<ConversationMessage>>(1);
                return "Response";
            });

        await sut.HandlePromptAsync(GuildId, VoiceChannelId, "Hello", default);

        capturedHistory.Should().NotBeNull();
        capturedHistory![0].Content.Should().Be("[Alice]: I am Alice");
        capturedHistory[1].Content.Should().Be("Hello");
    }

    // ─── Message Storage ────────────────────────────────────────────────

    [Fact]
    public async Task HandlePromptAsync_StoresUserAndAssistantMessages()
    {
        var sut = CreateService();
        var npc1 = CreateNpc(1, "Alice");
        SetupDefaultNpcs(npc1);
        SetupSettings(activeNpcId: 1);
        _llmService.GetCompletionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ConversationMessage>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("I am Alice!");

        await sut.HandlePromptAsync(GuildId, VoiceChannelId, "Who are you?", default);

        await _npcService.Received(1).AddMessageAsync(GuildId, null, null, "user", "Who are you?");
        await _npcService.Received(1).AddMessageAsync(GuildId, 1, "Alice", "assistant", "I am Alice!");
    }

    // ─── Response Sanitization and TTS ──────────────────────────────────

    [Fact]
    public async Task HandlePromptAsync_SanitizesResponseForTts()
    {
        var sut = CreateService();
        var npc1 = CreateNpc(1, "Alice");
        SetupDefaultNpcs(npc1);
        SetupSettings(activeNpcId: 1);
        _llmService.GetCompletionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ConversationMessage>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Check this **bold** text with https://example.com link");

        var result = await sut.HandlePromptAsync(GuildId, VoiceChannelId, "Hi", default);

        result.QueueItem.Text.Should().Be("Check this bold text with link");
    }

    [Fact]
    public async Task HandlePromptAsync_EmptySanitizedResponse_UsesFallbackText()
    {
        var sut = CreateService();
        var npc1 = CreateNpc(1, "Alice");
        SetupDefaultNpcs(npc1);
        SetupSettings(activeNpcId: 1);
        _llmService.GetCompletionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ConversationMessage>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://example.com"); // gets fully stripped

        var result = await sut.HandlePromptAsync(GuildId, VoiceChannelId, "Hi", default);

        result.QueueItem.Text.Should().Be("I have nothing to say.");
    }

    [Fact]
    public async Task HandlePromptAsync_TruncatesTtsResponse()
    {
        var sut = CreateService(maxMessageLength: 10);
        var npc1 = CreateNpc(1, "Alice");
        SetupDefaultNpcs(npc1);
        SetupSettings(activeNpcId: 1);
        _llmService.GetCompletionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ConversationMessage>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("This is a very long response that should be truncated");

        var result = await sut.HandlePromptAsync(GuildId, VoiceChannelId, "Hi", default);

        result.QueueItem.Text.Should().HaveLength(10);
    }

    [Fact]
    public async Task HandlePromptAsync_QueueItemUsesNpcVoiceId()
    {
        var sut = CreateService();
        var npc1 = CreateNpc(1, "Alice", voiceId: "alice-voice-123");
        SetupDefaultNpcs(npc1);
        SetupSettings(activeNpcId: 1);
        _llmService.GetCompletionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ConversationMessage>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Hello!");

        var result = await sut.HandlePromptAsync(GuildId, VoiceChannelId, "Hi", default);

        result.QueueItem.VoiceId.Should().Be("alice-voice-123");
        result.QueueItem.GuildId.Should().Be(GuildId);
        result.QueueItem.VoiceChannelId.Should().Be(VoiceChannelId);
        result.QueueItem.Username.Should().Be("Alice");
    }

    [Fact]
    public async Task HandlePromptAsync_ReturnsFullResponseText()
    {
        var sut = CreateService();
        var npc1 = CreateNpc(1, "Alice");
        SetupDefaultNpcs(npc1);
        SetupSettings(activeNpcId: 1);
        _llmService.GetCompletionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ConversationMessage>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Full response text here");

        var result = await sut.HandlePromptAsync(GuildId, VoiceChannelId, "Hi", default);

        result.Response.Should().Be("Full response text here");
    }

    [Fact]
    public async Task HandlePromptAsync_UsesNpcPersonalityAsSystemPrompt()
    {
        var sut = CreateService();
        var npc1 = CreateNpc(1, "Alice");
        SetupDefaultNpcs(npc1);
        SetupSettings(activeNpcId: 1);

        string? capturedSystemPrompt = null;
        _llmService.GetCompletionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ConversationMessage>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                capturedSystemPrompt = ci.ArgAt<string>(0);
                return "Response";
            });

        await sut.HandlePromptAsync(GuildId, VoiceChannelId, "Hello", default);

        capturedSystemPrompt.Should().Be("You are Alice");
    }

    [Fact]
    public async Task HandlePromptAsync_LlmException_Propagates()
    {
        var sut = CreateService();
        var npc1 = CreateNpc(1, "Alice");
        SetupDefaultNpcs(npc1);
        SetupSettings(activeNpcId: 1);
        _llmService.GetCompletionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ConversationMessage>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new LlmServiceException("Service unavailable")));

        var act = () => sut.HandlePromptAsync(GuildId, VoiceChannelId, "Hi", default);

        await act.Should().ThrowAsync<LlmServiceException>()
            .WithMessage("Service unavailable");
    }
}
