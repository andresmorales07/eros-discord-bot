namespace ErosTTS.Bot.Services.TTS;

/// <summary>
/// Interface for text-to-speech services.
/// </summary>
public interface ITtsService
{
    /// <summary>
    /// Synthesizes speech from text and returns the audio stream.
    /// </summary>
    /// <param name="text">The text to convert to speech.</param>
    /// <param name="voiceId">Optional voice ID override. Uses default if null.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A stream containing the audio data.</returns>
    Task<Stream> SynthesizeAsync(string text, string? voiceId = null, CancellationToken ct = default);

    /// <summary>
    /// Validates the API key by making a test request.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the API key is valid.</returns>
    Task<bool> ValidateApiKeyAsync(CancellationToken ct = default);
}
