using ErosTTS.Bot.Services.Guild;
using Microsoft.Extensions.Logging;

namespace ErosTTS.Bot.Services.TTS;

/// <summary>
/// Resolves the correct <see cref="ITtsProvider"/> for a guild based on its configuration.
/// </summary>
public sealed class TtsProviderFactory : ITtsProviderFactory
{
    private readonly Dictionary<string, ITtsProvider> _providers;
    private readonly IGuildConfigurationService _guildConfig;
    private readonly ITtsProvider _defaultProvider;
    private readonly ILogger<TtsProviderFactory> _logger;

    public TtsProviderFactory(
        IEnumerable<ITtsProvider> providers,
        IGuildConfigurationService guildConfig,
        ILogger<TtsProviderFactory> logger)
    {
        _guildConfig = guildConfig;
        _logger = logger;

        _providers = new Dictionary<string, ITtsProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in providers)
        {
            _providers[provider.ProviderName] = provider;
        }

        _defaultProvider = _providers.GetValueOrDefault("ElevenLabs")
            ?? _providers.Values.First();
    }

    public async Task<ITtsProvider> GetProviderAsync(ulong guildId)
    {
        var config = await _guildConfig.GetConfigurationAsync(guildId);
        if (config?.TtsProvider is { } name && _providers.TryGetValue(name, out var provider))
        {
            return provider;
        }

        return _defaultProvider;
    }

    public ITtsProvider? GetProviderByName(string name)
    {
        _providers.TryGetValue(name, out var provider);
        return provider;
    }

    public IReadOnlyList<string> GetAvailableProviders()
    {
        return _providers.Keys.ToList();
    }
}
