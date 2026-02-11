using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Exceptions;
using ErosTTS.Bot.Services.TTS;
using ErosTTS.Bot.Tests.Fakes;

namespace ErosTTS.Bot.Tests.Services.TTS;

public class OpenAiTtsServiceTests : IDisposable
{
    private readonly FakeHttpMessageHandler _httpHandler;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiTtsService> _logger;
    private readonly IOptions<OpenAiTtsConfiguration> _config;

    public OpenAiTtsServiceTests()
    {
        _httpHandler = new FakeHttpMessageHandler();
        _httpClient = new HttpClient(_httpHandler);
        _logger = Substitute.For<ILogger<OpenAiTtsService>>();
        _config = Options.Create(new OpenAiTtsConfiguration
        {
            ApiKey = "test-openai-key",
            Model = "tts-1",
            Voice = "alloy",
            OutputFormat = "mp3",
            Speed = 1.0,
            TimeoutSeconds = 30
        });
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private OpenAiTtsService CreateService()
        => new(_httpClient, _config, _logger);

    [Fact]
    public void ProviderName_ReturnsOpenAI()
    {
        var service = CreateService();
        service.ProviderName.Should().Be("OpenAI");
    }

    [Fact]
    public void DefaultVoiceId_ReturnsConfiguredVoice()
    {
        var service = CreateService();
        service.DefaultVoiceId.Should().Be("alloy");
    }

    [Fact]
    public void ModelId_ReturnsConfiguredModel()
    {
        var service = CreateService();
        service.ModelId.Should().Be("tts-1");
    }

    [Fact]
    public void OutputFormat_ReturnsConfiguredFormat()
    {
        var service = CreateService();
        service.OutputFormat.Should().Be("mp3");
    }

    [Fact]
    public async Task SynthesizeAsync_WithValidText_ReturnsAudioStream()
    {
        var audioData = new byte[] { 0x49, 0x44, 0x33 };
        _httpHandler.EnqueueResponse(HttpStatusCode.OK, audioData);
        var service = CreateService();

        var result = await service.SynthesizeAsync("Hello world");

        result.Should().NotBeNull();
        result.Should().BeReadable();
        result.Length.Should().Be(audioData.Length);
    }

    [Fact]
    public async Task SynthesizeAsync_SetsCorrectAuthorizationHeader()
    {
        _httpHandler.EnqueueResponse(HttpStatusCode.OK, new byte[] { 0x00 });
        var service = CreateService();

        await service.SynthesizeAsync("Test");

        _httpHandler.SentRequests.Should().HaveCount(1);
        var request = _httpHandler.SentRequests[0];
        request.Headers.Authorization.Should().NotBeNull();
        request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        request.Headers.Authorization.Parameter.Should().Be("test-openai-key");
    }

    [Fact]
    public async Task SynthesizeAsync_SendsRequestToCorrectUrl()
    {
        _httpHandler.EnqueueResponse(HttpStatusCode.OK, new byte[] { 0x00 });
        var service = CreateService();

        await service.SynthesizeAsync("Test");

        var request = _httpHandler.SentRequests[0];
        request.RequestUri!.ToString().Should().Contain("/v1/audio/speech");
        request.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task SynthesizeAsync_ReturnsStreamAtPositionZero()
    {
        var audioData = new byte[] { 0x01, 0x02, 0x03 };
        _httpHandler.EnqueueResponse(HttpStatusCode.OK, audioData);
        var service = CreateService();

        var result = await service.SynthesizeAsync("Test");

        result.Position.Should().Be(0);
    }

    [Fact]
    public async Task SynthesizeAsync_ReturnedStreamContainsCorrectData()
    {
        var audioData = new byte[] { 0xAA, 0xBB, 0xCC };
        _httpHandler.EnqueueResponse(HttpStatusCode.OK, audioData);
        var service = CreateService();

        var result = await service.SynthesizeAsync("Test");

        var buffer = new byte[audioData.Length];
        var bytesRead = await result.ReadAsync(buffer);
        bytesRead.Should().Be(audioData.Length);
        buffer.Should().BeEquivalentTo(audioData);
    }

    [Fact]
    public async Task SynthesizeAsync_WithTooManyRequests_ThrowsRateLimitException()
    {
        _httpHandler.EnqueueRateLimitResponse(TimeSpan.FromSeconds(30));
        var service = CreateService();

        var act = async () => await service.SynthesizeAsync("Test");

        await act.Should().ThrowAsync<RateLimitException>();
    }

    [Fact]
    public async Task SynthesizeAsync_WithTooManyRequests_SetsRetryAfterFromHeader()
    {
        var retryAfter = TimeSpan.FromSeconds(45);
        _httpHandler.EnqueueRateLimitResponse(retryAfter);
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<RateLimitException>(
            () => service.SynthesizeAsync("Test"));

        exception.RetryAfter.Should().Be(retryAfter);
    }

    [Fact]
    public async Task SynthesizeAsync_WithUnauthorized_ThrowsAuthenticationException()
    {
        _httpHandler.EnqueueResponse(HttpStatusCode.Unauthorized, "Invalid API key");
        var service = CreateService();

        var act = async () => await service.SynthesizeAsync("Test");

        await act.Should().ThrowAsync<AuthenticationException>()
            .WithMessage("*Invalid*API key*");
    }

    [Fact]
    public async Task SynthesizeAsync_WithBadRequest_ThrowsInvalidTextException()
    {
        _httpHandler.EnqueueResponse(HttpStatusCode.BadRequest, "Invalid request format");
        var service = CreateService();

        var act = async () => await service.SynthesizeAsync("Test");

        await act.Should().ThrowAsync<InvalidTextException>()
            .WithMessage("*Bad request*");
    }

    [Fact]
    public async Task SynthesizeAsync_WithServiceUnavailable_ThrowsTtsServiceException()
    {
        _httpHandler.EnqueueResponse(HttpStatusCode.ServiceUnavailable, "Service down");
        var service = CreateService();

        var act = async () => await service.SynthesizeAsync("Test");

        await act.Should().ThrowAsync<TtsServiceException>()
            .WithMessage("*ServiceUnavailable*");
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
}
