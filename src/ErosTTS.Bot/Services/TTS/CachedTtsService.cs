using System.Security.Cryptography;
using System.Text;
using ErosTTS.Bot.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErosTTS.Bot.Services.TTS;

/// <summary>
/// Decorator around an <see cref="ITtsProvider"/> that caches synthesized audio on disk.
/// </summary>
public sealed class CachedTtsService : ITtsProvider
{
    private readonly ITtsProvider _inner;
    private readonly TtsCacheConfiguration _cacheConfig;
    private readonly ILogger<CachedTtsService> _logger;

    public string ProviderName => _inner.ProviderName;
    public string DefaultVoiceId => _inner.DefaultVoiceId;
    public string ModelId => _inner.ModelId;
    public string OutputFormat => _inner.OutputFormat;

    public CachedTtsService(
        ITtsProvider inner,
        IOptions<TtsCacheConfiguration> cacheConfig,
        ILogger<CachedTtsService> logger)
    {
        _inner = inner;
        _cacheConfig = cacheConfig.Value;
        _logger = logger;
    }

    public async Task<Stream> SynthesizeAsync(string text, string? voiceId = null, CancellationToken ct = default)
    {
        if (!_cacheConfig.Enabled)
        {
            return await _inner.SynthesizeAsync(text, voiceId, ct);
        }

        var effectiveVoiceId = voiceId ?? _inner.DefaultVoiceId;
        var cacheKey = ComputeCacheKey(_inner.ProviderName, text, effectiveVoiceId, _inner.ModelId, _inner.OutputFormat);
        var cachePath = Path.Combine(_cacheConfig.CacheDirectory, $"{cacheKey}.mp3");

        if (File.Exists(cachePath))
        {
            _logger.LogDebug("TTS cache hit for key {CacheKey}", cacheKey);
            var cached = await File.ReadAllBytesAsync(cachePath, ct);
            return new MemoryStream(cached);
        }

        _logger.LogDebug("TTS cache miss for key {CacheKey}", cacheKey);

        await using var stream = await _inner.SynthesizeAsync(text, voiceId, ct);

        // Read into a byte array for both caching and returning
        var audioBytes = new MemoryStream();
        await stream.CopyToAsync(audioBytes, ct);

        // Save to cache
        Directory.CreateDirectory(_cacheConfig.CacheDirectory);
        await File.WriteAllBytesAsync(cachePath, audioBytes.ToArray(), ct);

        audioBytes.Position = 0;
        return audioBytes;
    }

    public Task<bool> ValidateApiKeyAsync(CancellationToken ct = default)
    {
        return _inner.ValidateApiKeyAsync(ct);
    }

    internal static string ComputeCacheKey(string providerName, string text, string voiceId, string modelId, string outputFormat)
    {
        var input = $"{providerName}|{text}|{voiceId}|{modelId}|{outputFormat}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
