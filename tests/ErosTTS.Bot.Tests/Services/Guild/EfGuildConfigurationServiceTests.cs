using ErosTTS.Bot.Services.Guild;
using ErosTTS.Bot.Tests.Data;

namespace ErosTTS.Bot.Tests.Services.Guild;

public class EfGuildConfigurationServiceTests : EfTestBase
{
    private readonly EfGuildConfigurationService _sut;

    public EfGuildConfigurationServiceTests()
    {
        var logger = Substitute.For<ILogger<EfGuildConfigurationService>>();
        _sut = new EfGuildConfigurationService(Factory, logger);
    }

    [Fact]
    public async Task GetConfigurationAsync_WhenNoConfigExists_ReturnsNull()
    {
        var result = await _sut.GetConfigurationAsync(12345UL);

        result.Should().BeNull();
    }

    [Fact]
    public async Task SetChannelsAsync_WithValidData_StoresConfiguration()
    {
        const ulong guildId = 12345UL;
        const ulong textChannelId = 67890UL;
        const ulong voiceChannelId = 11111UL;

        await _sut.SetChannelsAsync(guildId, textChannelId, voiceChannelId);

        var result = await _sut.GetConfigurationAsync(guildId);
        result.Should().NotBeNull();
        result!.GuildId.Should().Be(guildId);
        result.TextChannelId.Should().Be(textChannelId);
        result.VoiceChannelId.Should().Be(voiceChannelId);
    }

    [Fact]
    public async Task SetChannelsAsync_WhenCalledTwice_OverwritesPreviousConfiguration()
    {
        const ulong guildId = 12345UL;

        await _sut.SetChannelsAsync(guildId, 111UL, 222UL);
        await _sut.SetChannelsAsync(guildId, 333UL, 444UL);

        var result = await _sut.GetConfigurationAsync(guildId);
        result.Should().NotBeNull();
        result!.TextChannelId.Should().Be(333UL);
        result.VoiceChannelId.Should().Be(444UL);
    }

    [Fact]
    public async Task SetChannelsAsync_SetsUpdatedAtToRecentTime()
    {
        var beforeSet = DateTimeOffset.UtcNow;

        await _sut.SetChannelsAsync(12345UL, 111UL, 222UL);

        var afterSet = DateTimeOffset.UtcNow;
        var result = await _sut.GetConfigurationAsync(12345UL);
        result.Should().NotBeNull();
        result!.UpdatedAt.Should().BeOnOrAfter(beforeSet.AddSeconds(-1));
        result.UpdatedAt.Should().BeOnOrBefore(afterSet.AddSeconds(1));
    }

    [Fact]
    public async Task SetChannelsAsync_WithVoiceId_StoresVoiceId()
    {
        await _sut.SetChannelsAsync(12345UL, 111UL, 222UL, "custom-voice");

        var result = await _sut.GetConfigurationAsync(12345UL);
        result.Should().NotBeNull();
        result!.VoiceId.Should().Be("custom-voice");
    }

    [Fact]
    public async Task RemoveConfigurationAsync_WhenConfigExists_RemovesConfiguration()
    {
        const ulong guildId = 12345UL;
        await _sut.SetChannelsAsync(guildId, 111UL, 222UL);

        await _sut.RemoveConfigurationAsync(guildId);

        var result = await _sut.GetConfigurationAsync(guildId);
        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveConfigurationAsync_WhenConfigDoesNotExist_DoesNotThrow()
    {
        var act = async () => await _sut.RemoveConfigurationAsync(99999UL);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetAllConfigurationsAsync_WhenEmpty_ReturnsEmptyCollection()
    {
        var result = await _sut.GetAllConfigurationsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllConfigurationsAsync_WithMultipleGuilds_ReturnsAllConfigurations()
    {
        await _sut.SetChannelsAsync(111UL, 1UL, 2UL);
        await _sut.SetChannelsAsync(222UL, 3UL, 4UL);
        await _sut.SetChannelsAsync(333UL, 5UL, 6UL);

        var result = await _sut.GetAllConfigurationsAsync();

        result.Should().HaveCount(3);
        result.Select(c => c.GuildId).Should().BeEquivalentTo([111UL, 222UL, 333UL]);
    }

    [Fact]
    public async Task GetConfigurationAsync_ReturnsCorrectConfigForSpecificGuild()
    {
        await _sut.SetChannelsAsync(111UL, 10UL, 20UL);
        await _sut.SetChannelsAsync(222UL, 30UL, 40UL);

        var result = await _sut.GetConfigurationAsync(111UL);

        result.Should().NotBeNull();
        result!.GuildId.Should().Be(111UL);
        result.TextChannelId.Should().Be(10UL);
        result.VoiceChannelId.Should().Be(20UL);
    }

    [Fact]
    public async Task SetTtsProviderAsync_SetsProviderOnExistingConfig()
    {
        await _sut.SetChannelsAsync(111UL, 10UL, 20UL);

        await _sut.SetTtsProviderAsync(111UL, "OpenAI");

        var result = await _sut.GetConfigurationAsync(111UL);
        result.Should().NotBeNull();
        result!.TtsProvider.Should().Be("OpenAI");
    }

    [Fact]
    public async Task SetTtsProviderAsync_CreatesConfigIfNoneExists()
    {
        await _sut.SetTtsProviderAsync(111UL, "OpenAI");

        var result = await _sut.GetConfigurationAsync(111UL);
        result.Should().NotBeNull();
        result!.TtsProvider.Should().Be("OpenAI");
    }

    [Fact]
    public async Task SetTtsProviderAsync_WithNull_ClearsProvider()
    {
        await _sut.SetChannelsAsync(111UL, 10UL, 20UL);
        await _sut.SetTtsProviderAsync(111UL, "OpenAI");

        await _sut.SetTtsProviderAsync(111UL, null);

        var result = await _sut.GetConfigurationAsync(111UL);
        result.Should().NotBeNull();
        result!.TtsProvider.Should().BeNull();
    }
}
