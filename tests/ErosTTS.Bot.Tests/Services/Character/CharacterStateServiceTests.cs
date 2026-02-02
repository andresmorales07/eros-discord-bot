using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Services.Character;

namespace ErosTTS.Bot.Tests.Services.Character;

public class CharacterStateServiceTests
{
    private readonly ILogger<CharacterStateService> _logger;
    private readonly IOptions<OpenRouterConfiguration> _config;
    private readonly CharacterStateService _sut;

    public CharacterStateServiceTests()
    {
        _logger = Substitute.For<ILogger<CharacterStateService>>();
        _config = Options.Create(new OpenRouterConfiguration
        {
            ApiKey = "test-api-key",
            MaxHistoryMessages = 5
        });
        _sut = new CharacterStateService(_config, _logger);
    }

    [Fact]
    public async Task GetStateAsync_WhenNoStateExists_ReturnsNull()
    {
        var result = await _sut.GetStateAsync(12345UL);

        result.Should().BeNull();
    }

    [Fact]
    public async Task SetContextAsync_WithValidContext_StoresContext()
    {
        const ulong guildId = 12345UL;
        const string context = "You are a brave knight.";

        await _sut.SetContextAsync(guildId, context);

        var result = await _sut.GetStateAsync(guildId);
        result.Should().NotBeNull();
        result!.GuildId.Should().Be(guildId);
        result.Context.Should().Be(context);
    }

    [Fact]
    public async Task SetContextAsync_WithAppendFalse_ReplacesExistingContext()
    {
        const ulong guildId = 12345UL;

        await _sut.SetContextAsync(guildId, "Original context");
        await _sut.SetContextAsync(guildId, "New context", append: false);

        var result = await _sut.GetStateAsync(guildId);
        result.Should().NotBeNull();
        result!.Context.Should().Be("New context");
    }

    [Fact]
    public async Task SetContextAsync_WithAppendTrue_AppendsToExistingContext()
    {
        const ulong guildId = 12345UL;

        await _sut.SetContextAsync(guildId, "You are a knight.");
        await _sut.SetContextAsync(guildId, "You carry a sword.", append: true);

        var result = await _sut.GetStateAsync(guildId);
        result.Should().NotBeNull();
        result!.Context.Should().Be("You are a knight.\nYou carry a sword.");
    }

    [Fact]
    public async Task SetContextAsync_SetsUpdatedAtToRecentTime()
    {
        var beforeSet = DateTimeOffset.UtcNow;

        await _sut.SetContextAsync(12345UL, "Test context");

        var afterSet = DateTimeOffset.UtcNow;
        var result = await _sut.GetStateAsync(12345UL);
        result.Should().NotBeNull();
        result!.UpdatedAt.Should().BeOnOrAfter(beforeSet);
        result.UpdatedAt.Should().BeOnOrBefore(afterSet);
    }

    [Fact]
    public async Task AddMessageAsync_WithNewGuild_CreatesStateWithMessage()
    {
        const ulong guildId = 12345UL;

        await _sut.AddMessageAsync(guildId, "user", "Hello!");

        var result = await _sut.GetStateAsync(guildId);
        result.Should().NotBeNull();
        result!.ConversationHistory.Should().HaveCount(1);
        result.ConversationHistory[0].Role.Should().Be("user");
        result.ConversationHistory[0].Content.Should().Be("Hello!");
    }

    [Fact]
    public async Task AddMessageAsync_AddsToExistingHistory()
    {
        const ulong guildId = 12345UL;

        await _sut.AddMessageAsync(guildId, "user", "Hello!");
        await _sut.AddMessageAsync(guildId, "assistant", "Hi there!");
        await _sut.AddMessageAsync(guildId, "user", "How are you?");

        var result = await _sut.GetStateAsync(guildId);
        result.Should().NotBeNull();
        result!.ConversationHistory.Should().HaveCount(3);
        result.ConversationHistory[0].Content.Should().Be("Hello!");
        result.ConversationHistory[1].Content.Should().Be("Hi there!");
        result.ConversationHistory[2].Content.Should().Be("How are you?");
    }

    [Fact]
    public async Task AddMessageAsync_TrimsHistoryWhenExceedsMaxHistoryMessages()
    {
        const ulong guildId = 12345UL;
        // Config has MaxHistoryMessages = 5

        for (int i = 1; i <= 7; i++)
        {
            await _sut.AddMessageAsync(guildId, "user", $"Message {i}");
        }

        var result = await _sut.GetStateAsync(guildId);
        result.Should().NotBeNull();
        result!.ConversationHistory.Should().HaveCount(5);
        // Should keep the last 5 messages (3-7)
        result.ConversationHistory[0].Content.Should().Be("Message 3");
        result.ConversationHistory[4].Content.Should().Be("Message 7");
    }

    [Fact]
    public async Task AddMessageAsync_PreservesContextWhenAddingMessages()
    {
        const ulong guildId = 12345UL;

        await _sut.SetContextAsync(guildId, "You are a wizard.");
        await _sut.AddMessageAsync(guildId, "user", "Cast a spell!");

        var result = await _sut.GetStateAsync(guildId);
        result.Should().NotBeNull();
        result!.Context.Should().Be("You are a wizard.");
        result.ConversationHistory.Should().HaveCount(1);
    }

    [Fact]
    public async Task ClearStateAsync_RemovesAllState()
    {
        const ulong guildId = 12345UL;
        await _sut.SetContextAsync(guildId, "Test context");
        await _sut.AddMessageAsync(guildId, "user", "Hello!");

        await _sut.ClearStateAsync(guildId);

        var result = await _sut.GetStateAsync(guildId);
        result.Should().BeNull();
    }

    [Fact]
    public async Task ClearStateAsync_WhenNoStateExists_DoesNotThrow()
    {
        var act = async () => await _sut.ClearStateAsync(99999UL);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ConcurrentOperations_AreThreadSafe()
    {
        var tasks = Enumerable.Range(1, 50)
            .Select(i => Task.Run(async () =>
            {
                var guildId = (ulong)i;
                await _sut.SetContextAsync(guildId, $"Context for {i}");
                await _sut.AddMessageAsync(guildId, "user", $"Message from {i}");
                var state = await _sut.GetStateAsync(guildId);
                return state;
            }))
            .ToList();

        var results = await Task.WhenAll(tasks);

        results.Should().AllSatisfy(r => r.Should().NotBeNull());
        results.Should().AllSatisfy(r => r!.ConversationHistory.Should().HaveCount(1));
    }

    [Fact]
    public async Task AddMessageAsync_SetsTimestampOnMessage()
    {
        const ulong guildId = 12345UL;
        var beforeAdd = DateTimeOffset.UtcNow;

        await _sut.AddMessageAsync(guildId, "user", "Test message");

        var afterAdd = DateTimeOffset.UtcNow;
        var result = await _sut.GetStateAsync(guildId);
        result.Should().NotBeNull();
        result!.ConversationHistory[0].Timestamp.Should().BeOnOrAfter(beforeAdd);
        result.ConversationHistory[0].Timestamp.Should().BeOnOrBefore(afterAdd);
    }

    [Fact]
    public async Task SetContextAsync_WithEmptyContext_StoresEmptyString()
    {
        const ulong guildId = 12345UL;

        await _sut.SetContextAsync(guildId, "");

        var result = await _sut.GetStateAsync(guildId);
        result.Should().NotBeNull();
        result!.Context.Should().BeEmpty();
    }

    [Fact]
    public async Task MultipleGuilds_HaveIndependentState()
    {
        await _sut.SetContextAsync(111UL, "Guild 1 context");
        await _sut.SetContextAsync(222UL, "Guild 2 context");
        await _sut.AddMessageAsync(111UL, "user", "Message in guild 1");

        var guild1 = await _sut.GetStateAsync(111UL);
        var guild2 = await _sut.GetStateAsync(222UL);

        guild1!.Context.Should().Be("Guild 1 context");
        guild1.ConversationHistory.Should().HaveCount(1);
        guild2!.Context.Should().Be("Guild 2 context");
        guild2.ConversationHistory.Should().BeEmpty();
    }
}
