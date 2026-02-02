namespace ErosTTS.Bot.Services.Character;

/// <summary>
/// Interface for managing per-guild character state.
/// </summary>
public interface ICharacterStateService
{
    /// <summary>
    /// Sets or appends to the character context for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="context">The context text to set or append.</param>
    /// <param name="append">If true, appends to existing context; otherwise replaces it.</param>
    Task SetContextAsync(ulong guildId, string context, bool append = false);

    /// <summary>
    /// Gets the current character state for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The character state, or null if no state exists.</returns>
    Task<GuildCharacterState?> GetStateAsync(ulong guildId);

    /// <summary>
    /// Adds a message to the conversation history.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="role">The role of the message sender ("user" or "assistant").</param>
    /// <param name="content">The message content.</param>
    Task AddMessageAsync(ulong guildId, string role, string content);

    /// <summary>
    /// Clears all character state (context and history) for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    Task ClearStateAsync(ulong guildId);
}
