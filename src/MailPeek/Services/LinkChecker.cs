using System.Text.RegularExpressions;
using MailPeek.Hubs;
using MailPeek.Models;
using MailPeek.Storage;
using Microsoft.Extensions.Logging;

namespace MailPeek.Services;

public partial class LinkChecker(
    IMessageStore store,
    IHttpClientFactory httpClientFactory,
    MailPeekHubNotifier hubNotifier,
    ILogger<LinkChecker> logger)
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    public void Start()
    {
        store.OnMessageReceived += OnMessageReceived;
    }

    private void OnMessageReceived(StoredMessage message)
    {
        _ = Task.Run(() => CheckLinksAsync(message));
    }

    private async Task CheckLinksAsync(StoredMessage message)
    {
        var urls = ExtractUrls(message);
        if (urls.Count == 0)
        {
            message.LinkCheckResults = [];
            message.LinkCheckComplete = true;
            return;
        }

        var results = new List<LinkCheckResult>();
        using var client = httpClientFactory.CreateClient("LinkChecker");
        client.Timeout = RequestTimeout;

        foreach (var url in urls)
        {
            var result = new LinkCheckResult { Url = url };
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                using var response = await client.SendAsync(request).ConfigureAwait(false);
                result.StatusCode = (int)response.StatusCode;
                result.Status = response.IsSuccessStatusCode ? LinkStatus.Ok : LinkStatus.Broken;
            }
            catch (TaskCanceledException)
            {
                result.Status = LinkStatus.Timeout;
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                logger.LogWarning(ex, "Link check failed for URL {Url}", url);
                result.Status = LinkStatus.Error;
            }
            results.Add(result);
        }

        message.LinkCheckResults = results;
        message.LinkCheckComplete = true;
        await hubNotifier.NotifyLinkCheckComplete(message.Id).ConfigureAwait(false);
    }

#pragma warning disable MA0016
    public static List<string> ExtractUrls(StoredMessage message)
#pragma warning restore MA0016
    {
        var urls = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(message.HtmlBody))
        {
            foreach (Match match in HrefRegex().Matches(message.HtmlBody))
                urls.Add(match.Groups["url"].Value);
        }
        if (!string.IsNullOrEmpty(message.TextBody))
        {
            foreach (Match match in UrlRegex().Matches(message.TextBody))
                urls.Add(match.Value);
        }
        return [.. urls];
    }

    [GeneratedRegex("href=\"(?<url>https?://[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HrefRegex();

    [GeneratedRegex("https?://[^\\s<>\"]+", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UrlRegex();
}
