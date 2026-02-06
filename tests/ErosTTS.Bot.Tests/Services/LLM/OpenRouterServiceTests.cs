using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Exceptions;
using ErosTTS.Bot.Services.LLM;
using ErosTTS.Bot.Tests.Fakes;

namespace ErosTTS.Bot.Tests.Services.LLM;

public class OpenRouterServiceTests : IDisposable
{
    private readonly FakeHttpMessageHandler _httpHandler;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenRouterService> _logger;
    private readonly IOptions<OpenRouterConfiguration> _config;

    public OpenRouterServiceTests()
    {
        _httpHandler = new FakeHttpMessageHandler();
        _httpClient = new HttpClient(_httpHandler);
        _logger = Substitute.For<ILogger<OpenRouterService>>();
        _config = Options.Create(new OpenRouterConfiguration
        {
            ApiKey = "test-api-key",
            Model = "test-model",
            MaxTokens = 500,
            Temperature = 0.8,
            TimeoutSeconds = 30
        });
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private OpenRouterService CreateService()
        => new OpenRouterService(_httpClient, _config, _logger);

    private static string CreateChatCompletionResponse(string content)
    {
        return $$"""
        {
            "choices": [
                {
                    "message": {
                        "role": "assistant",
                        "content": "{{content}}"
                    }
                }
            ]
        }
        """;
    }

    [Fact]
    public async Task GetCompletionAsync_WithValidRequest_ReturnsResponse()
    {
        var expectedResponse = "Hello, I am your AI assistant!";
        _httpHandler.EnqueueResponse(HttpStatusCode.OK, CreateChatCompletionResponse(expectedResponse));
        var service = CreateService();

        var result = await service.GetCompletionAsync("", [], "Hello");

        result.Should().Be(expectedResponse);
    }

    [Fact]
    public async Task GetCompletionAsync_SetsCorrectAuthorizationHeader()
    {
        _httpHandler.EnqueueResponse(HttpStatusCode.OK, CreateChatCompletionResponse("response"));
        var service = CreateService();

        await service.GetCompletionAsync("", [], "Test");

        _httpHandler.SentRequests.Should().HaveCount(1);
        var request = _httpHandler.SentRequests[0];
        request.Headers.Authorization.Should().NotBeNull();
        request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        request.Headers.Authorization.Parameter.Should().Be("test-api-key");
    }

    [Fact]
    public async Task GetCompletionAsync_SendsRequestToCorrectUrl()
    {
        _httpHandler.EnqueueResponse(HttpStatusCode.OK, CreateChatCompletionResponse("response"));
        var service = CreateService();

        await service.GetCompletionAsync("", [], "Test");

        var request = _httpHandler.SentRequests[0];
        request.RequestUri!.ToString().Should().Be("https://openrouter.ai/api/v1/chat/completions");
    }

    [Fact]
    public async Task GetCompletionAsync_IncludesSystemPromptWhenProvided()
    {
        _httpHandler.EnqueueResponse(HttpStatusCode.OK, CreateChatCompletionResponse("response"));
        var service = CreateService();

        await service.GetCompletionAsync("You are a helpful assistant.", [], "Test");

        var request = _httpHandler.SentRequests[0];
        var content = await request.Content!.ReadAsStringAsync();
        content.Should().Contain("system");
        content.Should().Contain("You are a helpful assistant.");
    }

    [Fact]
    public async Task GetCompletionAsync_IncludesConversationHistory()
    {
        _httpHandler.EnqueueResponse(HttpStatusCode.OK, CreateChatCompletionResponse("response"));
        var service = CreateService();
        var history = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "Previous question" },
            new() { Role = "assistant", Content = "Previous answer" }
        };

        await service.GetCompletionAsync("", history, "New question");

        var request = _httpHandler.SentRequests[0];
        var content = await request.Content!.ReadAsStringAsync();
        content.Should().Contain("Previous question");
        content.Should().Contain("Previous answer");
        content.Should().Contain("New question");
    }

    [Fact]
    public async Task GetCompletionAsync_WithTooManyRequests_ThrowsLlmRateLimitException()
    {
        _httpHandler.EnqueueRateLimitResponse(TimeSpan.FromSeconds(30));
        var service = CreateService();

        var act = async () => await service.GetCompletionAsync("", [], "Test");

        await act.Should().ThrowAsync<LlmRateLimitException>();
    }

    [Fact]
    public async Task GetCompletionAsync_WithTooManyRequests_SetsRetryAfterFromHeader()
    {
        var retryAfter = TimeSpan.FromSeconds(45);
        _httpHandler.EnqueueRateLimitResponse(retryAfter);
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<LlmRateLimitException>(
            () => service.GetCompletionAsync("", [], "Test"));

        exception.RetryAfter.Should().Be(retryAfter);
    }

    [Fact]
    public async Task GetCompletionAsync_WithUnauthorized_ThrowsLlmAuthenticationException()
    {
        _httpHandler.EnqueueResponse(HttpStatusCode.Unauthorized, "Invalid API key");
        var service = CreateService();

        var act = async () => await service.GetCompletionAsync("", [], "Test");

        await act.Should().ThrowAsync<LlmAuthenticationException>()
            .WithMessage("*Invalid*API key*");
    }

    [Fact]
    public async Task GetCompletionAsync_WithBadRequest_ThrowsLlmRequestException()
    {
        _httpHandler.EnqueueResponse(HttpStatusCode.BadRequest, "Invalid request format");
        var service = CreateService();

        var act = async () => await service.GetCompletionAsync("", [], "Test");

        await act.Should().ThrowAsync<LlmRequestException>()
            .WithMessage("*Bad request*");
    }

    [Fact]
    public async Task GetCompletionAsync_WithServiceUnavailable_ThrowsLlmServiceException()
    {
        _httpHandler.EnqueueResponse(HttpStatusCode.ServiceUnavailable, "Service down");
        var service = CreateService();

        var act = async () => await service.GetCompletionAsync("", [], "Test");

        await act.Should().ThrowAsync<LlmServiceException>()
            .WithMessage("*ServiceUnavailable*");
    }

    [Fact]
    public async Task GetCompletionAsync_WithEmptyResponse_ThrowsLlmServiceException()
    {
        _httpHandler.EnqueueResponse(HttpStatusCode.OK, CreateChatCompletionResponse(""));
        var service = CreateService();

        var act = async () => await service.GetCompletionAsync("", [], "Test");

        await act.Should().ThrowAsync<LlmServiceException>()
            .WithMessage("*Empty response*");
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithValidKey_ReturnsTrue()
    {
        _httpHandler.EnqueueResponse(HttpStatusCode.OK, "{}");
        var service = CreateService();

        var result = await service.ValidateApiKeyAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithInvalidKey_ReturnsFalse()
    {
        _httpHandler.EnqueueResponse(HttpStatusCode.Unauthorized, "Invalid");
        var service = CreateService();

        var result = await service.ValidateApiKeyAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateApiKeyAsync_RequestsModelsEndpoint()
    {
        _httpHandler.EnqueueResponse(HttpStatusCode.OK, "{}");
        var service = CreateService();

        await service.ValidateApiKeyAsync();

        _httpHandler.SentRequests.Should().HaveCount(1);
        var request = _httpHandler.SentRequests[0];
        request.RequestUri!.ToString().Should().EndWith("/models");
        request.Method.Should().Be(HttpMethod.Get);
    }

    [Fact]
    public async Task GetCompletionAsync_IncludesModelInRequest()
    {
        _httpHandler.EnqueueResponse(HttpStatusCode.OK, CreateChatCompletionResponse("response"));
        var service = CreateService();

        await service.GetCompletionAsync("", [], "Test");

        var request = _httpHandler.SentRequests[0];
        var content = await request.Content!.ReadAsStringAsync();
        content.Should().Contain("\"model\":\"test-model\"");
    }

    [Fact]
    public async Task GetCompletionAsync_IncludesMaxTokensInRequest()
    {
        _httpHandler.EnqueueResponse(HttpStatusCode.OK, CreateChatCompletionResponse("response"));
        var service = CreateService();

        await service.GetCompletionAsync("", [], "Test");

        var request = _httpHandler.SentRequests[0];
        var content = await request.Content!.ReadAsStringAsync();
        content.Should().Contain("\"max_tokens\":500");
    }

    [Fact]
    public async Task GetCompletionAsync_IncludesTemperatureInRequest()
    {
        _httpHandler.EnqueueResponse(HttpStatusCode.OK, CreateChatCompletionResponse("response"));
        var service = CreateService();

        await service.GetCompletionAsync("", [], "Test");

        var request = _httpHandler.SentRequests[0];
        var content = await request.Content!.ReadAsStringAsync();
        content.Should().Contain("\"temperature\":0.8");
    }
}
