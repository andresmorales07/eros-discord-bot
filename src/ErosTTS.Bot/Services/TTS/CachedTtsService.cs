using System.Security.Cryptography;
using System.Text;
using ErosTTS.Bot.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErosTTS.Bot.Services.TTS;

/// <summary>
/// Decorator around <see cref="ElevenLabsTtsService"/> that caches synthesized audio on disk.
/// </summary>
public sealed class CachedTtsService : ITtsService
{
    private readonly ITtsService _inner;
    private readonly TtsCacheConfiguration _cacheConfig;
    private readonly ElevenLabsConfiguration _elevenLabsConfig;
    private readonly ILogger<CachedTtsService> _logger;

    public CachedTtsService(
        ITtsService inner,
        IOptions<TtsCacheConfiguration> cacheConfig,
        IOptions<ElevenLabsConfiguration> elevenLabsConfig,
        ILogger<CachedTtsService> logger)
    {
        _inner = inner;
        _cacheConfig = cacheConfig.Value;
        _elevenLabsConfig = elevenLabsConfig.Value;
        _logger = logger;
    }

    public async Task<Stream> SynthesizeAsync(string text, string? voiceId = null, CancellationToken ct = default)
    {
        if (!_cacheConfig.Enabled)
        {
            return await _inner.SynthesizeAsync(text, voiceId, ct);
        }

        var effectiveVoiceId = voiceId ?? _elevenLabsConfig.VoiceId;
        var cacheKey = ComputeCacheKey(text, effectiveVoiceId, _elevenLabsConfig.ModelId);
        var cachePath = Path.Combine(_cacheConfig.CacheDirectory, $"{cacheKey}.mp3");

        if (File.Exists(cachePath))
        {
            _logger.LogDebug("TTS cache hit for key {CacheKey}", cacheKey);
            var cached = await File.ReadAllBytesAsync(cachePath, ct);
            return new MemoryStream(cached);
        }

        _logger.LogDebug("TTS cache miss for key {CacheKey}", cacheKey);

        var stream = await _inner.SynthesizeAsync(text, voiceId, ct);

        // Save to cache
        Directory.CreateDirectory(_cacheConfig.CacheDirectory);
        var audioBytes = ((MemoryStream)stream).ToArray();
        await File.WriteAllBytesAsync(cachePath, audioBytes, ct);
        stream.Position = 0;

        return stream;
    }

    public Task<bool> ValidateApiKeyAsync(CancellationToken ct = default)
    {
        return _inner.ValidateApiKeyAsync(ct);
    }

    internal static string ComputeCacheKey(string text, string voiceId, string modelId)
    {
        var input = $"{text}|{voiceId}|{modelId}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
