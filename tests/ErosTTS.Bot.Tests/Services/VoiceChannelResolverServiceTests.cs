using ErosTTS.Bot.Services;
using ErosTTS.Bot.Services.Audio;
using ErosTTS.Bot.Services.Guild;

namespace ErosTTS.Bot.Tests.Services;

public sealed class VoiceChannelResolverServiceTests
{
    private const ulong GuildId = 100;
    private const ulong UserId = 500;

    private readonly IVoiceChannelInspector _inspector = Substitute.For<IVoiceChannelInspector>();
    private readonly IGuildConfigurationService _guildConfig = Substitute.For<IGuildConfigurationService>();

    private VoiceChannelResolverService CreateService() => new(_inspector, _guildConfig);

    [Fact]
    public async Task ResolveVoiceChannelAsync_ExplicitChannel_ReturnsExplicitChannel()
    {
        var sut = CreateService();

        var result = await sut.ResolveVoiceChannelAsync(GuildId, UserId, explicitChannelId: 42);

        result.Should().Be(42UL);
        // Should not need to check user voice state or guild config
        _inspector.DidNotReceive().GetUserVoiceChannelId(Arg.Any<ulong>(), Arg.Any<ulong>());
        await _guildConfig.DidNotReceive().GetConfigurationAsync(Arg.Any<ulong>());
    }

    [Fact]
    public async Task ResolveVoiceChannelAsync_NoExplicit_UserInChannel_ReturnsUserChannel()
    {
        var sut = CreateService();
        _inspector.GetUserVoiceChannelId(GuildId, UserId).Returns((ulong?)300);

        var result = await sut.ResolveVoiceChannelAsync(GuildId, UserId, explicitChannelId: null);

        result.Should().Be(300UL);
        // Should not need to check guild config
        await _guildConfig.DidNotReceive().GetConfigurationAsync(Arg.Any<ulong>());
    }

    [Fact]
    public async Task ResolveVoiceChannelAsync_NoExplicit_UserNotInChannel_FallsBackToGuildDefault()
    {
        var sut = CreateService();
        _inspector.GetUserVoiceChannelId(GuildId, UserId).Returns((ulong?)null);
        _guildConfig.GetConfigurationAsync(GuildId).Returns(new GuildTtsConfiguration
        {
            GuildId = GuildId,
            VoiceChannelId = 400
        });

        var result = await sut.ResolveVoiceChannelAsync(GuildId, UserId, explicitChannelId: null);

        result.Should().Be(400UL);
    }

    [Fact]
    public async Task ResolveVoiceChannelAsync_NoExplicit_UserNotInChannel_NoGuildDefault_ReturnsNull()
    {
        var sut = CreateService();
        _inspector.GetUserVoiceChannelId(GuildId, UserId).Returns((ulong?)null);
        _guildConfig.GetConfigurationAsync(GuildId).Returns((GuildTtsConfiguration?)null);

        var result = await sut.ResolveVoiceChannelAsync(GuildId, UserId, explicitChannelId: null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveVoiceChannelAsync_GuildConfigWithNoVoiceChannel_ReturnsNull()
    {
        var sut = CreateService();
        _inspector.GetUserVoiceChannelId(GuildId, UserId).Returns((ulong?)null);
        _guildConfig.GetConfigurationAsync(GuildId).Returns(new GuildTtsConfiguration
        {
            GuildId = GuildId,
            VoiceChannelId = null
        });

        var result = await sut.ResolveVoiceChannelAsync(GuildId, UserId, explicitChannelId: null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveVoiceChannelAsync_ExplicitZero_ReturnsZero()
    {
        // A channel ID of 0 is technically invalid but should be passed through if explicitly provided
        var sut = CreateService();

        var result = await sut.ResolveVoiceChannelAsync(GuildId, UserId, explicitChannelId: 0);

        result.Should().Be(0UL);
    }

    [Fact]
    public async Task ResolveVoiceChannelAsync_PrioritizesExplicitOverUserChannel()
    {
        var sut = CreateService();
        _inspector.GetUserVoiceChannelId(GuildId, UserId).Returns((ulong?)300);

        var result = await sut.ResolveVoiceChannelAsync(GuildId, UserId, explicitChannelId: 42);

        result.Should().Be(42UL);
    }

    [Fact]
    public async Task ResolveVoiceChannelAsync_PrioritizesUserChannelOverGuildDefault()
    {
        var sut = CreateService();
        _inspector.GetUserVoiceChannelId(GuildId, UserId).Returns((ulong?)300);
        _guildConfig.GetConfigurationAsync(GuildId).Returns(new GuildTtsConfiguration
        {
            GuildId = GuildId,
            VoiceChannelId = 400
        });

        var result = await sut.ResolveVoiceChannelAsync(GuildId, UserId, explicitChannelId: null);

        result.Should().Be(300UL);
    }
}
