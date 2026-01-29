namespace ErosTTS.Bot.Services.Queue;

/// <summary>
/// Interface for the TTS message queue.
/// </summary>
public interface ITtsQueue
{
    /// <summary>
    /// Adds an item to the queue.
    /// </summary>
    /// <param name="item">The item to enqueue.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask EnqueueAsync(TtsQueueItem item, CancellationToken ct = default);

    /// <summary>
    /// Returns an async enumerable that yields items from the queue.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    IAsyncEnumerable<TtsQueueItem> ReadAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the current number of items in the queue.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Marks the queue as complete, preventing further writes.
    /// </summary>
    void Complete();
}
