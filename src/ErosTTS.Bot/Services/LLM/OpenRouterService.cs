using System.Net;
using System.Text;
using System.Text.Json;
using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Exceptions;
using ErosTTS.Bot.Services.Character;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErosTTS.Bot.Services.LLM;

/// <summary>
/// OpenRouter API implementation of the LLM service.
/// Uses OpenAI-compatible API format.
/// </summary>
public sealed class OpenRouterService : ILlmService
{
    private readonly HttpClient _httpClient;
    private readonly OpenRouterConfiguration _config;
    private readonly ILogger<OpenRouterService> _logger;

    private const string BaseUrl = "https://openrouter.ai/api/v1";

    public OpenRouterService(
        HttpClient httpClient,
        IOptions<OpenRouterConfiguration> config,
        ILogger<OpenRouterService> logger)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
        _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/eros-discord-bot");
        _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
    }

    public async Task<string> GetCompletionAsync(
        string systemPrompt,
        IReadOnlyList<ConversationMessage> conversationHistory,
        string userMessage,
        CancellationToken ct = default)
    {
        var messages = new List<object>();

        // Combine default system prompt with per-guild character context
        var effectiveSystemPrompt = CombineSystemPrompts(_config.DefaultSystemPrompt, systemPrompt);
        if (!string.IsNullOrWhiteSpace(effectiveSystemPrompt))
        {
            messages.Add(new { role = "system", content = effectiveSystemPrompt });
        }

        // Add conversation history
        foreach (var msg in conversationHistory)
        {
            messages.Add(new { role = msg.Role, content = msg.Content });
        }

        // Add current user message
        messages.Add(new { role = "user", content = userMessage });

        var payload = new
        {
            model = _config.Model,
            messages,
            max_tokens = _config.MaxTokens,
            temperature = _config.Temperature
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        _logger.LogDebug(
            "Sending chat completion request with {MessageCount} messages to model {Model}",
            messages.Count, _config.Model);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync($"{BaseUrl}/chat/completions", content, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Request to OpenRouter timed out after {Timeout}s",
                _config.TimeoutSeconds);
            throw new LlmServiceException("Request timed out", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error communicating with OpenRouter API");
            throw new LlmServiceException("Network error", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("OpenRouter API error: {StatusCode} - {Body}",
                response.StatusCode, errorBody);

            throw response.StatusCode switch
            {
                HttpStatusCode.TooManyRequests => new LlmRateLimitException(
                    "OpenRouter rate limit exceeded",
                    response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(60)),
                HttpStatusCode.Unauthorized => new LlmAuthenticationException(
                    "Invalid OpenRouter API key"),
                HttpStatusCode.BadRequest => new LlmRequestException(
                    $"Bad request: {errorBody}"),
                _ => new LlmServiceException(
                    $"OpenRouter API error: {response.StatusCode} - {errorBody}")
            };
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        var jsonDoc = JsonDocument.Parse(responseBody);
        var assistantMessage = jsonDoc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrEmpty(assistantMessage))
        {
            throw new LlmServiceException("Empty response from OpenRouter");
        }

        _logger.LogDebug("Received response from OpenRouter: {CharCount} characters",
            assistantMessage.Length);

        return assistantMessage;
    }

    /// <summary>
    /// Combines the default system prompt with per-guild character context.
    /// </summary>
    private static string CombineSystemPrompts(string defaultPrompt, string characterContext)
    {
        var hasDefault = !string.IsNullOrWhiteSpace(defaultPrompt);
        var hasCharacter = !string.IsNullOrWhiteSpace(characterContext);

        return (hasDefault, hasCharacter) switch
        {
            (true, true) => $"{defaultPrompt}\n\n{characterContext}",
            (true, false) => defaultPrompt,
            (false, true) => characterContext,
            (false, false) => string.Empty
        };
    }

    public async Task<bool> ValidateApiKeyAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/models", ct);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("OpenRouter API key validated successfully");
                return true;
            }

            _logger.LogWarning("OpenRouter API key validation failed: {StatusCode}",
                response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate OpenRouter API key");
            return false;
        }
    }
}
