using System.Text.RegularExpressions;

namespace ErosTTS.Bot.Utilities;

/// <summary>
/// Utility class for sanitizing Discord message text for TTS.
/// </summary>
public static partial class TextSanitizer
{
    /// <summary>
    /// Sanitizes text by removing Discord-specific formatting.
    /// </summary>
    public static string Sanitize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var text = input;

        // Remove user mentions (<@123456789> or <@!123456789>)
        text = UserMentionRegex().Replace(text, "");

        // Remove channel mentions (<#123456789>)
        text = ChannelMentionRegex().Replace(text, "");

        // Remove role mentions (<@&123456789>)
        text = RoleMentionRegex().Replace(text, "");

        // Remove custom emojis (<:name:123456789> or <a:name:123456789>)
        text = CustomEmojiRegex().Replace(text, "");

        // Remove URLs
        text = UrlRegex().Replace(text, "");

        // Remove code blocks
        text = CodeBlockRegex().Replace(text, "");

        // Remove inline code
        text = InlineCodeRegex().Replace(text, "");

        // Remove markdown formatting (bold, italic, underline, strikethrough)
        text = text.Replace("**", "");
        text = text.Replace("__", "");
        text = text.Replace("~~", "");
        text = text.Replace("*", "");
        text = text.Replace("_", " ");

        // Collapse multiple spaces
        text = MultipleSpacesRegex().Replace(text, " ");

        return text.Trim();
    }

    [GeneratedRegex(@"<@!?\d+>")]
    private static partial Regex UserMentionRegex();

    [GeneratedRegex(@"<#\d+>")]
    private static partial Regex ChannelMentionRegex();

    [GeneratedRegex(@"<@&\d+>")]
    private static partial Regex RoleMentionRegex();

    [GeneratedRegex(@"<a?:\w+:\d+>")]
    private static partial Regex CustomEmojiRegex();

    [GeneratedRegex(@"https?://\S+")]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"```[\s\S]*?```")]
    private static partial Regex CodeBlockRegex();

    [GeneratedRegex(@"`[^`]+`")]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultipleSpacesRegex();
}
