#pragma warning disable MA0074 // xUnit Assert.Contains/DoesNotContain don't have StringComparison overloads
using MailPeek.Models;
using MailPeek.Services;

namespace MailPeek.Tests.Services;

public class SnippetExtractorTests
{
    [Fact]
    public void Extract_PrefersTextBody()
    {
        var msg = new StoredMessage
        {
            From = "a@b.com", To = ["c@d.com"], Subject = "Test",
            TextBody = "Hello world from text body",
            HtmlBody = "<p>Hello from HTML</p>"
        };
        var snippet = SnippetExtractor.Extract(msg);
        Assert.Equal("Hello world from text body", snippet);
    }

    [Fact]
    public void Extract_FallsBackToHtmlBody()
    {
        var msg = new StoredMessage
        {
            From = "a@b.com", To = ["c@d.com"], Subject = "Test",
            HtmlBody = "<p>Hello from <strong>HTML</strong></p>"
        };
        var snippet = SnippetExtractor.Extract(msg);
        Assert.Equal("Hello from HTML", snippet);
    }

    [Fact]
    public void Extract_TruncatesAt120Chars()
    {
        var longText = new string('A', 200);
        var msg = new StoredMessage
        {
            From = "a@b.com", To = ["c@d.com"], Subject = "Test",
            TextBody = longText
        };
        var snippet = SnippetExtractor.Extract(msg);
        Assert.Equal(123, snippet.Length);
        Assert.EndsWith("...", snippet);
    }

    [Fact]
    public void Extract_ReturnsEmptyForNoBody()
    {
        var msg = new StoredMessage
        {
            From = "a@b.com", To = ["c@d.com"], Subject = "Test"
        };
        var snippet = SnippetExtractor.Extract(msg);
        Assert.Equal(string.Empty, snippet);
    }

    [Fact]
    public void Extract_StripsHtmlTags()
    {
        var msg = new StoredMessage
        {
            From = "a@b.com", To = ["c@d.com"], Subject = "Test",
            HtmlBody = "<html><head><style>body{}</style></head><body><h1>Title</h1><p>Content here</p></body></html>"
        };
        var snippet = SnippetExtractor.Extract(msg);
        Assert.DoesNotContain("<", snippet);
        Assert.Contains("Title", snippet);
        Assert.Contains("Content here", snippet);
    }

    [Fact]
    public void Extract_CollapsesWhitespace()
    {
        var msg = new StoredMessage
        {
            From = "a@b.com", To = ["c@d.com"], Subject = "Test",
            TextBody = "Hello   \n\n  world"
        };
        var snippet = SnippetExtractor.Extract(msg);
        Assert.Equal("Hello world", snippet);
    }
}
