namespace ErosTTS.Bot.Services.TTS;

/// <summary>
/// Extended TTS service interface that exposes provider metadata.
/// Extends <see cref="ITtsService"/> for backward compatibility.
/// </summary>
public interface ITtsProvider : ITtsService
{
    /// <summary>
    /// Display name of this TTS provider (e.g. "ElevenLabs", "OpenAI").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// The default voice ID used when no override is specified.
    /// </summary>
    string DefaultVoiceId { get; }

    /// <summary>
    /// The model ID used for synthesis.
    /// </summary>
    string ModelId { get; }

    /// <summary>
    /// The output audio format.
    /// </summary>
    string OutputFormat { get; }
}
