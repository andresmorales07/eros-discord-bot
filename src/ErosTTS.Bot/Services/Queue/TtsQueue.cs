using System.Threading.Channels;
using ErosTTS.Bot.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErosTTS.Bot.Services.Queue;

/// <summary>
/// Bounded channel-based TTS message queue with single-reader pattern.
/// </summary>
public sealed class TtsQueue : ITtsQueue
{
    private readonly Channel<TtsQueueItem> _channel;
    private readonly ILogger<TtsQueue> _logger;

    public TtsQueue(ILogger<TtsQueue> logger, IOptions<QueueConfiguration> options)
    {
        _logger = logger;
        var capacity = options.Value.Capacity;

        _channel = Channel.CreateBounded<TtsQueueItem>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,  // Only one consumer processes TTS
            SingleWriter = false  // Multiple message handlers can enqueue
        });

        _logger.LogInformation("TTS Queue initialized with capacity {Capacity}", capacity);
    }

    public int Count => _channel.Reader.Count;

    public async ValueTask EnqueueAsync(TtsQueueItem item, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(item, ct);
        _logger.LogDebug(
            "Enqueued TTS item {ItemId} for guild {GuildId} from {Username}. Queue size: {Count}",
            item.Id, item.GuildId, item.Username, Count);
    }

    public IAsyncEnumerable<TtsQueueItem> ReadAllAsync(CancellationToken ct = default)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }

    public void Complete()
    {
        _channel.Writer.Complete();
        _logger.LogInformation("TTS Queue marked as complete");
    }
}
