using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Services;
using ErosTTS.Bot.Services.Guild;

namespace ErosTTS.Bot.Tests.Services;

public sealed class MessageProcessingServiceTests
{
    private const ulong GuildId = 100;
    private const ulong TextChannelId = 200;
    private const ulong VoiceChannelId = 300;

    private readonly IGuildConfigurationService _guildConfig = Substitute.For<IGuildConfigurationService>();
    private readonly ILogger<MessageProcessingService> _logger = Substitute.For<ILogger<MessageProcessingService>>();

    private MessageProcessingService CreateService(
        bool processBotMessages = false,
        int maxMessageLength = 500)
    {
        var config = Options.Create(new BotConfiguration
        {
            Token = "test-token",
            ProcessBotMessages = processBotMessages,
            MaxMessageLength = maxMessageLength
        });

        return new MessageProcessingService(_guildConfig, config, _logger);
    }

    private void SetupGuildConfig(ulong? voiceChannelId = VoiceChannelId)
    {
        _guildConfig.GetConfigurationAsync(GuildId).Returns(new GuildTtsConfiguration
        {
            GuildId = GuildId,
            TextChannelId = TextChannelId,
            VoiceChannelId = voiceChannelId
        });
    }

    [Fact]
    public async Task ProcessMessageAsync_BotMessage_WhenNotAllowed_ReturnsNull()
    {
        var sut = CreateService(processBotMessages: false);

        var result = await sut.ProcessMessageAsync(GuildId, TextChannelId, "Hello", "BotUser", isBot: true);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ProcessMessageAsync_BotMessage_WhenAllowed_ReturnsItem()
    {
        var sut = CreateService(processBotMessages: true);
        SetupGuildConfig();

        var result = await sut.ProcessMessageAsync(GuildId, TextChannelId, "Hello", "BotUser", isBot: true);

        result.Should().NotBeNull();
        result!.Text.Should().Contain("BotUser says: Hello");
    }

    [Fact]
    public async Task ProcessMessageAsync_UnmonitoredChannel_ReturnsNull()
    {
        var sut = CreateService();
        SetupGuildConfig();

        var result = await sut.ProcessMessageAsync(GuildId, 999, "Hello", "User", isBot: false);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ProcessMessageAsync_NoGuildConfig_ReturnsNull()
    {
        var sut = CreateService();
        _guildConfig.GetConfigurationAsync(GuildId).Returns((GuildTtsConfiguration?)null);

        var result = await sut.ProcessMessageAsync(GuildId, TextChannelId, "Hello", "User", isBot: false);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ProcessMessageAsync_NoVoiceChannel_ReturnsNull()
    {
        var sut = CreateService();
        SetupGuildConfig(voiceChannelId: null);

        var result = await sut.ProcessMessageAsync(GuildId, TextChannelId, "Hello", "User", isBot: false);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ProcessMessageAsync_EmptyAfterSanitization_ReturnsNull()
    {
        var sut = CreateService();
        SetupGuildConfig();

        // Only contains mentions which get stripped
        var result = await sut.ProcessMessageAsync(GuildId, TextChannelId, "<@123456>", "User", isBot: false);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ProcessMessageAsync_ValidMessage_ReturnsCorrectQueueItem()
    {
        var sut = CreateService();
        SetupGuildConfig();

        var result = await sut.ProcessMessageAsync(GuildId, TextChannelId, "Hello world!", "TestUser", isBot: false);

        result.Should().NotBeNull();
        result!.GuildId.Should().Be(GuildId);
        result.TextChannelId.Should().Be(TextChannelId);
        result.VoiceChannelId.Should().Be(VoiceChannelId);
        result.Username.Should().Be("TestUser");
        result.Text.Should().Be("TestUser says: Hello world!");
    }

    [Fact]
    public async Task ProcessMessageAsync_SanitizesDiscordMentions()
    {
        var sut = CreateService();
        SetupGuildConfig();

        var result = await sut.ProcessMessageAsync(GuildId, TextChannelId, "Hey <@123456> check this", "User", isBot: false);

        result.Should().NotBeNull();
        result!.Text.Should().Be("User says: Hey check this");
    }

    [Fact]
    public async Task ProcessMessageAsync_TruncatesLongMessages()
    {
        var sut = CreateService(maxMessageLength: 10);
        SetupGuildConfig();

        var longText = new string('A', 100);
        var result = await sut.ProcessMessageAsync(GuildId, TextChannelId, longText, "User", isBot: false);

        result.Should().NotBeNull();
        // The format is "User says: {truncated_text}"
        // The text portion before the prefix should be truncated to 10 chars
        var expectedPrefix = "User says: ";
        var textAfterPrefix = result!.Text[expectedPrefix.Length..];
        textAfterPrefix.Should().HaveLength(10);
    }

    [Fact]
    public async Task ProcessMessageAsync_MessageExactlyAtLimit_NotTruncated()
    {
        var sut = CreateService(maxMessageLength: 5);
        SetupGuildConfig();

        var result = await sut.ProcessMessageAsync(GuildId, TextChannelId, "Hello", "User", isBot: false);

        result.Should().NotBeNull();
        result!.Text.Should().Be("User says: Hello");
    }

    [Fact]
    public async Task ProcessMessageAsync_SetsUniqueId()
    {
        var sut = CreateService();
        SetupGuildConfig();

        var result1 = await sut.ProcessMessageAsync(GuildId, TextChannelId, "First", "User", isBot: false);
        var result2 = await sut.ProcessMessageAsync(GuildId, TextChannelId, "Second", "User", isBot: false);

        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1!.Id.Should().NotBe(result2!.Id);
    }
}
