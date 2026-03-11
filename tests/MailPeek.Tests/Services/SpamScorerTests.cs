using MailPeek.Models;
using MailPeek.Services;

namespace MailPeek.Tests.Services;

public class SpamScorerTests
{
    [Fact]
    public void Analyze_ReturnsZeroForCleanMessage()
    {
        var msg = new StoredMessage
        {
            From = "John <john@example.com>",
            To = ["jane@example.com"],
            Subject = "Meeting tomorrow",
            TextBody = "Hi Jane, can we meet tomorrow at 10am?",
            HtmlBody = "<p>Hi Jane, can we meet tomorrow at 10am?</p>",
            Headers = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Message-ID"] = "<abc@example.com>",
                ["Date"] = "Tue, 11 Mar 2026 10:00:00 +0000"
            }
        };
        var result = SpamScorer.Analyze(msg);
        Assert.Equal(0, result.Score);
        Assert.Equal("builtin", result.Source);
    }

    [Fact]
    public void Analyze_DetectsEmptySubject()
    {
        var msg = CreateBasicMessage();
        msg.Subject = "";
        var result = SpamScorer.Analyze(msg);
        Assert.Contains(result.Rules, r => string.Equals(r.Name, "EMPTY_SUBJECT", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_DetectsAllCapsSubject()
    {
        var msg = CreateBasicMessage();
        msg.Subject = "FREE MONEY NOW LIMITED TIME";
        var result = SpamScorer.Analyze(msg);
        Assert.Contains(result.Rules, r => string.Equals(r.Name, "ALL_CAPS_SUBJECT", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_DetectsExcessivePunctuation()
    {
        var msg = CreateBasicMessage();
        msg.Subject = "Buy now!!!";
        var result = SpamScorer.Analyze(msg);
        Assert.Contains(result.Rules, r => string.Equals(r.Name, "EXCESSIVE_PUNCTUATION", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_DetectsMissingMessageId()
    {
        var msg = CreateBasicMessage();
        msg.Headers = new Dictionary<string, string>(StringComparer.Ordinal);
        var result = SpamScorer.Analyze(msg);
        Assert.Contains(result.Rules, r => string.Equals(r.Name, "MISSING_MESSAGE_ID", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_DetectsHtmlOnlyNoTextPart()
    {
        var msg = CreateBasicMessage();
        msg.TextBody = null;
        msg.HtmlBody = "<p>HTML only</p>";
        var result = SpamScorer.Analyze(msg);
        Assert.Contains(result.Rules, r => string.Equals(r.Name, "HTML_ONLY", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_DetectsSuspiciousPhrases()
    {
        var msg = CreateBasicMessage();
        msg.TextBody = "Act now before this limited time offer expires! Click here!";
        var result = SpamScorer.Analyze(msg);
        Assert.Contains(result.Rules, r => string.Equals(r.Name, "SUSPICIOUS_PHRASES", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_DetectsUrlShorteners()
    {
        var msg = CreateBasicMessage();
        msg.TextBody = "Check this out: https://bit.ly/abc123";
        var result = SpamScorer.Analyze(msg);
        Assert.Contains(result.Rules, r => string.Equals(r.Name, "URL_SHORTENER", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_DetectsMissingFromDisplayName()
    {
        var msg = CreateBasicMessage();
        msg.From = "noreply@example.com";
        var result = SpamScorer.Analyze(msg);
        Assert.Contains(result.Rules, r => string.Equals(r.Name, "MISSING_FROM_NAME", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_SourceIsBuiltin()
    {
        var msg = CreateBasicMessage();
        var result = SpamScorer.Analyze(msg);
        Assert.Equal("builtin", result.Source);
    }

    private static StoredMessage CreateBasicMessage() => new()
    {
        From = "John <john@example.com>",
        To = ["jane@example.com"],
        Subject = "Hello",
        TextBody = "Normal message content",
        HtmlBody = "<p>Normal message content</p>",
        Headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Message-ID"] = "<abc@example.com>",
            ["Date"] = "Tue, 11 Mar 2026 10:00:00 +0000"
        }
    };
}
