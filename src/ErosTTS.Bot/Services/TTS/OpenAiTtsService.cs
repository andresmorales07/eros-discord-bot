using System.Net;
using System.Text;
using System.Text.Json;
using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErosTTS.Bot.Services.TTS;

/// <summary>
/// OpenAI TTS API implementation.
/// </summary>
public sealed class OpenAiTtsService : ITtsProvider
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiTtsConfiguration _config;
    private readonly ILogger<OpenAiTtsService> _logger;

    private const string BaseUrl = "https://api.openai.com/v1";

    public string ProviderName => "OpenAI";
    public string DefaultVoiceId => _config.Voice;
    public string ModelId => _config.Model;
    public string OutputFormat => _config.OutputFormat;

    public OpenAiTtsService(
        HttpClient httpClient,
        IOptions<OpenAiTtsConfiguration> config,
        ILogger<OpenAiTtsService> logger)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
        _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
    }

    public async Task<Stream> SynthesizeAsync(string text, string? voiceId = null, CancellationToken ct = default)
    {
        var effectiveVoice = voiceId ?? _config.Voice;

        var payload = new
        {
            model = _config.Model,
            input = text,
            voice = effectiveVoice,
            response_format = _config.OutputFormat,
            speed = _config.Speed
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        _logger.LogDebug("Sending TTS request for {CharCount} characters to OpenAI voice {Voice}",
            text.Length, effectiveVoice);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync($"{BaseUrl}/audio/speech", content, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Request to OpenAI TTS timed out after {Timeout}s",
                _config.TimeoutSeconds);
            throw new TtsServiceException("Request timed out", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error communicating with OpenAI TTS API");
            throw new TtsServiceException("Network error", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("OpenAI TTS API error: {StatusCode} - {Body}",
                response.StatusCode, errorBody);

            throw response.StatusCode switch
            {
                HttpStatusCode.TooManyRequests => new RateLimitException(
                    "OpenAI rate limit exceeded",
                    response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(60)),
                HttpStatusCode.Unauthorized => new AuthenticationException(
                    "Invalid OpenAI API key"),
                HttpStatusCode.BadRequest => new InvalidTextException(
                    $"Bad request: {errorBody}"),
                _ => new TtsServiceException(
                    $"OpenAI TTS API error: {response.StatusCode} - {errorBody}")
            };
        }

        _logger.LogDebug("Successfully received audio response from OpenAI TTS");

        var audioStream = new MemoryStream();
        await response.Content.CopyToAsync(audioStream, ct);
        audioStream.Position = 0;
        return audioStream;
    }

    public async Task<bool> ValidateApiKeyAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/models", ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("OpenAI API key validated successfully");
                return true;
            }

            _logger.LogWarning("OpenAI API key validation failed: {StatusCode}",
                response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate OpenAI API key");
            return false;
        }
    }
}
