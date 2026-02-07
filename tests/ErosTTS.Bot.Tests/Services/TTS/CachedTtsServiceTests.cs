using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Services.TTS;

namespace ErosTTS.Bot.Tests.Services.TTS;

public class CachedTtsServiceTests : IDisposable
{
    private readonly string _cacheDir;
    private readonly ITtsService _innerService;
    private readonly IOptions<ElevenLabsConfiguration> _elevenLabsConfig;
    private readonly ILogger<CachedTtsService> _logger;

    public CachedTtsServiceTests()
    {
        _cacheDir = Path.Combine(Path.GetTempPath(), $"tts-cache-test-{Guid.NewGuid():N}");
        _innerService = Substitute.For<ITtsService>();
        _elevenLabsConfig = Options.Create(new ElevenLabsConfiguration
        {
            ApiKey = "test-key",
            VoiceId = "default-voice",
            ModelId = "eleven_turbo_v2_5"
        });
        _logger = Substitute.For<ILogger<CachedTtsService>>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir))
        {
            Directory.Delete(_cacheDir, true);
        }
    }

    private CachedTtsService CreateService(bool enabled = true)
    {
        var cacheConfig = Options.Create(new TtsCacheConfiguration
        {
            Enabled = enabled,
            CacheDirectory = _cacheDir
        });

        return new CachedTtsService(
            _innerService,
            cacheConfig,
            _elevenLabsConfig,
            _logger);
    }

    [Fact]
    public async Task SynthesizeAsync_CacheMiss_CallsInnerServiceAndWritesFile()
    {
        var audioData = new byte[] { 0x01, 0x02, 0x03 };
        _innerService.SynthesizeAsync("hello", null, Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(audioData));

        var service = CreateService();

        var result = await service.SynthesizeAsync("hello");

        result.Should().NotBeNull();
        result.Position.Should().Be(0);
        var buffer = new byte[3];
        await result.ReadAsync(buffer);
        buffer.Should().BeEquivalentTo(audioData);

        await _innerService.Received(1).SynthesizeAsync("hello", null, Arg.Any<CancellationToken>());

        // Verify file was written to disk
        var cacheKey = CachedTtsService.ComputeCacheKey("hello", "default-voice", "eleven_turbo_v2_5");
        var cachePath = Path.Combine(_cacheDir, $"{cacheKey}.mp3");
        File.Exists(cachePath).Should().BeTrue();
        var cachedBytes = await File.ReadAllBytesAsync(cachePath);
        cachedBytes.Should().BeEquivalentTo(audioData);
    }

    [Fact]
    public async Task SynthesizeAsync_CacheHit_ReturnsCachedFileWithoutCallingInner()
    {
        var audioData = new byte[] { 0xAA, 0xBB, 0xCC };
        var cacheKey = CachedTtsService.ComputeCacheKey("hello", "default-voice", "eleven_turbo_v2_5");
        var cachePath = Path.Combine(_cacheDir, $"{cacheKey}.mp3");

        Directory.CreateDirectory(_cacheDir);
        await File.WriteAllBytesAsync(cachePath, audioData);

        var service = CreateService();

        var result = await service.SynthesizeAsync("hello");

        result.Should().NotBeNull();
        result.Position.Should().Be(0);
        var buffer = new byte[3];
        await result.ReadAsync(buffer);
        buffer.Should().BeEquivalentTo(audioData);

        await _innerService.DidNotReceive().SynthesizeAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SynthesizeAsync_CacheDisabled_PassesThroughWithoutWritingFile()
    {
        var audioData = new byte[] { 0x01, 0x02, 0x03 };
        _innerService.SynthesizeAsync("hello", null, Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(audioData));

        var service = CreateService(enabled: false);

        var result = await service.SynthesizeAsync("hello");

        result.Should().NotBeNull();
        await _innerService.Received(1).SynthesizeAsync("hello", null, Arg.Any<CancellationToken>());

        // No files should have been written
        Directory.Exists(_cacheDir).Should().BeFalse();
    }

    [Fact]
    public async Task ValidateApiKeyAsync_DelegatesToInner()
    {
        _innerService.ValidateApiKeyAsync(Arg.Any<CancellationToken>()).Returns(true);

        var service = CreateService();

        var result = await service.ValidateApiKeyAsync();

        result.Should().BeTrue();
        await _innerService.Received(1).ValidateApiKeyAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ComputeCacheKey_SameInputs_ReturnsSameKey()
    {
        var key1 = CachedTtsService.ComputeCacheKey("hello", "voice1", "model1");
        var key2 = CachedTtsService.ComputeCacheKey("hello", "voice1", "model1");

        key1.Should().Be(key2);
    }

    [Fact]
    public void ComputeCacheKey_DifferentText_ReturnsDifferentKey()
    {
        var key1 = CachedTtsService.ComputeCacheKey("hello", "voice1", "model1");
        var key2 = CachedTtsService.ComputeCacheKey("world", "voice1", "model1");

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void ComputeCacheKey_DifferentVoice_ReturnsDifferentKey()
    {
        var key1 = CachedTtsService.ComputeCacheKey("hello", "voice1", "model1");
        var key2 = CachedTtsService.ComputeCacheKey("hello", "voice2", "model1");

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void ComputeCacheKey_DifferentModel_ReturnsDifferentKey()
    {
        var key1 = CachedTtsService.ComputeCacheKey("hello", "voice1", "model1");
        var key2 = CachedTtsService.ComputeCacheKey("hello", "voice1", "model2");

        key1.Should().NotBe(key2);
    }

    [Fact]
    public async Task SynthesizeAsync_WithVoiceIdOverride_UsesThatVoiceForCacheKey()
    {
        var audioData = new byte[] { 0x01 };
        _innerService.SynthesizeAsync("hello", "custom-voice", Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(audioData));

        var service = CreateService();

        await service.SynthesizeAsync("hello", "custom-voice");

        // File should be cached using the custom voice in the key
        var cacheKey = CachedTtsService.ComputeCacheKey("hello", "custom-voice", "eleven_turbo_v2_5");
        var cachePath = Path.Combine(_cacheDir, $"{cacheKey}.mp3");
        File.Exists(cachePath).Should().BeTrue();
    }
}
