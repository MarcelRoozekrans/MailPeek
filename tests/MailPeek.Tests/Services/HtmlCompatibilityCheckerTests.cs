using MailPeek.Models;
using MailPeek.Services;

namespace MailPeek.Tests.Services;

public class HtmlCompatibilityCheckerTests
{
    [Fact]
    public void Analyze_ReturnsFullScoreForCleanHtml()
    {
        var html = "<table width=\"600\"><tr><td style=\"font-size:14px;\">Hello</td></tr></table>";
        var result = HtmlCompatibilityChecker.Analyze(html);
        Assert.Equal(100, result.Score);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Analyze_DetectsFlexbox()
    {
        var html = "<div style=\"display:flex\"><div>Item</div></div>";
        var result = HtmlCompatibilityChecker.Analyze(html);
        Assert.Contains(result.Issues, i => string.Equals(i.RuleId, "no-flexbox", StringComparison.Ordinal));
        Assert.Contains(result.Issues, i => i.AffectedClients.Contains("Outlook", StringComparer.Ordinal));
    }

    [Fact]
    public void Analyze_DetectsGrid()
    {
        var html = "<div style=\"display:grid\"><div>Item</div></div>";
        var result = HtmlCompatibilityChecker.Analyze(html);
        Assert.Contains(result.Issues, i => string.Equals(i.RuleId, "no-grid", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_DetectsDivLayout()
    {
        var html = "<div><div>Column 1</div><div>Column 2</div></div>";
        var result = HtmlCompatibilityChecker.Analyze(html);
        Assert.Contains(result.Issues, i => string.Equals(i.RuleId, "prefer-table-layout", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_DetectsBackgroundImage()
    {
        var html = "<div style=\"background-image:url('bg.png')\">Content</div>";
        var result = HtmlCompatibilityChecker.Analyze(html);
        Assert.Contains(result.Issues, i => string.Equals(i.RuleId, "no-background-image", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_DetectsPositionAbsolute()
    {
        var html = "<div style=\"position:absolute\">Floating</div>";
        var result = HtmlCompatibilityChecker.Analyze(html);
        Assert.Contains(result.Issues, i => string.Equals(i.RuleId, "no-position", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_DetectsMissingImgAlt()
    {
        var html = "<img src=\"logo.png\">";
        var result = HtmlCompatibilityChecker.Analyze(html);
        Assert.Contains(result.Issues, i => string.Equals(i.RuleId, "img-alt-required", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_DetectsHeadStyles()
    {
        var html = "<html><head><style>body { color: red; }</style></head><body>Hi</body></html>";
        var result = HtmlCompatibilityChecker.Analyze(html);
        Assert.Contains(result.Issues, i => string.Equals(i.RuleId, "no-head-style", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_DetectsMediaQueries()
    {
        var html = "<style>@media (max-width: 600px) { .col { width: 100%; } }</style><div class=\"col\">Hi</div>";
        var result = HtmlCompatibilityChecker.Analyze(html);
        Assert.Contains(result.Issues, i => string.Equals(i.RuleId, "limited-media-queries", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_DetectsFormElements()
    {
        var html = "<form><input type=\"text\"><button>Submit</button></form>";
        var result = HtmlCompatibilityChecker.Analyze(html);
        Assert.Contains(result.Issues, i => string.Equals(i.RuleId, "no-form-elements", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_DetectsBorderRadius()
    {
        var html = "<div style=\"border-radius:8px\">Rounded</div>";
        var result = HtmlCompatibilityChecker.Analyze(html);
        Assert.Contains(result.Issues, i => string.Equals(i.RuleId, "no-border-radius", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_DetectsVideoAudio()
    {
        var html = "<video src=\"clip.mp4\"></video>";
        var result = HtmlCompatibilityChecker.Analyze(html);
        Assert.Contains(result.Issues, i => string.Equals(i.RuleId, "no-video-audio", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_DetectsMissingImgWidth()
    {
        var html = "<img src=\"logo.png\" alt=\"Logo\">";
        var result = HtmlCompatibilityChecker.Analyze(html);
        Assert.Contains(result.Issues, i => string.Equals(i.RuleId, "img-width-required", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_ScoreClampedToZero()
    {
        var html = "<html><head><style>@media(max-width:600px){}</style></head><body><div style=\"display:flex;position:absolute;background-image:url(x);border-radius:5px\"><video src=\"v.mp4\"></video><img src=\"x.png\"><form><input><button>Go</button></form></div></body></html>";
        var result = HtmlCompatibilityChecker.Analyze(html);
        Assert.True(result.Score >= 0);
    }

    [Fact]
    public void Analyze_ReturnsEmptyForNullHtml()
    {
        var result = HtmlCompatibilityChecker.Analyze(null);
        Assert.Equal(100, result.Score);
        Assert.Empty(result.Issues);
    }
}
