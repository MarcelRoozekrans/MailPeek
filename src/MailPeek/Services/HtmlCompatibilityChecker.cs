using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using MailPeek.Hubs;
using MailPeek.Models;
using MailPeek.Storage;
using Microsoft.Extensions.Logging;

namespace MailPeek.Services;

public partial class HtmlCompatibilityChecker(
    IMessageStore store,
    MailPeekHubNotifier hubNotifier,
    ILogger<HtmlCompatibilityChecker> logger)
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
            message.HtmlCompatibilityResult = Analyze(message.HtmlBody);
            message.HtmlCompatibilityCheckComplete = true;
            await hubNotifier.NotifyHtmlCompatibilityCheckComplete(message.Id).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogWarning(ex, "HTML compatibility check failed for message {MessageId}", message.Id);
            message.HtmlCompatibilityResult = new HtmlCompatibilityResult { Score = -1 };
            message.HtmlCompatibilityCheckComplete = true;
        }
    }

    public static HtmlCompatibilityResult Analyze(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return new HtmlCompatibilityResult { Score = 100 };

        var issues = new List<HtmlCompatibilityIssue>();

        if (FlexboxRegex().IsMatch(html))
            issues.Add(new HtmlCompatibilityIssue { RuleId = "no-flexbox", Description = "CSS flexbox is not supported in Outlook and many webmail clients", Severity = IssueSeverity.Critical, AffectedClients = ["Outlook", "Gmail (partial)", "Yahoo Mail"] });

        if (GridRegex().IsMatch(html))
            issues.Add(new HtmlCompatibilityIssue { RuleId = "no-grid", Description = "CSS Grid is not supported in most email clients", Severity = IssueSeverity.Critical, AffectedClients = ["Outlook", "Gmail", "Yahoo Mail", "Apple Mail (partial)"] });

        if (PositionRegex().IsMatch(html))
            issues.Add(new HtmlCompatibilityIssue { RuleId = "no-position", Description = "CSS position (absolute/relative/fixed) is not supported in most email clients", Severity = IssueSeverity.Critical, AffectedClients = ["Outlook", "Gmail", "Yahoo Mail"] });

        if (DivLayoutRegex().IsMatch(html) && !TableLayoutRegex().IsMatch(html))
            issues.Add(new HtmlCompatibilityIssue { RuleId = "prefer-table-layout", Description = "Use <table> layout instead of <div> for reliable rendering in Outlook (Word renderer)", Severity = IssueSeverity.Major, AffectedClients = ["Outlook"] });

        if (BackgroundImageRegex().IsMatch(html))
            issues.Add(new HtmlCompatibilityIssue { RuleId = "no-background-image", Description = "CSS background-image is not supported in Outlook", Severity = IssueSeverity.Major, AffectedClients = ["Outlook", "Gmail (partial)"] });

        if (HeadStyleRegex().IsMatch(html))
            issues.Add(new HtmlCompatibilityIssue { RuleId = "no-head-style", Description = "Styles in <head> are stripped by Gmail; use inline styles instead", Severity = IssueSeverity.Major, AffectedClients = ["Gmail"] });

        if (FormElementRegex().IsMatch(html))
            issues.Add(new HtmlCompatibilityIssue { RuleId = "no-form-elements", Description = "Form elements are not supported in most email clients", Severity = IssueSeverity.Major, AffectedClients = ["Gmail", "Outlook", "Yahoo Mail"] });

        if (VideoAudioRegex().IsMatch(html))
            issues.Add(new HtmlCompatibilityIssue { RuleId = "no-video-audio", Description = "<video> and <audio> elements are not supported in email clients", Severity = IssueSeverity.Major, AffectedClients = ["All clients"] });

        if (BorderRadiusRegex().IsMatch(html))
            issues.Add(new HtmlCompatibilityIssue { RuleId = "no-border-radius", Description = "border-radius is not supported in Outlook", Severity = IssueSeverity.Minor, AffectedClients = ["Outlook"] });

        if (MediaQueryRegex().IsMatch(html))
            issues.Add(new HtmlCompatibilityIssue { RuleId = "limited-media-queries", Description = "@media queries have limited support in email clients", Severity = IssueSeverity.Minor, AffectedClients = ["Gmail", "Outlook", "Yahoo Mail (partial)"] });

        if (ImgWithoutAltRegex().IsMatch(html))
            issues.Add(new HtmlCompatibilityIssue { RuleId = "img-alt-required", Description = "Images should have alt attributes for accessibility", Severity = IssueSeverity.Minor, AffectedClients = ["All clients (accessibility)"] });

        if (ImgWithoutWidthRegex().IsMatch(html))
            issues.Add(new HtmlCompatibilityIssue { RuleId = "img-width-required", Description = "Images should have width attribute for consistent rendering in Outlook", Severity = IssueSeverity.Minor, AffectedClients = ["Outlook"] });

        var penalty = 0;
        foreach (ref readonly var issue in CollectionsMarshal.AsSpan(issues))
        {
            penalty += issue.Severity switch
            {
                IssueSeverity.Critical => 10,
                IssueSeverity.Major => 5,
                IssueSeverity.Minor => 2,
                _ => 0
            };
        }

        return new HtmlCompatibilityResult
        {
            Score = Math.Max(0, 100 - penalty),
            Issues = issues
        };
    }

    [GeneratedRegex(@"display\s*:\s*flex", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FlexboxRegex();

    [GeneratedRegex(@"display\s*:\s*grid", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GridRegex();

    [GeneratedRegex(@"position\s*:\s*(absolute|relative|fixed)", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PositionRegex();

    [GeneratedRegex(@"<div[\s>]", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DivLayoutRegex();

    [GeneratedRegex(@"<table[\s>]", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TableLayoutRegex();

    [GeneratedRegex(@"background-image\s*:", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BackgroundImageRegex();

    [GeneratedRegex(@"<head[^>]*>.*?<style", RegexOptions.IgnoreCase | RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HeadStyleRegex();

    [GeneratedRegex(@"<(?:form|input|button|select|textarea)[\s>]", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FormElementRegex();

    [GeneratedRegex(@"<(?:video|audio)[\s>]", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VideoAudioRegex();

    [GeneratedRegex(@"border-radius\s*:", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BorderRadiusRegex();

    [GeneratedRegex(@"@media\s*[\(\{]", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MediaQueryRegex();

    [GeneratedRegex(@"<img\s+(?![^>]*\balt\s*=)[^>]*>", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ImgWithoutAltRegex();

    [GeneratedRegex(@"<img\s+(?![^>]*\bwidth\s*=)[^>]*>", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ImgWithoutWidthRegex();
}
