namespace ErosTTS.Bot.Configuration;

/// <summary>
/// Configuration for voice audio processing.
/// </summary>
public sealed class VoiceConfiguration
{
    public const string SectionName = "Voice";

    /// <summary>
    /// Path to the FFmpeg executable.
    /// </summary>
    public string FFmpegPath { get; init; } = "ffmpeg";

    /// <summary>
    /// Audio bitrate in kbps for Discord streaming.
    /// </summary>
    public int BitRate { get; init; } = 128;

    /// <summary>
    /// Buffer size in milliseconds for audio streaming.
    /// </summary>
    public int BufferMilliseconds { get; init; } = 1000;
}
