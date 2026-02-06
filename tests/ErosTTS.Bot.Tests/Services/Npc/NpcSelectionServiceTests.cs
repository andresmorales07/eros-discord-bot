using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Services.LLM;
using ErosTTS.Bot.Services.Npc;

namespace ErosTTS.Bot.Tests.Services.Npc;

public class NpcSelectionServiceTests
{
    private readonly ILlmService _llmService;
    private readonly NpcSelectionService _sut;

    private static readonly NpcDefinition Gandalf = new()
    {
        Id = 1, GuildId = 1, Name = "Gandalf", Personality = "You are Gandalf the Grey, a wise wizard."
    };

    private static readonly NpcDefinition Saruman = new()
    {
        Id = 2, GuildId = 1, Name = "Saruman", Personality = "You are Saruman the White, a powerful and cunning wizard."
    };

    private static readonly NpcDefinition Frodo = new()
    {
        Id = 3, GuildId = 1, Name = "Frodo", Personality = "You are Frodo Baggins, a brave hobbit carrying the One Ring."
    };

    public NpcSelectionServiceTests()
    {
        _llmService = Substitute.For<ILlmService>();
        var logger = Substitute.For<ILogger<NpcSelectionService>>();
        var config = Options.Create(new NpcConfiguration
        {
            AutoSwitchContextMessages = 3
        });
        _sut = new NpcSelectionService(_llmService, config, logger);
    }

    [Fact]
    public async Task SelectNpcAsync_SingleNpc_ReturnsThatNpc()
    {
        var result = await _sut.SelectNpcAsync(1UL, "Hello!", [Gandalf], [], CancellationToken.None);

        result.Should().Be(Gandalf);
        // Should not call LLM for single NPC
        await _llmService.DidNotReceiveWithAnyArgs().GetCompletionAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task SelectNpcAsync_LlmReturnsExactName_SelectsThatNpc()
    {
        _llmService.GetCompletionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ConversationMessage>>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Saruman");

        var result = await _sut.SelectNpcAsync(1UL, "I seek power!", [Gandalf, Saruman, Frodo], [], CancellationToken.None);

        result.Should().Be(Saruman);
    }

    [Fact]
    public async Task SelectNpcAsync_LlmReturnsNameWithExtraText_SelectsThatNpc()
    {
        _llmService.GetCompletionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ConversationMessage>>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("I think Frodo should respond to this.");

        var result = await _sut.SelectNpcAsync(1UL, "Where is the ring?", [Gandalf, Saruman, Frodo], [], CancellationToken.None);

        result.Should().Be(Frodo);
    }

    [Fact]
    public async Task SelectNpcAsync_LlmReturnsNameCaseInsensitive_SelectsThatNpc()
    {
        _llmService.GetCompletionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ConversationMessage>>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("gandalf");

        var result = await _sut.SelectNpcAsync(1UL, "Wizard?", [Gandalf, Saruman], [], CancellationToken.None);

        result.Should().Be(Gandalf);
    }

    [Fact]
    public async Task SelectNpcAsync_LlmReturnsUnrecognizedName_FallsBackToFirst()
    {
        _llmService.GetCompletionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ConversationMessage>>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Aragorn");

        var result = await _sut.SelectNpcAsync(1UL, "Hello!", [Gandalf, Saruman], [], CancellationToken.None);

        result.Should().Be(Gandalf);
    }

    [Fact]
    public async Task SelectNpcAsync_LlmThrows_FallsBackToFirst()
    {
        _llmService.GetCompletionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ConversationMessage>>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<string>(x => throw new Exception("LLM error"));

        var result = await _sut.SelectNpcAsync(1UL, "Hello!", [Gandalf, Saruman], [], CancellationToken.None);

        result.Should().Be(Gandalf);
    }

    [Fact]
    public async Task SelectNpcAsync_NoNpcs_Throws()
    {
        var act = async () => await _sut.SelectNpcAsync(1UL, "Hello!", [], [], CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SelectNpcAsync_PassesRecentHistoryToLlm()
    {
        var history = new List<NpcConversationMessage>
        {
            new() { Role = "user", Content = "Msg 1", NpcName = null, Timestamp = DateTimeOffset.UtcNow },
            new() { Role = "assistant", Content = "Reply 1", NpcName = "Gandalf", Timestamp = DateTimeOffset.UtcNow },
            new() { Role = "user", Content = "Msg 2", NpcName = null, Timestamp = DateTimeOffset.UtcNow },
            new() { Role = "assistant", Content = "Reply 2", NpcName = "Saruman", Timestamp = DateTimeOffset.UtcNow },
            new() { Role = "user", Content = "Msg 3", NpcName = null, Timestamp = DateTimeOffset.UtcNow },
        };

        _llmService.GetCompletionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ConversationMessage>>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Gandalf");

        await _sut.SelectNpcAsync(1UL, "New message", [Gandalf, Saruman], history, CancellationToken.None);

        // Should only pass last 3 (AutoSwitchContextMessages=3)
        await _llmService.Received(1).GetCompletionAsync(
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<ConversationMessage>>(msgs => msgs.Count == 3),
            "New message",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectNpcAsync_FormatsAssistantMessagesWithNpcName()
    {
        var history = new List<NpcConversationMessage>
        {
            new() { Role = "assistant", Content = "Hello there", NpcName = "Gandalf", Timestamp = DateTimeOffset.UtcNow },
        };

        _llmService.GetCompletionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ConversationMessage>>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Gandalf");

        await _sut.SelectNpcAsync(1UL, "Hello", [Gandalf, Saruman], history, CancellationToken.None);

        await _llmService.Received(1).GetCompletionAsync(
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<ConversationMessage>>(msgs =>
                msgs[0].Content == "[Gandalf]: Hello there"),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}
