namespace ErosTTS.Bot.Data.Converters;

/// <summary>
/// Converts between ulong Discord snowflake IDs and long values for database storage.
/// The conversion is lossless — all 64 bits are preserved via unchecked cast.
/// </summary>
public static class DiscordIdConverter
{
    public static long ToLong(ulong discordId) => unchecked((long)discordId);
    public static ulong ToULong(long storedId) => unchecked((ulong)storedId);
}
