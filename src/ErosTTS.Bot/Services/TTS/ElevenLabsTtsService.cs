using System.Net;
using System.Text;
using System.Text.Json;
using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErosTTS.Bot.Services.TTS;

/// <summary>
/// Eleven Labs API implementation of the TTS service.
/// </summary>
public sealed class ElevenLabsTtsService : ITtsService
{
    private readonly HttpClient _httpClient;
    private readonly ElevenLabsConfiguration _config;
    private readonly ILogger<ElevenLabsTtsService> _logger;

    private const string BaseUrl = "https://api.elevenlabs.io/v1";

    public ElevenLabsTtsService(
        HttpClient httpClient,
        IOptions<ElevenLabsConfiguration> config,
        ILogger<ElevenLabsTtsService> logger)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Add("xi-api-key", _config.ApiKey);
        _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
    }

    public async Task<Stream> SynthesizeAsync(string text, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/text-to-speech/{_config.VoiceId}";

        var payload = new
        {
            text,
            model_id = _config.ModelId,
            output_format = _config.OutputFormat,
            voice_settings = new
            {
                stability = _config.Stability,
                similarity_boost = _config.SimilarityBoost
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        _logger.LogDebug("Sending TTS request for {CharCount} characters to voice {VoiceId}",
            text.Length, _config.VoiceId);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync(url, content, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Request to Eleven Labs timed out after {Timeout}s",
                _config.TimeoutSeconds);
            throw new TtsServiceException("Request timed out", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error communicating with Eleven Labs API");
            throw new TtsServiceException("Network error", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("ElevenLabs API error: {StatusCode} - {Body}",
                response.StatusCode, errorBody);

            throw response.StatusCode switch
            {
                HttpStatusCode.TooManyRequests => new RateLimitException(
                    "ElevenLabs rate limit exceeded",
                    response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(60)),
                HttpStatusCode.Unauthorized => new AuthenticationException(
                    "Invalid ElevenLabs API key"),
                HttpStatusCode.UnprocessableEntity => new InvalidTextException(
                    $"Text processing error: {errorBody}"),
                HttpStatusCode.BadRequest => new InvalidTextException(
                    $"Bad request: {errorBody}"),
                _ => new TtsServiceException(
                    $"ElevenLabs API error: {response.StatusCode} - {errorBody}")
            };
        }

        _logger.LogDebug("Successfully received audio response from Eleven Labs");

        // Return the audio stream - caller is responsible for disposing
        var audioStream = new MemoryStream();
        await response.Content.CopyToAsync(audioStream, ct);
        audioStream.Position = 0;
        return audioStream;
    }

    public async Task<bool> ValidateApiKeyAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/user", ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Eleven Labs API key validated successfully");
                return true;
            }

            _logger.LogWarning("Eleven Labs API key validation failed: {StatusCode}",
                response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate Eleven Labs API key");
            return false;
        }
    }
}
