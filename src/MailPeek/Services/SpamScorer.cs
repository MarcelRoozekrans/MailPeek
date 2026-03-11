using System.Text.RegularExpressions;
using MailPeek.Hubs;
using MailPeek.Models;
using MailPeek.Storage;
using Microsoft.Extensions.Logging;

namespace MailPeek.Services;

public partial class SpamScorer(
    IMessageStore store,
    MailPeekHubNotifier hubNotifier,
    ILogger<SpamScorer> logger)
{
    public void Start()
    {
        store.OnMessageReceived += OnMessageReceived;
    }

    private void OnMessageReceived(StoredMessage message)
    {
        _ = Task.Run(() => RunCheck(message));
    }

    private async Task RunCheck(StoredMessage message)
    {
        try
        {
            message.SpamCheckResult = Analyze(message);
            message.SpamCheckComplete = true;
            await hubNotifier.NotifySpamCheckComplete(message.Id).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogWarning(ex, "Spam check failed for message {MessageId}", message.Id);
            message.SpamCheckResult = new SpamCheckResult { Score = -1, Source = "builtin" };
            message.SpamCheckComplete = true;
        }
    }

    public static SpamCheckResult Analyze(StoredMessage message)
    {
        var rules = new List<SpamCheckRule>();

        // Empty subject (3)
        if (string.IsNullOrWhiteSpace(message.Subject))
            rules.Add(new SpamCheckRule { Name = "EMPTY_SUBJECT", Score = 3, Description = "Subject is empty" });

        // ALL CAPS subject (3)
        if (!string.IsNullOrWhiteSpace(message.Subject) && message.Subject.Length > 5 &&
            string.Equals(message.Subject, message.Subject.ToUpperInvariant(), StringComparison.Ordinal) &&
            LetterRegex().IsMatch(message.Subject))
            rules.Add(new SpamCheckRule { Name = "ALL_CAPS_SUBJECT", Score = 3, Description = "Subject is entirely uppercase" });

        // Excessive punctuation (2)
        if (!string.IsNullOrWhiteSpace(message.Subject) && ExcessivePunctuationRegex().IsMatch(message.Subject))
            rules.Add(new SpamCheckRule { Name = "EXCESSIVE_PUNCTUATION", Score = 2, Description = "Subject contains excessive punctuation" });

        // Missing Message-ID (2)
        if (!message.Headers.ContainsKey("Message-ID") && !message.Headers.ContainsKey("Message-Id"))
            rules.Add(new SpamCheckRule { Name = "MISSING_MESSAGE_ID", Score = 2, Description = "Missing Message-ID header" });

        // Missing Date (2)
        if (!message.Headers.ContainsKey("Date"))
            rules.Add(new SpamCheckRule { Name = "MISSING_DATE", Score = 2, Description = "Missing Date header" });

        // Missing From display name (1)
        if (!string.IsNullOrEmpty(message.From) && !message.From.Contains('<', StringComparison.Ordinal))
            rules.Add(new SpamCheckRule { Name = "MISSING_FROM_NAME", Score = 1, Description = "From address has no display name" });

        // HTML only, no text part (2)
        if (string.IsNullOrWhiteSpace(message.TextBody) && !string.IsNullOrWhiteSpace(message.HtmlBody))
            rules.Add(new SpamCheckRule { Name = "HTML_ONLY", Score = 2, Description = "Message has HTML body but no plain text alternative" });

        // Suspicious phrases (2)
        var bodyText = message.TextBody ?? message.HtmlBody ?? "";
        if (SuspiciousPhrasesRegex().IsMatch(bodyText))
            rules.Add(new SpamCheckRule { Name = "SUSPICIOUS_PHRASES", Score = 2, Description = "Body contains suspicious phrases" });

        // URL shorteners (2)
        if (UrlShortenerRegex().IsMatch(bodyText))
            rules.Add(new SpamCheckRule { Name = "URL_SHORTENER", Score = 2, Description = "Body contains URL shortener links" });

        // Excessive links (1)
        if (!string.IsNullOrEmpty(message.HtmlBody))
        {
            var linkCount = LinkCountRegex().Matches(message.HtmlBody).Count;
            if (linkCount > 10)
                rules.Add(new SpamCheckRule { Name = "EXCESSIVE_LINKS", Score = 1, Description = "Body contains " + linkCount + " links (>10)" });
        }

        var score = 0.0;
        foreach (ref readonly var rule in System.Runtime.InteropServices.CollectionsMarshal.AsSpan(rules))
            score += rule.Score;

        return new SpamCheckResult { Score = score, Source = "builtin", Rules = rules };
    }

    [GeneratedRegex(@"[a-zA-Z]", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LetterRegex();

    [GeneratedRegex(@"[!?]{3,}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ExcessivePunctuationRegex();

    [GeneratedRegex(@"\b(act now|limited time|click here|buy now|free|winner|congratulations|urgent)\b", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SuspiciousPhrasesRegex();

    [GeneratedRegex(@"https?://(bit\.ly|tinyurl\.com|t\.co|goo\.gl|ow\.ly|is\.gd|buff\.ly)/", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UrlShortenerRegex();

    [GeneratedRegex(@"<a\s", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LinkCountRegex();
}
