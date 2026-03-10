using MailPeek.Models;
using MailPeek.Services;

namespace MailPeek.Tests.Services;

public class LinkCheckerTests
{
    [Fact]
    public void ExtractUrls_FromHtmlBody()
    {
        var msg = new StoredMessage
        {
            HtmlBody = "<a href=\"https://example.com\">Link</a> and <a href=\"https://test.com/page\">Another</a>"
        };
        var urls = LinkChecker.ExtractUrls(msg);
        Assert.Equal(2, urls.Count);
        Assert.Contains("https://example.com", urls);
        Assert.Contains("https://test.com/page", urls);
    }

    [Fact]
    public void ExtractUrls_FromTextBody()
    {
        var msg = new StoredMessage
        {
            TextBody = "Visit https://example.com and http://test.com/page for info"
        };
        var urls = LinkChecker.ExtractUrls(msg);
        Assert.Contains("https://example.com", urls);
        Assert.Contains("http://test.com/page", urls);
    }

    [Fact]
    public void ExtractUrls_Deduplicates()
    {
        var msg = new StoredMessage
        {
            HtmlBody = "<a href=\"https://example.com\">Link</a>",
            TextBody = "Visit https://example.com"
        };
#pragma warning disable HLQ005 // False positive on Assert.Single
        Assert.Single(LinkChecker.ExtractUrls(msg));
#pragma warning restore HLQ005
    }

    [Fact]
    public void ExtractUrls_ReturnsEmptyForNoUrls()
    {
        var msg = new StoredMessage { TextBody = "No links here" };
        Assert.Empty(LinkChecker.ExtractUrls(msg));
    }
}
