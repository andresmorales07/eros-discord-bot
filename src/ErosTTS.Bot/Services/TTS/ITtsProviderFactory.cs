namespace ErosTTS.Bot.Services.TTS;

/// <summary>
/// Factory that resolves the correct <see cref="ITtsProvider"/> for a guild.
/// </summary>
public interface ITtsProviderFactory
{
    /// <summary>
    /// Gets the TTS provider configured for a guild, falling back to the default provider.
    /// </summary>
    Task<ITtsProvider> GetProviderAsync(ulong guildId);

    /// <summary>
    /// Gets a provider by its display name (case-insensitive).
    /// </summary>
    ITtsProvider? GetProviderByName(string name);

    /// <summary>
    /// Lists the names of all registered (available) providers.
    /// </summary>
    IReadOnlyList<string> GetAvailableProviders();
}
