namespace ErosTTS.Bot.Configuration;

/// <summary>
/// Configuration for the TTS message queue.
/// </summary>
public sealed class QueueConfiguration
{
    public const string SectionName = "Queue";

    /// <summary>
    /// Maximum number of items in the queue.
    /// </summary>
    public int Capacity { get; init; } = 100;
}
