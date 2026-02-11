namespace ErosTTS.Bot.Configuration;

/// <summary>
/// Configuration for the OpenAI TTS API.
/// </summary>
public sealed class OpenAiTtsConfiguration
{
    public const string SectionName = "OpenAiTts";

    /// <summary>
    /// The OpenAI API key.
    /// </summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// The TTS model to use.
    /// </summary>
    public string Model { get; init; } = "tts-1";

    /// <summary>
    /// The voice to use (alloy, echo, fable, onyx, nova, shimmer).
    /// </summary>
    public string Voice { get; init; } = "alloy";

    /// <summary>
    /// Output audio format (mp3, opus, aac, flac, wav, pcm).
    /// </summary>
    public string OutputFormat { get; init; } = "mp3";

    /// <summary>
    /// Speech speed multiplier (0.25 to 4.0).
    /// </summary>
    public double Speed { get; init; } = 1.0;

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 30;
}
