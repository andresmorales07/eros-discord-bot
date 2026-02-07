namespace ErosTTS.Bot.Configuration;

/// <summary>
/// Configuration for the Eleven Labs TTS API.
/// </summary>
public sealed class ElevenLabsConfiguration
{
    public const string SectionName = "ElevenLabs";

    /// <summary>
    /// The Eleven Labs API key.
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// The voice ID to use for TTS. Defaults to "Rachel" voice.
    /// </summary>
    public string VoiceId { get; init; } = "21m00Tcm4TlvDq8ikWAM";

    /// <summary>
    /// The model ID to use for TTS.
    /// </summary>
    public string ModelId { get; init; } = "eleven_turbo_v2_5";

    /// <summary>
    /// Output audio format.
    /// </summary>
    public string OutputFormat { get; init; } = "mp3_44100_128";

    /// <summary>
    /// Voice stability setting (0.0 to 1.0).
    /// </summary>
    public double Stability { get; init; } = 0.5;

    /// <summary>
    /// Voice similarity boost setting (0.0 to 1.0).
    /// </summary>
    public double SimilarityBoost { get; init; } = 0.75;

    /// <summary>
    /// Maximum number of retries for failed requests.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 30;
}
