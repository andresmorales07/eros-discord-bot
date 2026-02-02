using System.Collections.Concurrent;
using ErosTTS.Bot.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErosTTS.Bot.Services.Character;

/// <summary>
/// In-memory implementation of character state storage.
/// </summary>
public sealed class CharacterStateService : ICharacterStateService
{
    private readonly ConcurrentDictionary<ulong, GuildCharacterState> _states = new();
    private readonly OpenRouterConfiguration _config;
    private readonly ILogger<CharacterStateService> _logger;

    public CharacterStateService(
        IOptions<OpenRouterConfiguration> config,
        ILogger<CharacterStateService> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    public Task SetContextAsync(ulong guildId, string context, bool append = false)
    {
        _states.AddOrUpdate(
            guildId,
            _ => new GuildCharacterState
            {
                GuildId = guildId,
                Context = context,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            (_, existing) => existing with
            {
                Context = append
                    ? $"{existing.Context}\n{context}".Trim()
                    : context,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        _logger.LogInformation(
            "Updated character context for guild {GuildId}, append={Append}",
            guildId, append);

        return Task.CompletedTask;
    }

    public Task<GuildCharacterState?> GetStateAsync(ulong guildId)
    {
        _states.TryGetValue(guildId, out var state);
        return Task.FromResult(state);
    }

    public Task AddMessageAsync(ulong guildId, string role, string content)
    {
        var message = new ConversationMessage
        {
            Role = role,
            Content = content,
            Timestamp = DateTimeOffset.UtcNow
        };

        _states.AddOrUpdate(
            guildId,
            _ => new GuildCharacterState
            {
                GuildId = guildId,
                ConversationHistory = [message],
                UpdatedAt = DateTimeOffset.UtcNow
            },
            (_, existing) =>
            {
                var history = existing.ConversationHistory.ToList();
                history.Add(message);

                // Trim to max history size
                var maxHistory = _config.MaxHistoryMessages;
                if (history.Count > maxHistory)
                {
                    history = history.Skip(history.Count - maxHistory).ToList();
                }

                return existing with
                {
                    ConversationHistory = history,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
            });

        return Task.CompletedTask;
    }

    public Task ClearStateAsync(ulong guildId)
    {
        if (_states.TryRemove(guildId, out _))
        {
            _logger.LogInformation("Cleared character state for guild {GuildId}", guildId);
        }

        return Task.CompletedTask;
    }
}
