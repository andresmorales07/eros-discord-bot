using ErosTTS.Bot.Services.Guild;
using ErosTTS.Bot.Services.TTS;

namespace ErosTTS.Bot.Tests.Services.TTS;

public class TtsProviderFactoryTests
{
    private readonly IGuildConfigurationService _guildConfig = Substitute.For<IGuildConfigurationService>();
    private readonly ILogger<TtsProviderFactory> _logger = Substitute.For<ILogger<TtsProviderFactory>>();

    private static ITtsProvider CreateMockProvider(string name)
    {
        var provider = Substitute.For<ITtsProvider>();
        provider.ProviderName.Returns(name);
        return provider;
    }

    [Fact]
    public async Task GetProviderAsync_NoGuildConfig_ReturnsDefaultProvider()
    {
        var elevenLabs = CreateMockProvider("ElevenLabs");
        var openAi = CreateMockProvider("OpenAI");
        _guildConfig.GetConfigurationAsync(Arg.Any<ulong>()).Returns((GuildTtsConfiguration?)null);

        var factory = new TtsProviderFactory([elevenLabs, openAi], _guildConfig, _logger);

        var result = await factory.GetProviderAsync(100);

        result.Should().BeSameAs(elevenLabs);
    }

    [Fact]
    public async Task GetProviderAsync_GuildConfigWithNoProvider_ReturnsDefaultProvider()
    {
        var elevenLabs = CreateMockProvider("ElevenLabs");
        _guildConfig.GetConfigurationAsync(100).Returns(new GuildTtsConfiguration
        {
            GuildId = 100,
            TtsProvider = null
        });

        var factory = new TtsProviderFactory([elevenLabs], _guildConfig, _logger);

        var result = await factory.GetProviderAsync(100);

        result.Should().BeSameAs(elevenLabs);
    }

    [Fact]
    public async Task GetProviderAsync_GuildConfigWithOpenAI_ReturnsOpenAIProvider()
    {
        var elevenLabs = CreateMockProvider("ElevenLabs");
        var openAi = CreateMockProvider("OpenAI");
        _guildConfig.GetConfigurationAsync(100).Returns(new GuildTtsConfiguration
        {
            GuildId = 100,
            TtsProvider = "OpenAI"
        });

        var factory = new TtsProviderFactory([elevenLabs, openAi], _guildConfig, _logger);

        var result = await factory.GetProviderAsync(100);

        result.Should().BeSameAs(openAi);
    }

    [Fact]
    public async Task GetProviderAsync_GuildConfigWithUnknownProvider_ReturnsDefaultProvider()
    {
        var elevenLabs = CreateMockProvider("ElevenLabs");
        _guildConfig.GetConfigurationAsync(100).Returns(new GuildTtsConfiguration
        {
            GuildId = 100,
            TtsProvider = "NonExistent"
        });

        var factory = new TtsProviderFactory([elevenLabs], _guildConfig, _logger);

        var result = await factory.GetProviderAsync(100);

        result.Should().BeSameAs(elevenLabs);
    }

    [Fact]
    public async Task GetProviderAsync_CaseInsensitiveLookup()
    {
        var openAi = CreateMockProvider("OpenAI");
        var elevenLabs = CreateMockProvider("ElevenLabs");
        _guildConfig.GetConfigurationAsync(100).Returns(new GuildTtsConfiguration
        {
            GuildId = 100,
            TtsProvider = "openai"
        });

        var factory = new TtsProviderFactory([elevenLabs, openAi], _guildConfig, _logger);

        var result = await factory.GetProviderAsync(100);

        result.Should().BeSameAs(openAi);
    }

    [Fact]
    public void GetProviderByName_ExistingProvider_ReturnsProvider()
    {
        var elevenLabs = CreateMockProvider("ElevenLabs");

        var factory = new TtsProviderFactory([elevenLabs], _guildConfig, _logger);

        var result = factory.GetProviderByName("ElevenLabs");

        result.Should().BeSameAs(elevenLabs);
    }

    [Fact]
    public void GetProviderByName_CaseInsensitive()
    {
        var elevenLabs = CreateMockProvider("ElevenLabs");

        var factory = new TtsProviderFactory([elevenLabs], _guildConfig, _logger);

        var result = factory.GetProviderByName("elevenlabs");

        result.Should().BeSameAs(elevenLabs);
    }

    [Fact]
    public void GetProviderByName_NonExistent_ReturnsNull()
    {
        var elevenLabs = CreateMockProvider("ElevenLabs");

        var factory = new TtsProviderFactory([elevenLabs], _guildConfig, _logger);

        var result = factory.GetProviderByName("NonExistent");

        result.Should().BeNull();
    }

    [Fact]
    public void GetAvailableProviders_ReturnsAllRegisteredProviders()
    {
        var elevenLabs = CreateMockProvider("ElevenLabs");
        var openAi = CreateMockProvider("OpenAI");

        var factory = new TtsProviderFactory([elevenLabs, openAi], _guildConfig, _logger);

        var result = factory.GetAvailableProviders();

        result.Should().HaveCount(2);
        result.Should().Contain("ElevenLabs");
        result.Should().Contain("OpenAI");
    }

    [Fact]
    public void GetAvailableProviders_SingleProvider_ReturnsSingleItem()
    {
        var elevenLabs = CreateMockProvider("ElevenLabs");

        var factory = new TtsProviderFactory([elevenLabs], _guildConfig, _logger);

        var result = factory.GetAvailableProviders();

        result.Should().ContainSingle().Which.Should().Be("ElevenLabs");
    }
}
