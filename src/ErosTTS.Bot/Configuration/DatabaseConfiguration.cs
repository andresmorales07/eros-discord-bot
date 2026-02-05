namespace ErosTTS.Bot.Configuration;

/// <summary>
/// Configuration for database persistence.
/// </summary>
public sealed class DatabaseConfiguration
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Database";

    /// <summary>
    /// The database provider to use: "InMemory", "Sqlite", or "Postgres".
    /// </summary>
    public string Provider { get; init; } = "InMemory";

    /// <summary>
    /// Connection string for the database provider.
    /// For Sqlite: "Data Source=data/erostts.db"
    /// For Postgres: "Host=...;Database=...;Username=...;Password=..."
    /// Ignored when Provider is "InMemory".
    /// </summary>
    public string ConnectionString { get; init; } = "Data Source=data/erostts.db";
}
