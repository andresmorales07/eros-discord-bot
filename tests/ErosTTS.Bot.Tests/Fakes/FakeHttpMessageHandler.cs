using System.Net.Http.Headers;

namespace ErosTTS.Bot.Tests.Fakes;

public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();
    private readonly List<HttpRequestMessage> _sentRequests = new();

    public IReadOnlyList<HttpRequestMessage> SentRequests => _sentRequests;

    public void EnqueueResponse(HttpResponseMessage response)
        => _responses.Enqueue(response);

    public void EnqueueResponse(HttpStatusCode statusCode, string? content = null)
    {
        var response = new HttpResponseMessage(statusCode);
        if (content != null)
            response.Content = new StringContent(content);
        _responses.Enqueue(response);
    }

    public void EnqueueResponse(HttpStatusCode statusCode, byte[] audioContent)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new ByteArrayContent(audioContent)
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("audio/mpeg");
        _responses.Enqueue(response);
    }

    public void EnqueueRateLimitResponse(TimeSpan retryAfter)
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(retryAfter);
        response.Content = new StringContent("Rate limit exceeded");
        _responses.Enqueue(response);
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _sentRequests.Add(request);

        if (_responses.Count == 0)
            throw new InvalidOperationException("No response configured for FakeHttpMessageHandler");

        return Task.FromResult(_responses.Dequeue());
    }
}
