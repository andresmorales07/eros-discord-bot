using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Exceptions;
using ErosTTS.Bot.HostedServices;
using ErosTTS.Bot.Services.Audio;
using ErosTTS.Bot.Services.Guild;
using ErosTTS.Bot.Services.Queue;
using ErosTTS.Bot.Services.TTS;

namespace ErosTTS.Bot.Tests.HostedServices;

public sealed class TtsProcessorServiceTests
{
    private readonly ITtsQueue _queue = Substitute.For<ITtsQueue>();
    private readonly ITtsProviderFactory _providerFactory = Substitute.For<ITtsProviderFactory>();
    private readonly ITtsProvider _ttsProvider = Substitute.For<ITtsProvider>();
    private readonly IAudioService _audioService = Substitute.For<IAudioService>();
    private readonly IGuildConfigurationService _guildConfig = Substitute.For<IGuildConfigurationService>();
    private readonly ILogger<TtsProcessorService> _logger = Substitute.For<ILogger<TtsProcessorService>>();

    public TtsProcessorServiceTests()
    {
        _ttsProvider.ProviderName.Returns("ElevenLabs");
        _providerFactory.GetProviderAsync(Arg.Any<ulong>()).Returns(_ttsProvider);
    }

    private TtsProcessorService CreateService(int maxRetries = 3)
    {
        var config = Options.Create(new ElevenLabsConfiguration
        {
            ApiKey = "test-key",
            MaxRetries = maxRetries
        });

        return new TtsProcessorService(
            _queue,
            _providerFactory,
            _audioService,
            _guildConfig,
            null!,
            config,
            _logger);
    }

    private static TtsQueueItem CreateTestItem(ulong guildId = 100, int retryCount = 0, string? voiceId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            GuildId = guildId,
            TextChannelId = 200,
            VoiceChannelId = 300,
            Text = "Hello world",
            Username = "TestUser",
            VoiceId = voiceId,
            RetryCount = retryCount
        };

    // ─── ProcessItemAsync ───────────────────────────────────────────────

    [Fact]
    public async Task ProcessItemAsync_NoGuildConfig_SkipsItem()
    {
        var sut = CreateService();
        _guildConfig.GetConfigurationAsync(Arg.Any<ulong>()).Returns((GuildTtsConfiguration?)null);

        await sut.ProcessItemAsync(CreateTestItem(), CancellationToken.None);

        await _ttsProvider.DidNotReceive().SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _audioService.DidNotReceive().PlayAudioAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessItemAsync_NoVoiceChannel_SkipsItem()
    {
        var sut = CreateService();
        _guildConfig.GetConfigurationAsync(Arg.Any<ulong>()).Returns(new GuildTtsConfiguration
        {
            GuildId = 100,
            TextChannelId = 200,
            VoiceChannelId = null
        });

        await sut.ProcessItemAsync(CreateTestItem(), CancellationToken.None);

        await _ttsProvider.DidNotReceive().SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessItemAsync_UsesNpcVoiceIdOverride()
    {
        var sut = CreateService();
        _guildConfig.GetConfigurationAsync(Arg.Any<ulong>()).Returns(new GuildTtsConfiguration
        {
            GuildId = 100,
            VoiceChannelId = 300,
            VoiceId = "guild-voice"
        });
        _ttsProvider.SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new MemoryStream([1, 2, 3]));

        var item = CreateTestItem(voiceId: "npc-voice");

        await sut.ProcessItemAsync(item, CancellationToken.None);

        await _ttsProvider.Received(1).SynthesizeAsync(item.Text, "npc-voice", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessItemAsync_FallsBackToGuildVoiceId()
    {
        var sut = CreateService();
        _guildConfig.GetConfigurationAsync(Arg.Any<ulong>()).Returns(new GuildTtsConfiguration
        {
            GuildId = 100,
            VoiceChannelId = 300,
            VoiceId = "guild-voice"
        });
        _ttsProvider.SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new MemoryStream([1, 2, 3]));

        var item = CreateTestItem(voiceId: null);

        await sut.ProcessItemAsync(item, CancellationToken.None);

        await _ttsProvider.Received(1).SynthesizeAsync(item.Text, "guild-voice", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessItemAsync_PlaysAudioInCorrectChannel()
    {
        var sut = CreateService();
        _guildConfig.GetConfigurationAsync(Arg.Any<ulong>()).Returns(new GuildTtsConfiguration
        {
            GuildId = 100,
            VoiceChannelId = 300
        });
        var audioStream = new MemoryStream([1, 2, 3]);
        _ttsProvider.SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(audioStream);

        var item = CreateTestItem();

        await sut.ProcessItemAsync(item, CancellationToken.None);

        await _audioService.Received(1).PlayAudioAsync(100, 300, audioStream, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessItemAsync_DisposesAudioStreamOnSuccess()
    {
        var sut = CreateService();
        _guildConfig.GetConfigurationAsync(Arg.Any<ulong>()).Returns(new GuildTtsConfiguration
        {
            GuildId = 100,
            VoiceChannelId = 300
        });
        var audioStream = Substitute.For<Stream>();
        audioStream.DisposeAsync().Returns(ValueTask.CompletedTask);
        _ttsProvider.SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(audioStream);

        await sut.ProcessItemAsync(CreateTestItem(), CancellationToken.None);

        await audioStream.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task ProcessItemAsync_DisposesAudioStreamOnFailure()
    {
        var sut = CreateService();
        _guildConfig.GetConfigurationAsync(Arg.Any<ulong>()).Returns(new GuildTtsConfiguration
        {
            GuildId = 100,
            VoiceChannelId = 300
        });
        var audioStream = Substitute.For<Stream>();
        audioStream.DisposeAsync().Returns(ValueTask.CompletedTask);
        _ttsProvider.SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(audioStream);
        _audioService.PlayAudioAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new VoiceConnectionException("test")));

        var act = () => sut.ProcessItemAsync(CreateTestItem(), CancellationToken.None);

        await act.Should().ThrowAsync<VoiceConnectionException>();
        await audioStream.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task ProcessItemAsync_ResolvesProviderForGuild()
    {
        var sut = CreateService();
        _guildConfig.GetConfigurationAsync(Arg.Any<ulong>()).Returns(new GuildTtsConfiguration
        {
            GuildId = 100,
            VoiceChannelId = 300
        });
        _ttsProvider.SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new MemoryStream([1, 2, 3]));

        var item = CreateTestItem(guildId: 100);

        await sut.ProcessItemAsync(item, CancellationToken.None);

        await _providerFactory.Received(1).GetProviderAsync(100);
    }

    // ─── HandleFailureAsync ─────────────────────────────────────────────

    [Fact]
    public async Task HandleFailureAsync_RateLimit_RequeuesWithIncrementedRetryCount()
    {
        var sut = CreateService(maxRetries: 3);
        var item = CreateTestItem(retryCount: 0);
        var ex = new RateLimitException("Rate limited", TimeSpan.Zero);

        await sut.HandleFailureAsync(item, ex, CancellationToken.None);

        item.RetryCount.Should().Be(1);
        await _queue.Received(1).EnqueueAsync(item, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleFailureAsync_RateLimit_ExceedsMaxRetries_DoesNotRequeue()
    {
        var sut = CreateService(maxRetries: 3);
        var item = CreateTestItem(retryCount: 3);
        var ex = new RateLimitException("Rate limited", TimeSpan.Zero);

        await sut.HandleFailureAsync(item, ex, CancellationToken.None);

        await _queue.DidNotReceive().EnqueueAsync(Arg.Any<TtsQueueItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleFailureAsync_AuthenticationException_DoesNotRequeue()
    {
        var sut = CreateService();
        var item = CreateTestItem(retryCount: 0);
        var ex = new AuthenticationException("Bad key");

        await sut.HandleFailureAsync(item, ex, CancellationToken.None);

        await _queue.DidNotReceive().EnqueueAsync(Arg.Any<TtsQueueItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleFailureAsync_InvalidTextException_DoesNotRequeue()
    {
        var sut = CreateService();
        var item = CreateTestItem(retryCount: 0);
        var ex = new InvalidTextException("Bad text");

        await sut.HandleFailureAsync(item, ex, CancellationToken.None);

        await _queue.DidNotReceive().EnqueueAsync(Arg.Any<TtsQueueItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleFailureAsync_VoiceConnectionException_RequeuesOnce()
    {
        var sut = CreateService();
        var item = CreateTestItem(retryCount: 0);
        var ex = new VoiceConnectionException("Connection failed");

        await sut.HandleFailureAsync(item, ex, CancellationToken.None);

        item.RetryCount.Should().Be(1);
        await _queue.Received(1).EnqueueAsync(item, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleFailureAsync_VoiceConnectionException_AlreadyRetried_DoesNotRequeue()
    {
        var sut = CreateService();
        var item = CreateTestItem(retryCount: 1);
        var ex = new VoiceConnectionException("Connection failed");

        await sut.HandleFailureAsync(item, ex, CancellationToken.None);

        await _queue.DidNotReceive().EnqueueAsync(Arg.Any<TtsQueueItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleFailureAsync_GenericException_RequeuesWithExponentialBackoff()
    {
        var sut = CreateService(maxRetries: 3);
        var item = CreateTestItem(retryCount: 0);
        var ex = new Exception("Something went wrong");

        await sut.HandleFailureAsync(item, ex, CancellationToken.None);

        item.RetryCount.Should().Be(1);
        await _queue.Received(1).EnqueueAsync(item, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleFailureAsync_GenericException_ExceedsMaxRetries_DoesNotRequeue()
    {
        var sut = CreateService(maxRetries: 3);
        var item = CreateTestItem(retryCount: 3);
        var ex = new Exception("Something went wrong");

        await sut.HandleFailureAsync(item, ex, CancellationToken.None);

        await _queue.DidNotReceive().EnqueueAsync(Arg.Any<TtsQueueItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleFailureAsync_GenericException_IncrementalRetryCount()
    {
        var sut = CreateService(maxRetries: 5);
        var item = CreateTestItem(retryCount: 2);
        var ex = new Exception("Error");

        await sut.HandleFailureAsync(item, ex, CancellationToken.None);

        item.RetryCount.Should().Be(3);
        await _queue.Received(1).EnqueueAsync(item, Arg.Any<CancellationToken>());
    }
}
