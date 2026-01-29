namespace ErosTTS.Bot.Exceptions;

/// <summary>
/// Base exception for TTS service errors.
/// </summary>
public class TtsServiceException : Exception
{
    public TtsServiceException(string message) : base(message) { }
    public TtsServiceException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Exception thrown when the API rate limit is exceeded.
/// </summary>
public class RateLimitException : TtsServiceException
{
    /// <summary>
    /// The recommended time to wait before retrying.
    /// </summary>
    public TimeSpan RetryAfter { get; }

    public RateLimitException(string message, TimeSpan retryAfter)
        : base(message) => RetryAfter = retryAfter;
}

/// <summary>
/// Exception thrown when API authentication fails.
/// </summary>
public class AuthenticationException : TtsServiceException
{
    public AuthenticationException(string message) : base(message) { }
}

/// <summary>
/// Exception thrown when the input text is invalid or cannot be processed.
/// </summary>
public class InvalidTextException : TtsServiceException
{
    public InvalidTextException(string message) : base(message) { }
}

/// <summary>
/// Exception thrown when voice channel connection fails.
/// </summary>
public class VoiceConnectionException : TtsServiceException
{
    public VoiceConnectionException(string message) : base(message) { }
    public VoiceConnectionException(string message, Exception inner) : base(message, inner) { }
}
