namespace ErosTTS.Bot.Exceptions;

/// <summary>
/// Base exception for LLM service errors.
/// </summary>
public class LlmServiceException : Exception
{
    public LlmServiceException(string message) : base(message) { }
    public LlmServiceException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Exception thrown when the LLM API rate limit is exceeded.
/// </summary>
public class LlmRateLimitException : LlmServiceException
{
    /// <summary>
    /// The recommended time to wait before retrying.
    /// </summary>
    public TimeSpan RetryAfter { get; }

    public LlmRateLimitException(string message, TimeSpan retryAfter)
        : base(message) => RetryAfter = retryAfter;
}

/// <summary>
/// Exception thrown when LLM API authentication fails.
/// </summary>
public class LlmAuthenticationException : LlmServiceException
{
    public LlmAuthenticationException(string message) : base(message) { }
}

/// <summary>
/// Exception thrown when the LLM request is invalid.
/// </summary>
public class LlmRequestException : LlmServiceException
{
    public LlmRequestException(string message) : base(message) { }
}
