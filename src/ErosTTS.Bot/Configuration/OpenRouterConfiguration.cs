namespace ErosTTS.Bot.Configuration;

/// <summary>
/// Configuration for the OpenRouter LLM API.
/// </summary>
public sealed class OpenRouterConfiguration
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "OpenRouter";

    /// <summary>
    /// The OpenRouter API key.
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// The model ID to use for chat completions.
    /// Defaults to Claude 3.5 Sonnet.
    /// </summary>
    public string Model { get; init; } = "anthropic/claude-3.5-sonnet";

    /// <summary>
    /// Maximum tokens for the response.
    /// </summary>
    public int MaxTokens { get; init; } = 500;

    /// <summary>
    /// Temperature for response generation (0.0 to 2.0).
    /// Higher values make output more random, lower values more deterministic.
    /// </summary>
    public double Temperature { get; init; } = 0.8;

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 60;

    /// <summary>
    /// Maximum conversation history messages to include in context.
    /// Older messages are trimmed when this limit is exceeded.
    /// </summary>
    public int MaxHistoryMessages { get; init; } = 20;

    /// <summary>
    /// Default system prompt that is always prepended to OpenRouter API requests.
    /// This is combined with per-guild character context (default first, then character context).
    /// </summary>
    public string DefaultSystemPrompt { get; init; } = string.Empty;
}
