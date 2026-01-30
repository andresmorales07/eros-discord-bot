using ErosTTS.Bot.Utilities;

namespace ErosTTS.Bot.Tests.Utilities;

public class TextSanitizerTests
{
    [Fact]
    public void Sanitize_WithNullInput_ReturnsEmptyString()
    {
        var result = TextSanitizer.Sanitize(null!);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Sanitize_WithEmptyString_ReturnsEmptyString()
    {
        var result = TextSanitizer.Sanitize(string.Empty);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Sanitize_WithWhitespaceOnly_ReturnsEmptyString()
    {
        var result = TextSanitizer.Sanitize("   \t\n  ");

        result.Should().BeEmpty();
    }

    [Fact]
    public void Sanitize_WithPlainText_ReturnsUnchangedText()
    {
        var result = TextSanitizer.Sanitize("Hello world");

        result.Should().Be("Hello world");
    }

    [Theory]
    [InlineData("<@123456789>", "")]
    [InlineData("<@!123456789>", "")]
    [InlineData("Hello <@123> world", "Hello world")]
    [InlineData("<@!999> says hi", "says hi")]
    public void Sanitize_WithUserMention_RemovesMention(string input, string expected)
    {
        var result = TextSanitizer.Sanitize(input);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("<#123456789>", "")]
    [InlineData("Check out <#12345>", "Check out")]
    [InlineData("Go to <#111> and <#222>", "Go to and")]
    public void Sanitize_WithChannelMention_RemovesMention(string input, string expected)
    {
        var result = TextSanitizer.Sanitize(input);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("<@&123456789>", "")]
    [InlineData("Hey <@&999> members", "Hey members")]
    public void Sanitize_WithRoleMention_RemovesMention(string input, string expected)
    {
        var result = TextSanitizer.Sanitize(input);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("<:emoji:123456789>", "")]
    [InlineData("<a:animated:123456789>", "")]
    [InlineData("Cool <:fire:111> stuff", "Cool stuff")]
    [InlineData("Nice <a:dance:222> move", "Nice move")]
    public void Sanitize_WithCustomEmoji_RemovesEmoji(string input, string expected)
    {
        var result = TextSanitizer.Sanitize(input);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("https://example.com", "")]
    [InlineData("http://example.com/path?query=1", "")]
    [InlineData("Check https://google.com please", "Check please")]
    [InlineData("Visit http://site.com and http://other.net", "Visit and")]
    public void Sanitize_WithUrl_RemovesUrl(string input, string expected)
    {
        var result = TextSanitizer.Sanitize(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void Sanitize_WithCodeBlock_RemovesCodeBlock()
    {
        var input = "Here is code:\n```csharp\nvar x = 1;\n```\nEnd";

        var result = TextSanitizer.Sanitize(input);

        result.Should().Be("Here is code: End");
    }

    [Fact]
    public void Sanitize_WithInlineCode_RemovesInlineCode()
    {
        var input = "Use `Console.WriteLine` to print";

        var result = TextSanitizer.Sanitize(input);

        result.Should().Be("Use to print");
    }

    [Theory]
    [InlineData("**bold**", "bold")]
    [InlineData("__underline__", "underline")]
    [InlineData("~~strikethrough~~", "strikethrough")]
    [InlineData("*italic*", "italic")]
    public void Sanitize_WithMarkdown_RemovesFormatting(string input, string expected)
    {
        var result = TextSanitizer.Sanitize(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void Sanitize_WithUnderscore_ReplacesWithSpace()
    {
        var result = TextSanitizer.Sanitize("hello_world");

        result.Should().Be("hello world");
    }

    [Fact]
    public void Sanitize_WithMultipleSpaces_CollapsesToSingleSpace()
    {
        var result = TextSanitizer.Sanitize("Hello    world   test");

        result.Should().Be("Hello world test");
    }

    [Fact]
    public void Sanitize_WithMixedContent_RemovesAllSpecialFormattingAndTrims()
    {
        var input = "  <@123> says **hello** to <#456> at https://test.com  ";

        var result = TextSanitizer.Sanitize(input);

        result.Should().Be("says hello to at");
    }

    [Fact]
    public void Sanitize_PreservesUnicodeEmoji()
    {
        var result = TextSanitizer.Sanitize("Hello world \ud83d\udc4b");

        result.Should().Contain("\ud83d\udc4b");
    }

    [Fact]
    public void Sanitize_PreservesNormalPunctuation()
    {
        var result = TextSanitizer.Sanitize("Hello, world! How are you?");

        result.Should().Be("Hello, world! How are you?");
    }

    [Fact]
    public void Sanitize_WithNestedMarkdown_RemovesAll()
    {
        var result = TextSanitizer.Sanitize("***bold italic***");

        result.Should().Be("bold italic");
    }

    [Fact]
    public void Sanitize_WithMultipleMentionTypes_RemovesAll()
    {
        var input = "<@111> mentioned <@&222> in <#333>";

        var result = TextSanitizer.Sanitize(input);

        result.Should().Be("mentioned in");
    }
}
