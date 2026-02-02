using ErosTTS.Bot.Services.Character;

namespace ErosTTS.Bot.Services.LLM;

/// <summary>
/// Interface for LLM chat completion services.
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// Generates a chat completion based on context and conversation history.
    /// </summary>
    /// <param name="systemPrompt">The system prompt/character context.</param>
    /// <param name="conversationHistory">Previous messages in the conversation.</param>
    /// <param name="userMessage">The new user message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The assistant's response.</returns>
    Task<string> GetCompletionAsync(
        string systemPrompt,
        IReadOnlyList<ConversationMessage> conversationHistory,
        string userMessage,
        CancellationToken ct = default);

    /// <summary>
    /// Validates the API key by making a test request.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the API key is valid.</returns>
    Task<bool> ValidateApiKeyAsync(CancellationToken ct = default);
}
