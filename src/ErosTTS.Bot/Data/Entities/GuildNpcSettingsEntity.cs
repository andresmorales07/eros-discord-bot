namespace ErosTTS.Bot.Data.Entities;

/// <summary>
/// EF Core entity for per-guild NPC settings.
/// </summary>
public sealed class GuildNpcSettingsEntity
{
    public long GuildId { get; set; }
    public int? ActiveNpcId { get; set; }
    public bool AutoSwitchEnabled { get; set; }
    public bool SharedHistory { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public NpcEntity? ActiveNpc { get; set; }
}
