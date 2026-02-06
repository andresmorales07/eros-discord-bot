namespace ErosTTS.Bot.Services.Npc;

/// <summary>
/// Per-guild NPC settings: active NPC, auto-switch toggle, and history mode.
/// </summary>
public sealed record GuildNpcSettings
{
    public required ulong GuildId { get; init; }
    public int? ActiveNpcId { get; init; }
    public bool AutoSwitchEnabled { get; init; }
    public bool SharedHistory { get; init; }
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
