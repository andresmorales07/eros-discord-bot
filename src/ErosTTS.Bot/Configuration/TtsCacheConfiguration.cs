namespace ErosTTS.Bot.Configuration;

/// <summary>
/// Configuration for TTS audio caching.
/// </summary>
public sealed class TtsCacheConfiguration
{
    public const string SectionName = "TtsCache";

    /// <summary>
    /// Whether TTS caching is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Directory path for cached audio files.
    /// </summary>
    public string CacheDirectory { get; init; } = "data/tts-cache";
}
