using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Services.Queue;

namespace ErosTTS.Bot.Tests.Services.Queue;

public class TtsQueueTests
{
    private readonly ILogger<TtsQueue> _logger;

    public TtsQueueTests()
    {
        _logger = Substitute.For<ILogger<TtsQueue>>();
    }

    private TtsQueue CreateQueue(int capacity = 100)
    {
        var options = Options.Create(new QueueConfiguration { Capacity = capacity });
        return new TtsQueue(_logger, options);
    }

    private static TtsQueueItem CreateTestItem(string text = "Test message")
    {
        return new TtsQueueItem
        {
            Id = Guid.NewGuid(),
            GuildId = 12345UL,
            TextChannelId = 67890UL,
            VoiceChannelId = 11111UL,
            Text = text,
            Username = "TestUser"
        };
    }

    [Fact]
    public void Count_WhenEmpty_ReturnsZero()
    {
        var queue = CreateQueue();

        queue.Count.Should().Be(0);
    }

    [Fact]
    public async Task EnqueueAsync_WithValidItem_IncreasesCount()
    {
        var queue = CreateQueue();
        var item = CreateTestItem();

        await queue.EnqueueAsync(item);

        queue.Count.Should().Be(1);
    }

    [Fact]
    public async Task EnqueueAsync_MultipleTimes_CorrectlyTracksCount()
    {
        var queue = CreateQueue();

        await queue.EnqueueAsync(CreateTestItem("Message 1"));
        await queue.EnqueueAsync(CreateTestItem("Message 2"));
        await queue.EnqueueAsync(CreateTestItem("Message 3"));

        queue.Count.Should().Be(3);
    }

    [Fact]
    public async Task ReadAllAsync_AfterEnqueue_YieldsEnqueuedItem()
    {
        var queue = CreateQueue();
        var item = CreateTestItem("Hello world");
        await queue.EnqueueAsync(item);

        using var cts = new CancellationTokenSource();
        var readTask = Task.Run(async () =>
        {
            await foreach (var readItem in queue.ReadAllAsync(cts.Token))
            {
                return readItem;
            }
            return null;
        });

        var result = await readTask.WaitAsync(TimeSpan.FromSeconds(1));

        result.Should().NotBeNull();
        result!.Id.Should().Be(item.Id);
        result.Text.Should().Be("Hello world");
    }

    [Fact]
    public async Task ReadAllAsync_AfterMultipleEnqueues_YieldsItemsInOrder()
    {
        var queue = CreateQueue();
        var items = new[]
        {
            CreateTestItem("First"),
            CreateTestItem("Second"),
            CreateTestItem("Third")
        };

        foreach (var item in items)
        {
            await queue.EnqueueAsync(item);
        }

        using var cts = new CancellationTokenSource();
        var readItems = new List<TtsQueueItem>();
        var readTask = Task.Run(async () =>
        {
            await foreach (var readItem in queue.ReadAllAsync(cts.Token))
            {
                readItems.Add(readItem);
                if (readItems.Count >= 3)
                    break;
            }
        });

        await readTask.WaitAsync(TimeSpan.FromSeconds(1));

        readItems.Should().HaveCount(3);
        readItems[0].Text.Should().Be("First");
        readItems[1].Text.Should().Be("Second");
        readItems[2].Text.Should().Be("Third");
    }

    [Fact]
    public async Task Complete_AfterCompletion_ReadAllAsyncCompletes()
    {
        var queue = CreateQueue();
        await queue.EnqueueAsync(CreateTestItem());

        var itemsRead = new List<TtsQueueItem>();
        var readTask = Task.Run(async () =>
        {
            await foreach (var item in queue.ReadAllAsync())
            {
                itemsRead.Add(item);
            }
        });

        await Task.Delay(50);
        queue.Complete();

        await readTask.WaitAsync(TimeSpan.FromSeconds(1));

        itemsRead.Should().HaveCount(1);
    }

    [Fact]
    public async Task EnqueueAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var queue = CreateQueue(capacity: 1);
        await queue.EnqueueAsync(CreateTestItem());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await queue.EnqueueAsync(CreateTestItem(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ReadAllAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var queue = CreateQueue();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () =>
        {
            await foreach (var _ in queue.ReadAllAsync(cts.Token))
            {
            }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task EnqueueAsync_PreservesItemProperties()
    {
        var queue = CreateQueue();
        var originalItem = new TtsQueueItem
        {
            Id = Guid.NewGuid(),
            GuildId = 999UL,
            TextChannelId = 888UL,
            VoiceChannelId = 777UL,
            Text = "Test text",
            Username = "TestUsername",
            RetryCount = 2
        };

        await queue.EnqueueAsync(originalItem);

        using var cts = new CancellationTokenSource();
        TtsQueueItem? readItem = null;
        var readTask = Task.Run(async () =>
        {
            await foreach (var item in queue.ReadAllAsync(cts.Token))
            {
                readItem = item;
                break;
            }
        });

        await readTask.WaitAsync(TimeSpan.FromSeconds(1));

        readItem.Should().NotBeNull();
        readItem!.Id.Should().Be(originalItem.Id);
        readItem.GuildId.Should().Be(999UL);
        readItem.TextChannelId.Should().Be(888UL);
        readItem.VoiceChannelId.Should().Be(777UL);
        readItem.Text.Should().Be("Test text");
        readItem.Username.Should().Be("TestUsername");
        readItem.RetryCount.Should().Be(2);
    }

    [Fact]
    public async Task ConcurrentEnqueue_WithMultipleWriters_IsThreadSafe()
    {
        var queue = CreateQueue(capacity: 200);

        var enqueueTasks = Enumerable.Range(1, 50)
            .Select(i => Task.Run(async () =>
            {
                await queue.EnqueueAsync(CreateTestItem($"Message {i}"));
            }))
            .ToList();

        await Task.WhenAll(enqueueTasks);

        queue.Count.Should().Be(50);
    }
}
