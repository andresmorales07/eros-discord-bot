using ErosTTS.Bot.HostedServices;
using ErosTTS.Bot.Services.Audio;

namespace ErosTTS.Bot.Tests.HostedServices;

public class VoiceInactivityHostedServiceTests : IDisposable
{
    private const ulong GuildId = 111UL;
    private const ulong ChannelId = 222UL;
    private static readonly TimeSpan TestDelay = TimeSpan.FromMilliseconds(50);

    private readonly IVoiceChannelInspector _inspector;
    private readonly ILogger<VoiceInactivityHostedService> _logger;
    private readonly VoiceInactivityHostedService _service;

    public VoiceInactivityHostedServiceTests()
    {
        _inspector = Substitute.For<IVoiceChannelInspector>();
        _logger = Substitute.For<ILogger<VoiceInactivityHostedService>>();
        _service = new VoiceInactivityHostedService(
            _inspector, _logger, gatewayClient: null, disconnectDelay: TestDelay,
            pollingInterval: Timeout.InfiniteTimeSpan);
    }

    public void Dispose()
    {
        _service.Dispose();
    }

    [Fact]
    public void HandleVoiceStateChange_BotNotConnected_NoTimerStarted()
    {
        _inspector.IsBotConnected(GuildId).Returns(false);

        _service.HandleVoiceStateChange(GuildId);

        _service.HasPendingTimer(GuildId).Should().BeFalse();
        _inspector.DidNotReceive().GetBotVoiceChannelId(GuildId);
    }

    [Fact]
    public void HandleVoiceStateChange_ChannelHasUsers_NoTimerStarted()
    {
        _inspector.IsBotConnected(GuildId).Returns(true);
        _inspector.GetBotVoiceChannelId(GuildId).Returns(ChannelId);
        _inspector.CountNonBotUsersInChannel(GuildId, ChannelId).Returns(2);

        _service.HandleVoiceStateChange(GuildId);

        _service.HasPendingTimer(GuildId).Should().BeFalse();
    }

    [Fact]
    public void HandleVoiceStateChange_ChannelEmpty_TimerStarted()
    {
        _inspector.IsBotConnected(GuildId).Returns(true);
        _inspector.GetBotVoiceChannelId(GuildId).Returns(ChannelId);
        _inspector.CountNonBotUsersInChannel(GuildId, ChannelId).Returns(0);

        _service.HandleVoiceStateChange(GuildId);

        _service.HasPendingTimer(GuildId).Should().BeTrue();
    }

    [Fact]
    public async Task HandleVoiceStateChange_ChannelEmpty_DisconnectsAfterTimeout()
    {
        _inspector.IsBotConnected(GuildId).Returns(true);
        _inspector.GetBotVoiceChannelId(GuildId).Returns(ChannelId);
        _inspector.CountNonBotUsersInChannel(GuildId, ChannelId).Returns(0);
        _inspector.DisconnectBotAsync(GuildId).Returns(Task.CompletedTask);

        _service.HandleVoiceStateChange(GuildId);

        // Wait for the timer to fire (TestDelay is 50ms, allow some margin)
        await Task.Delay(TestDelay + TimeSpan.FromMilliseconds(200));

        await _inspector.Received(1).DisconnectBotAsync(GuildId);
        _service.HasPendingTimer(GuildId).Should().BeFalse();
    }

    [Fact]
    public async Task HandleVoiceStateChange_UserRejoinsBeforeTimeout_DisconnectNotCalled()
    {
        _inspector.IsBotConnected(GuildId).Returns(true);
        _inspector.GetBotVoiceChannelId(GuildId).Returns(ChannelId);
        _inspector.CountNonBotUsersInChannel(GuildId, ChannelId).Returns(0);

        // Channel goes empty → timer starts
        _service.HandleVoiceStateChange(GuildId);
        _service.HasPendingTimer(GuildId).Should().BeTrue();

        // User rejoins → timer cancelled
        _inspector.CountNonBotUsersInChannel(GuildId, ChannelId).Returns(1);
        _service.HandleVoiceStateChange(GuildId);
        _service.HasPendingTimer(GuildId).Should().BeFalse();

        // Wait past the original timeout
        await Task.Delay(TestDelay + TimeSpan.FromMilliseconds(200));

        await _inspector.DidNotReceive().DisconnectBotAsync(GuildId);
    }

    [Fact]
    public void HandleVoiceStateChange_MultipleEmptyEvents_OnlyOneTimer()
    {
        _inspector.IsBotConnected(GuildId).Returns(true);
        _inspector.GetBotVoiceChannelId(GuildId).Returns(ChannelId);
        _inspector.CountNonBotUsersInChannel(GuildId, ChannelId).Returns(0);

        _service.HandleVoiceStateChange(GuildId);
        _service.HandleVoiceStateChange(GuildId);
        _service.HandleVoiceStateChange(GuildId);

        // Only one timer should exist
        _service.HasPendingTimer(GuildId).Should().BeTrue();
    }

    [Fact]
    public async Task HandleVoiceStateChange_ReVerifyChannelRefilled_NoDisconnect()
    {
        // Initially empty
        _inspector.IsBotConnected(GuildId).Returns(true);
        _inspector.GetBotVoiceChannelId(GuildId).Returns(ChannelId);
        _inspector.CountNonBotUsersInChannel(GuildId, ChannelId).Returns(0);

        _service.StartTimer(GuildId);

        // Simulate user joining just before timer fires (re-verify will see 1 user)
        // Wait a bit, then change the return value before timer fires
        await Task.Delay(TestDelay / 2);
        _inspector.CountNonBotUsersInChannel(GuildId, ChannelId).Returns(1);

        // Wait for timer to fire
        await Task.Delay(TestDelay + TimeSpan.FromMilliseconds(200));

        // Re-verify should have found users and skipped disconnect
        await _inspector.DidNotReceive().DisconnectBotAsync(GuildId);
    }

    [Fact]
    public async Task HandleVoiceStateChange_BotDisconnectedBeforeTimerFires_NoDisconnect()
    {
        _inspector.IsBotConnected(GuildId).Returns(true);
        _inspector.GetBotVoiceChannelId(GuildId).Returns(ChannelId);
        _inspector.CountNonBotUsersInChannel(GuildId, ChannelId).Returns(0);

        _service.StartTimer(GuildId);

        // Bot disconnects (e.g. /tts-stop) before timer fires
        await Task.Delay(TestDelay / 2);
        _inspector.IsBotConnected(GuildId).Returns(false);
        _inspector.GetBotVoiceChannelId(GuildId).Returns((ulong?)null);

        await Task.Delay(TestDelay + TimeSpan.FromMilliseconds(200));

        await _inspector.DidNotReceive().DisconnectBotAsync(GuildId);
    }

    [Fact]
    public async Task IndependentTimersPerGuild()
    {
        const ulong guild1 = 100UL;
        const ulong guild2 = 200UL;
        const ulong channel1 = 1001UL;
        const ulong channel2 = 2001UL;

        _inspector.IsBotConnected(guild1).Returns(true);
        _inspector.GetBotVoiceChannelId(guild1).Returns(channel1);
        _inspector.CountNonBotUsersInChannel(guild1, channel1).Returns(0);
        _inspector.DisconnectBotAsync(guild1).Returns(Task.CompletedTask);

        _inspector.IsBotConnected(guild2).Returns(true);
        _inspector.GetBotVoiceChannelId(guild2).Returns(channel2);
        _inspector.CountNonBotUsersInChannel(guild2, channel2).Returns(0);
        _inspector.DisconnectBotAsync(guild2).Returns(Task.CompletedTask);

        // Start timers for both guilds
        _service.HandleVoiceStateChange(guild1);
        _service.HandleVoiceStateChange(guild2);

        _service.HasPendingTimer(guild1).Should().BeTrue();
        _service.HasPendingTimer(guild2).Should().BeTrue();

        // Cancel guild1 timer (user rejoins)
        _inspector.CountNonBotUsersInChannel(guild1, channel1).Returns(1);
        _service.HandleVoiceStateChange(guild1);

        _service.HasPendingTimer(guild1).Should().BeFalse();
        _service.HasPendingTimer(guild2).Should().BeTrue();

        // Wait for guild2 timer to fire
        await Task.Delay(TestDelay + TimeSpan.FromMilliseconds(200));

        await _inspector.DidNotReceive().DisconnectBotAsync(guild1);
        await _inspector.Received(1).DisconnectBotAsync(guild2);
    }

    [Fact]
    public void HandleVoiceStateChange_BotChannelIdNull_NoTimerStarted()
    {
        _inspector.IsBotConnected(GuildId).Returns(true);
        _inspector.GetBotVoiceChannelId(GuildId).Returns((ulong?)null);

        _service.HandleVoiceStateChange(GuildId);

        _service.HasPendingTimer(GuildId).Should().BeFalse();
    }

    [Fact]
    public void CancelTimer_WhenNoTimerExists_DoesNotThrow()
    {
        // Should not throw
        _service.CancelTimer(GuildId);

        _service.HasPendingTimer(GuildId).Should().BeFalse();
    }

    [Fact]
    public async Task Dispose_CancelsAllPendingTimers()
    {
        _inspector.IsBotConnected(GuildId).Returns(true);
        _inspector.GetBotVoiceChannelId(GuildId).Returns(ChannelId);
        _inspector.CountNonBotUsersInChannel(GuildId, ChannelId).Returns(0);

        _service.HandleVoiceStateChange(GuildId);
        _service.HasPendingTimer(GuildId).Should().BeTrue();

        _service.Dispose();

        await Task.Delay(TestDelay + TimeSpan.FromMilliseconds(200));

        await _inspector.DidNotReceive().DisconnectBotAsync(GuildId);
    }

    [Fact]
    public void HandleVoiceStateChange_BotNotConnected_CancelsExistingTimer()
    {
        // First: bot connected, channel empty → timer starts
        _inspector.IsBotConnected(GuildId).Returns(true);
        _inspector.GetBotVoiceChannelId(GuildId).Returns(ChannelId);
        _inspector.CountNonBotUsersInChannel(GuildId, ChannelId).Returns(0);

        _service.HandleVoiceStateChange(GuildId);
        _service.HasPendingTimer(GuildId).Should().BeTrue();

        // Then: bot disconnects → stale timer should be cancelled
        _inspector.IsBotConnected(GuildId).Returns(false);
        _service.HandleVoiceStateChange(GuildId);

        _service.HasPendingTimer(GuildId).Should().BeFalse();
    }

    [Fact]
    public async Task Poll_DiscoversEmptyChannel_StartsTimer()
    {
        var pollInterval = TimeSpan.FromMilliseconds(50);
        var inspector = Substitute.For<IVoiceChannelInspector>();
        var logger = Substitute.For<ILogger<VoiceInactivityHostedService>>();
        var service = new VoiceInactivityHostedService(
            inspector, logger, gatewayClient: null,
            disconnectDelay: TimeSpan.FromSeconds(30),
            pollingInterval: pollInterval);

        inspector.GetConnectedGuildIds().Returns(new List<ulong> { GuildId });
        inspector.IsBotConnected(GuildId).Returns(true);
        inspector.GetBotVoiceChannelId(GuildId).Returns(ChannelId);
        inspector.CountNonBotUsersInChannel(GuildId, ChannelId).Returns(0);

        await service.StartAsync(CancellationToken.None);

        // Wait for at least one poll cycle
        await Task.Delay(pollInterval + TimeSpan.FromMilliseconds(100));

        service.HasPendingTimer(GuildId).Should().BeTrue();

        await service.StopAsync(CancellationToken.None);
        service.Dispose();
    }

    [Fact]
    public async Task Poll_ChannelHasUsers_NoTimerStarted()
    {
        var pollInterval = TimeSpan.FromMilliseconds(50);
        var inspector = Substitute.For<IVoiceChannelInspector>();
        var logger = Substitute.For<ILogger<VoiceInactivityHostedService>>();
        var service = new VoiceInactivityHostedService(
            inspector, logger, gatewayClient: null,
            disconnectDelay: TimeSpan.FromSeconds(30),
            pollingInterval: pollInterval);

        inspector.GetConnectedGuildIds().Returns(new List<ulong> { GuildId });
        inspector.IsBotConnected(GuildId).Returns(true);
        inspector.GetBotVoiceChannelId(GuildId).Returns(ChannelId);
        inspector.CountNonBotUsersInChannel(GuildId, ChannelId).Returns(2);

        await service.StartAsync(CancellationToken.None);

        // Wait for at least one poll cycle
        await Task.Delay(pollInterval + TimeSpan.FromMilliseconds(100));

        service.HasPendingTimer(GuildId).Should().BeFalse();

        await service.StopAsync(CancellationToken.None);
        service.Dispose();
    }
}
