using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErosTTS.Bot.Services.Npc;

/// <summary>
/// Shared JSON serialization options for NPC import/export.
/// </summary>
internal static class NpcJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };
}

internal sealed class NpcExportData
{
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("npcs")]
    public List<NpcExportItem> Npcs { get; set; } = [];
}

internal sealed class NpcExportItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("personality")]
    public string? Personality { get; set; }

    [JsonPropertyName("voiceId")]
    public string? VoiceId { get; set; }
}
