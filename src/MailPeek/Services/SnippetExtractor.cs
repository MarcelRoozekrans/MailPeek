using System.Text.RegularExpressions;
using MailPeek.Models;

namespace MailPeek.Services;

public static partial class SnippetExtractor
{
    private const int MaxLength = 120;

    public static string Extract(StoredMessage message)
    {
        string? text = message.TextBody;

        if (string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(message.HtmlBody))
        {
            text = StripHtml(message.HtmlBody);
        }

        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        text = CollapseWhitespace(text).Trim();

        return text.Length > MaxLength
            ? string.Concat(text.AsSpan(0, MaxLength), "...")
            : text;
    }

    private static string StripHtml(string html)
    {
        var stripped = StyleScriptRegex().Replace(html, " ");
        stripped = HtmlTagRegex().Replace(stripped, " ");
        stripped = stripped
            .Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("&amp;", "&", StringComparison.OrdinalIgnoreCase)
            .Replace("&lt;", "<", StringComparison.OrdinalIgnoreCase)
            .Replace("&gt;", ">", StringComparison.OrdinalIgnoreCase)
            .Replace("&quot;", "\"", StringComparison.OrdinalIgnoreCase);
        return stripped;
    }

    private static string CollapseWhitespace(string text) =>
        WhitespaceRegex().Replace(text, " ");

    [GeneratedRegex(@"<(?<tag>style|script)[^>]*>.*?</\k<tag>>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex StyleScriptRegex();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\s+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex WhitespaceRegex();
}
