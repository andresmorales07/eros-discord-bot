namespace ErosTTS.Bot.Configuration;

/// <summary>
/// Configuration for the NPC system.
/// </summary>
public sealed class NpcConfiguration
{
    public const string SectionName = "Npc";

    /// <summary>
    /// Maximum number of NPCs allowed per guild.
    /// </summary>
    public int MaxNpcsPerGuild { get; init; } = 20;

    /// <summary>
    /// Maximum conversation history messages to retain per context.
    /// </summary>
    public int MaxHistoryMessages { get; init; } = 50;

    /// <summary>
    /// Number of recent history messages to include when auto-switching NPCs.
    /// </summary>
    public int AutoSwitchContextMessages { get; init; } = 5;
}
