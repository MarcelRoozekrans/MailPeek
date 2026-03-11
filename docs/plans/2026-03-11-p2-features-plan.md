# P2 Feature Batch Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add four P2 features: message preview snippets, keyboard shortcuts, HTML compatibility scoring, and spam score analysis.

**Architecture:** Each analysis feature (HTML compat, spam) follows the existing LinkChecker pattern — subscribe to `OnMessageReceived`, run background analysis, store results on `StoredMessage`, notify via SignalR. Snippet extraction happens at message creation time in the SMTP handler. Keyboard shortcuts are pure frontend JS.

**Tech Stack:** C# / .NET 8+9, xUnit, vanilla JS, CSS, SignalR

---

### Task 1: Message Preview Snippet — Model + Extraction

**Files:**
- Modify: `src/MailPeek/Models/StoredMessage.cs:11-12` (add Snippet property)
- Modify: `src/MailPeek/Models/MessageSummary.cs:8-9` (add Snippet field)
- Modify: `src/MailPeek/Models/StoredMessage.cs:28-38` (update ToSummary)
- Create: `src/MailPeek/Services/SnippetExtractor.cs`
- Test: `tests/MailPeek.Tests/Services/SnippetExtractorTests.cs`

**Step 1: Write the failing tests**

```csharp
// tests/MailPeek.Tests/Services/SnippetExtractorTests.cs
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
        Assert.Equal(123, snippet.Length); // 120 + "..."
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
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MailPeek.Tests/ --filter "FullyQualifiedName~SnippetExtractorTests" --verbosity quiet`
Expected: FAIL — `SnippetExtractor` class does not exist

**Step 3: Write minimal implementation**

```csharp
// src/MailPeek/Services/SnippetExtractor.cs
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
        // Remove style/script blocks first
        var stripped = StyleScriptRegex().Replace(html, " ");
        // Remove all HTML tags
        stripped = HtmlTagRegex().Replace(stripped, " ");
        // Decode common entities
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

    [GeneratedRegex(@"<(style|script)[^>]*>.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex StyleScriptRegex();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\s+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex WhitespaceRegex();
}
```

Now add `Snippet` to the models:

```csharp
// In StoredMessage.cs, add after line 11 (Subject):
public string Snippet { get; set; } = string.Empty;
```

```csharp
// In MessageSummary.cs, add after line 8 (Subject):
public required string Snippet { get; set; }
```

```csharp
// In StoredMessage.cs ToSummary(), add after Subject = Subject,:
Snippet = Snippet,
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/MailPeek.Tests/ --filter "FullyQualifiedName~SnippetExtractorTests" --verbosity quiet`
Expected: PASS (6 tests)

**Step 5: Commit**

```bash
git add src/MailPeek/Services/SnippetExtractor.cs src/MailPeek/Models/StoredMessage.cs src/MailPeek/Models/MessageSummary.cs tests/MailPeek.Tests/Services/SnippetExtractorTests.cs
git commit -m "feat: add snippet extraction for message previews"
```

---

### Task 2: Message Preview Snippet — Wire Up + UI

**Files:**
- Modify: `src/MailPeek/Smtp/MailPeekMessageStore.cs` (call SnippetExtractor during message creation)
- Modify: `src/MailPeek/Assets/js/dashboard.js` (render snippet in inbox rows)
- Modify: `src/MailPeek/Assets/css/dashboard.css` (style the snippet line)

**Step 1: Wire snippet extraction into SMTP message handler**

Find where `StoredMessage` is created from the MIME message in `MailPeekMessageStore.cs`. After setting `TextBody` and `HtmlBody`, add:

```csharp
storedMessage.Snippet = SnippetExtractor.Extract(storedMessage);
```

Add `using MailPeek.Services;` to the top.

**Step 2: Update dashboard.js to render snippet**

In `renderInbox()`, where the subject cell is created, add a snippet line after the subject text:

```javascript
// After the subject text and tag pills, add:
if (msg.snippet) {
    const snippetEl = document.createElement('div');
    snippetEl.className = 'msg-snippet';
    snippetEl.textContent = msg.snippet;
    tdSubject.appendChild(snippetEl);
}
```

**Step 3: Add CSS for snippet**

```css
/* In dashboard.css */
.msg-snippet {
    font-size: 0.8rem;
    color: var(--text-muted);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    max-width: 400px;
    margin-top: 2px;
}

@media (max-width: 768px) {
    .msg-snippet { display: none; }
}
```

**Step 4: Run all tests to verify nothing broke**

Run: `dotnet test tests/MailPeek.Tests/ --verbosity quiet`
Expected: All tests PASS

**Step 5: Commit**

```bash
git add src/MailPeek/Smtp/MailPeekMessageStore.cs src/MailPeek/Assets/js/dashboard.js src/MailPeek/Assets/css/dashboard.css
git commit -m "feat: display message snippet in inbox rows"
```

---

### Task 3: Keyboard Shortcuts — Implementation

**Files:**
- Modify: `src/MailPeek/Assets/js/dashboard.js` (add keyboard event handler)
- Modify: `src/MailPeek/Assets/css/dashboard.css` (add .focused row style)
- Modify: `src/MailPeek/Assets/index.html` (add help overlay)

**Step 1: Add focused row CSS**

```css
/* In dashboard.css, after the tr.selected styles */
#messageTable tbody tr.focused {
    outline: 2px solid var(--primary);
    outline-offset: -2px;
}

/* Keyboard help overlay */
.keyboard-help-overlay {
    display: none;
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.5);
    z-index: 200;
    justify-content: center;
    align-items: center;
}
.keyboard-help-overlay.visible { display: flex; }
.keyboard-help {
    background: var(--surface);
    border-radius: 8px;
    padding: 24px;
    max-width: 400px;
    width: 90%;
    box-shadow: 0 8px 32px rgba(0, 0, 0, 0.3);
}
.keyboard-help h3 { margin: 0 0 16px; }
.keyboard-help table { width: 100%; }
.keyboard-help kbd {
    background: var(--bg);
    border: 1px solid var(--border);
    border-radius: 4px;
    padding: 2px 8px;
    font-family: monospace;
    font-size: 0.85rem;
}
.keyboard-help td { padding: 4px 0; }
```

**Step 2: Add keyboard help overlay to index.html**

Add before `</main>`:

```html
<div id="keyboardHelp" class="keyboard-help-overlay">
    <div class="keyboard-help">
        <h3>Keyboard Shortcuts</h3>
        <table>
            <tr><td><kbd>j</kbd></td><td>Next message</td></tr>
            <tr><td><kbd>k</kbd></td><td>Previous message</td></tr>
            <tr><td><kbd>Enter</kbd></td><td>Open message</td></tr>
            <tr><td><kbd>Delete</kbd></td><td>Delete message</td></tr>
            <tr><td><kbd>Esc</kbd></td><td>Back to inbox / Close</td></tr>
            <tr><td><kbd>?</kbd></td><td>Show this help</td></tr>
        </table>
    </div>
</div>
```

**Step 3: Add keyboard handler to dashboard.js**

Add state variable at the top with other state:

```javascript
let focusedIndex = -1;
```

Add in `setupEventListeners()`:

```javascript
document.addEventListener('keydown', handleKeyDown);
document.getElementById('keyboardHelp').addEventListener('click', (e) => {
    if (e.target.id === 'keyboardHelp') hideKeyboardHelp();
});
```

Add new functions:

```javascript
function handleKeyDown(e) {
    // Don't intercept when typing in inputs
    if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;

    const rows = document.querySelectorAll('#messageTable tbody tr');
    const detailVisible = document.getElementById('messageDetail').style.display !== 'none';

    switch (e.key) {
        case 'j':
            e.preventDefault();
            if (!detailVisible && rows.length > 0) {
                focusedIndex = Math.min(focusedIndex + 1, rows.length - 1);
                updateFocusedRow(rows);
            }
            break;
        case 'k':
            e.preventDefault();
            if (!detailVisible && rows.length > 0) {
                focusedIndex = Math.max(focusedIndex - 1, 0);
                updateFocusedRow(rows);
            }
            break;
        case 'Enter':
            e.preventDefault();
            if (!detailVisible && focusedIndex >= 0 && focusedIndex < rows.length) {
                rows[focusedIndex].click();
            }
            break;
        case 'Delete':
        case 'Backspace':
            if (!detailVisible && focusedIndex >= 0 && focusedIndex < rows.length) {
                e.preventDefault();
                const deleteBtn = rows[focusedIndex].querySelector('.btn-delete');
                if (deleteBtn) deleteBtn.click();
                if (focusedIndex >= rows.length - 1) focusedIndex = rows.length - 2;
            }
            break;
        case 'Escape':
            if (document.getElementById('keyboardHelp').classList.contains('visible')) {
                hideKeyboardHelp();
            } else if (detailVisible) {
                e.preventDefault();
                document.querySelector('.btn-back')?.click();
            }
            break;
        case '?':
            if (!detailVisible) {
                e.preventDefault();
                showKeyboardHelp();
            }
            break;
    }
}

function updateFocusedRow(rows) {
    rows.forEach(r => r.classList.remove('focused'));
    if (focusedIndex >= 0 && focusedIndex < rows.length) {
        rows[focusedIndex].classList.add('focused');
        rows[focusedIndex].scrollIntoView({ block: 'nearest' });
    }
}

function showKeyboardHelp() {
    document.getElementById('keyboardHelp').classList.add('visible');
}

function hideKeyboardHelp() {
    document.getElementById('keyboardHelp').classList.remove('visible');
}
```

Reset `focusedIndex = -1` in `renderInbox()` after clearing the table body.

**Step 4: Run all tests**

Run: `dotnet test tests/MailPeek.Tests/ --verbosity quiet`
Expected: All tests PASS

**Step 5: Commit**

```bash
git add src/MailPeek/Assets/js/dashboard.js src/MailPeek/Assets/css/dashboard.css src/MailPeek/Assets/index.html
git commit -m "feat: add keyboard shortcuts for inbox navigation"
```

---

### Task 4: HTML Compatibility Scoring — Models

**Files:**
- Create: `src/MailPeek/Models/HtmlCompatibilityResult.cs`
- Create: `src/MailPeek/Models/HtmlCompatibilityIssue.cs`
- Create: `src/MailPeek/Models/IssueSeverity.cs`
- Modify: `src/MailPeek/Models/StoredMessage.cs` (add compat result properties)

**Step 1: Create the models**

```csharp
// src/MailPeek/Models/IssueSeverity.cs
using System.Text.Json.Serialization;

namespace MailPeek.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IssueSeverity
{
    Critical,
    Major,
    Minor
}
```

```csharp
// src/MailPeek/Models/HtmlCompatibilityIssue.cs
namespace MailPeek.Models;

public class HtmlCompatibilityIssue
{
    public required string RuleId { get; set; }
    public required string Description { get; set; }
    public required IssueSeverity Severity { get; set; }
#pragma warning disable MA0016
    public required List<string> AffectedClients { get; set; }
#pragma warning restore MA0016
}
```

```csharp
// src/MailPeek/Models/HtmlCompatibilityResult.cs
namespace MailPeek.Models;

public class HtmlCompatibilityResult
{
    public int Score { get; set; }
#pragma warning disable MA0016
    public List<HtmlCompatibilityIssue> Issues { get; set; } = [];
#pragma warning restore MA0016
}
```

**Step 2: Add properties to StoredMessage**

In `StoredMessage.cs`, add after `LinkCheckComplete` (line 24):

```csharp
public HtmlCompatibilityResult? HtmlCompatibilityResult { get; set; }
public bool HtmlCompatibilityCheckComplete { get; set; }
```

**Step 3: Run all tests to verify compilation**

Run: `dotnet test tests/MailPeek.Tests/ --verbosity quiet`
Expected: All tests PASS (no behavioral changes)

**Step 4: Commit**

```bash
git add src/MailPeek/Models/IssueSeverity.cs src/MailPeek/Models/HtmlCompatibilityIssue.cs src/MailPeek/Models/HtmlCompatibilityResult.cs src/MailPeek/Models/StoredMessage.cs
git commit -m "feat: add HTML compatibility result models"
```

---

### Task 5: HTML Compatibility Scoring — Rule Engine + Tests

**Files:**
- Create: `src/MailPeek/Services/HtmlCompatibilityChecker.cs`
- Create: `tests/MailPeek.Tests/Services/HtmlCompatibilityCheckerTests.cs`

**Step 1: Write the failing tests**

```csharp
// tests/MailPeek.Tests/Services/HtmlCompatibilityCheckerTests.cs
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
        Assert.Contains(result.Issues, i => i.AffectedClients.Contains("Outlook"));
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
        // HTML with many issues should not go below 0
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
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MailPeek.Tests/ --filter "FullyQualifiedName~HtmlCompatibilityCheckerTests" --verbosity quiet`
Expected: FAIL — `HtmlCompatibilityChecker` class does not exist

**Step 3: Write the implementation**

```csharp
// src/MailPeek/Services/HtmlCompatibilityChecker.cs
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

        // Critical rules (weight 10)
        if (FlexboxRegex().IsMatch(html))
            issues.Add(new HtmlCompatibilityIssue { RuleId = "no-flexbox", Description = "CSS flexbox is not supported in Outlook and many webmail clients", Severity = IssueSeverity.Critical, AffectedClients = ["Outlook", "Gmail (partial)", "Yahoo Mail"] });

        if (GridRegex().IsMatch(html))
            issues.Add(new HtmlCompatibilityIssue { RuleId = "no-grid", Description = "CSS Grid is not supported in most email clients", Severity = IssueSeverity.Critical, AffectedClients = ["Outlook", "Gmail", "Yahoo Mail", "Apple Mail (partial)"] });

        if (PositionRegex().IsMatch(html))
            issues.Add(new HtmlCompatibilityIssue { RuleId = "no-position", Description = "CSS position (absolute/relative/fixed) is not supported in most email clients", Severity = IssueSeverity.Critical, AffectedClients = ["Outlook", "Gmail", "Yahoo Mail"] });

        // Major rules (weight 5)
        if (DivLayoutRegex().IsMatch(html) && !TableLayoutRegex().IsMatch(html))
            issues.Add(new HtmlCompatibilityIssue { RuleId = "prefer-table-layout", Description = "Use <table> layout instead of <div> for reliable rendering in Outlook (Word renderer)", Severity = IssueSeverity.Major, AffectedClients = ["Outlook"] });

        if (BackgroundImageRegex().IsMatch(html))
            issues.Add(new HtmlCompatibilityIssue { RuleId = "no-background-image", Description = "CSS background-image is not supported in Outlook", Severity = IssueSeverity.Major, AffectedClients = ["Outlook", "Gmail (partial)"] });

        if (HeadStyleRegex().IsMatch(html))
            issues.Add(new HtmlCompatibilityIssue { RuleId = "no-head-style", Description = "Styles in <head> are stripped by Gmail; use inline styles instead", Severity = IssueSeverity.Major, AffectedClients = ["Gmail"] });

        if (FormElementRegex().IsMatch(html))
            issues.Add(new HtmlCompatibilityIssue { RuleId = "no-form-elements", Description = "Form elements (<input>, <button>, <form>) are not supported in most email clients", Severity = IssueSeverity.Major, AffectedClients = ["Gmail", "Outlook", "Yahoo Mail"] });

        if (VideoAudioRegex().IsMatch(html))
            issues.Add(new HtmlCompatibilityIssue { RuleId = "no-video-audio", Description = "<video> and <audio> elements are not supported in email clients", Severity = IssueSeverity.Major, AffectedClients = ["All clients"] });

        // Minor rules (weight 2)
        if (BorderRadiusRegex().IsMatch(html))
            issues.Add(new HtmlCompatibilityIssue { RuleId = "no-border-radius", Description = "border-radius is not supported in Outlook", Severity = IssueSeverity.Minor, AffectedClients = ["Outlook"] });

        if (MediaQueryRegex().IsMatch(html))
            issues.Add(new HtmlCompatibilityIssue { RuleId = "limited-media-queries", Description = "@media queries have limited support in email clients", Severity = IssueSeverity.Minor, AffectedClients = ["Gmail", "Outlook", "Yahoo Mail (partial)"] });

        if (ImgWithoutAltRegex().IsMatch(html))
            issues.Add(new HtmlCompatibilityIssue { RuleId = "img-alt-required", Description = "Images should have alt attributes for accessibility", Severity = IssueSeverity.Minor, AffectedClients = ["All clients (accessibility)"] });

        if (ImgWithoutWidthRegex().IsMatch(html))
            issues.Add(new HtmlCompatibilityIssue { RuleId = "img-width-required", Description = "Images should have width attribute for consistent rendering in Outlook", Severity = IssueSeverity.Minor, AffectedClients = ["Outlook"] });

        // Calculate score
        var penalty = 0;
        foreach (var issue in issues)
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

    [GeneratedRegex(@"position\s*:\s*(absolute|relative|fixed)", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PositionRegex();

    [GeneratedRegex(@"<div[\s>]", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DivLayoutRegex();

    [GeneratedRegex(@"<table[\s>]", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TableLayoutRegex();

    [GeneratedRegex(@"background-image\s*:", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BackgroundImageRegex();

    [GeneratedRegex(@"<head[^>]*>.*?<style", RegexOptions.IgnoreCase | RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HeadStyleRegex();

    [GeneratedRegex(@"<(form|input|button|select|textarea)[\s>]", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FormElementRegex();

    [GeneratedRegex(@"<(video|audio)[\s>]", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
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
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/MailPeek.Tests/ --filter "FullyQualifiedName~HtmlCompatibilityCheckerTests" --verbosity quiet`
Expected: PASS (15 tests)

**Step 5: Commit**

```bash
git add src/MailPeek/Services/HtmlCompatibilityChecker.cs tests/MailPeek.Tests/Services/HtmlCompatibilityCheckerTests.cs
git commit -m "feat: add HTML compatibility rule engine with 13 rules"
```

---

### Task 6: HTML Compatibility — API + SignalR + DI Wiring

**Files:**
- Modify: `src/MailPeek/Hubs/MailPeekHubNotifier.cs` (add NotifyHtmlCompatibilityCheckComplete)
- Modify: `src/MailPeek/Middleware/DashboardApiExtensions.cs` (add GET compatibility endpoint)
- Modify: `src/MailPeek/Extensions/ServiceCollectionExtensions.cs` (register service)
- Modify: `src/MailPeek/Extensions/ApplicationBuilderExtensions.cs` (start service)
- Test: `tests/MailPeek.Tests/Middleware/DashboardApiMiddlewareTests.cs`

**Step 1: Write the failing API tests**

Add to `DashboardApiMiddlewareTests.cs`:

```csharp
[Fact]
public async Task GetCompatibility_Returns202WhenChecking()
{
    var msg = new StoredMessage { From = "a@b.com", To = ["c@d.com"], Subject = "Test", HtmlBody = "<p>Hi</p>" };
    _store.Add(msg);
    var response = await _client!.GetAsync($"/mailpeek/api/messages/{msg.Id}/compatibility");
    Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
}

[Fact]
public async Task GetCompatibility_Returns200WhenComplete()
{
    var msg = new StoredMessage
    {
        From = "a@b.com", To = ["c@d.com"], Subject = "Test",
        HtmlCompatibilityCheckComplete = true,
        HtmlCompatibilityResult = new HtmlCompatibilityResult { Score = 85 }
    };
    _store.Add(msg);
    var response = await _client!.GetAsync($"/mailpeek/api/messages/{msg.Id}/compatibility");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}

[Fact]
public async Task GetCompatibility_ReturnsNotFoundForMissing()
{
    var response = await _client!.GetAsync($"/mailpeek/api/messages/{Guid.NewGuid()}/compatibility");
    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MailPeek.Tests/ --filter "FullyQualifiedName~GetCompatibility" --verbosity quiet`
Expected: FAIL — route not found (404)

**Step 3: Add the API endpoint**

In `DashboardApiExtensions.cs`, add after the links endpoint (around line 138):

```csharp
endpoints.MapGet($"{api}/messages/{{id:guid}}/compatibility", (Guid id, IMessageStore store) =>
{
    var msg = store.GetById(id);
    if (msg is null) return Results.NotFound();
    if (!msg.HtmlCompatibilityCheckComplete)
        return Results.Json(new { status = "checking" }, JsonOptions, statusCode: 202);
    return Results.Json(msg.HtmlCompatibilityResult, JsonOptions);
});
```

**Step 4: Add SignalR notification**

In `MailPeekHubNotifier.cs`, add:

```csharp
public Task NotifyHtmlCompatibilityCheckComplete(Guid id) =>
    hubContext.Clients.All.SendAsync("HtmlCompatibilityCheckComplete", id);
```

**Step 5: Register and start the service**

In `ServiceCollectionExtensions.cs`, add after `services.AddSingleton<LinkChecker>();`:

```csharp
services.AddSingleton<HtmlCompatibilityChecker>();
```

(Add to both `AddMailPeek` overloads.)

In `ApplicationBuilderExtensions.cs`, add after `linkChecker.Start();`:

```csharp
var htmlCompatChecker = app.ApplicationServices.GetRequiredService<HtmlCompatibilityChecker>();
htmlCompatChecker.Start();
```

Add `using MailPeek.Services;` if not already present.

**Step 6: Run tests to verify they pass**

Run: `dotnet test tests/MailPeek.Tests/ --verbosity quiet`
Expected: All tests PASS

**Step 7: Commit**

```bash
git add src/MailPeek/Hubs/MailPeekHubNotifier.cs src/MailPeek/Middleware/DashboardApiExtensions.cs src/MailPeek/Extensions/ServiceCollectionExtensions.cs src/MailPeek/Extensions/ApplicationBuilderExtensions.cs tests/MailPeek.Tests/Middleware/DashboardApiMiddlewareTests.cs
git commit -m "feat: wire HTML compatibility checker API, SignalR, and DI"
```

---

### Task 7: HTML Compatibility — Dashboard UI Tab

**Files:**
- Modify: `src/MailPeek/Assets/index.html` (add Compatibility tab)
- Modify: `src/MailPeek/Assets/js/dashboard.js` (fetch + render compat results)
- Modify: `src/MailPeek/Assets/css/dashboard.css` (compat score + issue styles)

**Step 1: Add tab to index.html**

In the detail tabs section, add after the Links tab:

```html
<button class="tab" data-tab="compatibility">Compatibility</button>
```

Add the tab panel after the links panel:

```html
<div id="compatibilityPanel" class="tab-panel">
    <div id="compatibilityContent"></div>
</div>
```

**Step 2: Add CSS for compatibility display**

```css
.compat-score {
    display: inline-block;
    font-size: 1.5rem;
    font-weight: 700;
    padding: 8px 16px;
    border-radius: 8px;
    margin-bottom: 16px;
}
.compat-score.score-good { background: #22c55e22; color: #22c55e; }
.compat-score.score-warn { background: #eab30822; color: #eab308; }
.compat-score.score-bad { background: #ef444422; color: #ef4444; }

.compat-issue {
    padding: 8px 12px;
    margin-bottom: 8px;
    border-left: 3px solid var(--border);
    border-radius: 4px;
    background: var(--bg);
}
.compat-issue.severity-critical { border-left-color: #ef4444; }
.compat-issue.severity-major { border-left-color: #eab308; }
.compat-issue.severity-minor { border-left-color: #6b7280; }
.compat-issue-title { font-weight: 600; font-size: 0.9rem; }
.compat-issue-clients { font-size: 0.8rem; color: var(--text-muted); margin-top: 4px; }
```

**Step 3: Add JS to fetch and render**

Add function to dashboard.js:

```javascript
async function loadCompatibility(id) {
    const container = document.getElementById('compatibilityContent');
    container.innerHTML = '<p class="text-muted">Checking compatibility...</p>';

    const resp = await fetch(`${basePath}/api/messages/${id}/compatibility`);
    if (resp.status === 202) {
        container.innerHTML = '<p class="text-muted">Compatibility check in progress...</p>';
        return;
    }
    const data = await resp.json();

    const scoreClass = data.score >= 80 ? 'score-good' : data.score >= 50 ? 'score-warn' : 'score-bad';
    let html = `<div class="compat-score ${scoreClass}">${data.score}/100</div>`;

    if (data.issues && data.issues.length > 0) {
        html += '<div class="compat-issues">';
        for (const issue of data.issues) {
            html += `<div class="compat-issue severity-${issue.severity.toLowerCase()}">
                <div class="compat-issue-title">${issue.description}</div>
                <div class="compat-issue-clients">Affected: ${issue.affectedClients.join(', ')}</div>
            </div>`;
        }
        html += '</div>';
    } else {
        html += '<p class="text-muted">No compatibility issues found.</p>';
    }

    container.innerHTML = html;
}
```

Call `loadCompatibility(id)` when the Compatibility tab is activated (in the tab click handler).

Add a SignalR handler:

```javascript
connection.on('HtmlCompatibilityCheckComplete', (id) => {
    if (currentMessageId === id) loadCompatibility(id);
});
```

**Step 4: Run all tests**

Run: `dotnet test tests/MailPeek.Tests/ --verbosity quiet`
Expected: All tests PASS

**Step 5: Commit**

```bash
git add src/MailPeek/Assets/index.html src/MailPeek/Assets/js/dashboard.js src/MailPeek/Assets/css/dashboard.css
git commit -m "feat: add Compatibility tab in message detail view"
```

---

### Task 8: Spam Score — Models

**Files:**
- Create: `src/MailPeek/Models/SpamCheckResult.cs`
- Create: `src/MailPeek/Models/SpamCheckRule.cs`
- Modify: `src/MailPeek/Models/StoredMessage.cs`

**Step 1: Create the models**

```csharp
// src/MailPeek/Models/SpamCheckRule.cs
namespace MailPeek.Models;

public class SpamCheckRule
{
    public required string Name { get; set; }
    public double Score { get; set; }
    public required string Description { get; set; }
}
```

```csharp
// src/MailPeek/Models/SpamCheckResult.cs
namespace MailPeek.Models;

public class SpamCheckResult
{
    public double Score { get; set; }
    public required string Source { get; set; }
#pragma warning disable MA0016
    public List<SpamCheckRule> Rules { get; set; } = [];
#pragma warning restore MA0016
}
```

**Step 2: Add properties to StoredMessage**

After the HTML compatibility properties:

```csharp
public SpamCheckResult? SpamCheckResult { get; set; }
public bool SpamCheckComplete { get; set; }
```

**Step 3: Run all tests**

Run: `dotnet test tests/MailPeek.Tests/ --verbosity quiet`
Expected: All tests PASS

**Step 4: Commit**

```bash
git add src/MailPeek/Models/SpamCheckResult.cs src/MailPeek/Models/SpamCheckRule.cs src/MailPeek/Models/StoredMessage.cs
git commit -m "feat: add spam check result models"
```

---

### Task 9: Spam Score — Built-in Heuristic Scorer + Tests

**Files:**
- Create: `src/MailPeek/Services/SpamScorer.cs`
- Create: `tests/MailPeek.Tests/Services/SpamScorerTests.cs`

**Step 1: Write the failing tests**

```csharp
// tests/MailPeek.Tests/Services/SpamScorerTests.cs
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
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MailPeek.Tests/ --filter "FullyQualifiedName~SpamScorerTests" --verbosity quiet`
Expected: FAIL — `SpamScorer` class does not exist

**Step 3: Write the implementation**

```csharp
// src/MailPeek/Services/SpamScorer.cs
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

        // Excessive punctuation in subject (2)
        if (!string.IsNullOrWhiteSpace(message.Subject) && ExcessivePunctuationRegex().IsMatch(message.Subject))
            rules.Add(new SpamCheckRule { Name = "EXCESSIVE_PUNCTUATION", Score = 2, Description = "Subject contains excessive punctuation (!!!, ???, etc.)" });

        // Missing Message-ID (2)
        if (!message.Headers.ContainsKey("Message-ID") && !message.Headers.ContainsKey("Message-Id"))
            rules.Add(new SpamCheckRule { Name = "MISSING_MESSAGE_ID", Score = 2, Description = "Missing Message-ID header" });

        // Missing Date header (2)
        if (!message.Headers.ContainsKey("Date"))
            rules.Add(new SpamCheckRule { Name = "MISSING_DATE", Score = 2, Description = "Missing Date header" });

        // Missing From display name (1)
        if (!string.IsNullOrEmpty(message.From) && !message.From.Contains('<', StringComparison.Ordinal))
            rules.Add(new SpamCheckRule { Name = "MISSING_FROM_NAME", Score = 1, Description = "From address has no display name" });

        // HTML only, no text part (2)
        if (string.IsNullOrWhiteSpace(message.TextBody) && !string.IsNullOrWhiteSpace(message.HtmlBody))
            rules.Add(new SpamCheckRule { Name = "HTML_ONLY", Score = 2, Description = "Message has HTML body but no plain text alternative" });

        // Suspicious phrases in body (2)
        var bodyText = message.TextBody ?? message.HtmlBody ?? "";
        if (SuspiciousPhrasesRegex().IsMatch(bodyText))
            rules.Add(new SpamCheckRule { Name = "SUSPICIOUS_PHRASES", Score = 2, Description = "Body contains suspicious phrases (act now, limited time, click here, etc.)" });

        // URL shorteners (2)
        if (UrlShortenerRegex().IsMatch(bodyText))
            rules.Add(new SpamCheckRule { Name = "URL_SHORTENER", Score = 2, Description = "Body contains URL shortener links" });

        // Excessive links (1)
        if (!string.IsNullOrEmpty(message.HtmlBody))
        {
            var linkCount = LinkCountRegex().Matches(message.HtmlBody).Count;
            if (linkCount > 10)
                rules.Add(new SpamCheckRule { Name = "EXCESSIVE_LINKS", Score = 1, Description = $"Body contains {linkCount} links (>10)" });
        }

        var score = 0.0;
        foreach (var rule in rules)
            score += rule.Score;

        return new SpamCheckResult { Score = score, Source = "builtin", Rules = rules };
    }

    [GeneratedRegex(@"[a-zA-Z]", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LetterRegex();

    [GeneratedRegex(@"[!?]{3,}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ExcessivePunctuationRegex();

    [GeneratedRegex(@"\b(act now|limited time|click here|buy now|free|winner|congratulations|urgent)\b", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SuspiciousPhrasesRegex();

    [GeneratedRegex(@"https?://(bit\.ly|tinyurl\.com|t\.co|goo\.gl|ow\.ly|is\.gd|buff\.ly)/", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UrlShortenerRegex();

    [GeneratedRegex(@"<a\s", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LinkCountRegex();
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/MailPeek.Tests/ --filter "FullyQualifiedName~SpamScorerTests" --verbosity quiet`
Expected: PASS (10 tests)

**Step 5: Commit**

```bash
git add src/MailPeek/Services/SpamScorer.cs tests/MailPeek.Tests/Services/SpamScorerTests.cs
git commit -m "feat: add built-in spam heuristic scorer with 10 rules"
```

---

### Task 10: Spam Score — Optional SpamAssassin Client + Tests

**Files:**
- Create: `src/MailPeek/Services/SpamAssassinClient.cs`
- Create: `tests/MailPeek.Tests/Services/SpamAssassinClientTests.cs`
- Modify: `src/MailPeek/Configuration/MailPeekSmtpOptions.cs` (add config)

**Step 1: Add configuration**

In `MailPeekSmtpOptions.cs`, add:

```csharp
public SpamAssassinOptions? SpamAssassin { get; set; }
```

Create new config class:

```csharp
// src/MailPeek/Configuration/SpamAssassinOptions.cs
namespace MailPeek.Configuration;

public class SpamAssassinOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 783;
}
```

**Step 2: Write the failing tests**

```csharp
// tests/MailPeek.Tests/Services/SpamAssassinClientTests.cs
using MailPeek.Services;

namespace MailPeek.Tests.Services;

public class SpamAssassinClientTests
{
    [Fact]
    public void ParseResponse_ParsesScoreAndRules()
    {
        var response = "SPAMD/1.1 0 EX_OK\r\nSpam: True ; 8.5 / 5.0\r\n\r\n";
        var result = SpamAssassinClient.ParseCheckResponse(response);
        Assert.NotNull(result);
        Assert.Equal(8.5, result!.Score);
        Assert.Equal("spamassassin", result.Source);
    }

    [Fact]
    public void ParseResponse_HandlesCleanMessage()
    {
        var response = "SPAMD/1.1 0 EX_OK\r\nSpam: False ; 1.2 / 5.0\r\n\r\n";
        var result = SpamAssassinClient.ParseCheckResponse(response);
        Assert.NotNull(result);
        Assert.Equal(1.2, result!.Score);
    }

    [Fact]
    public void ParseResponse_ReturnsNullForInvalidResponse()
    {
        var result = SpamAssassinClient.ParseCheckResponse("garbage");
        Assert.Null(result);
    }

    [Fact]
    public void BuildCheckCommand_FormatsCorrectly()
    {
        var rawMessage = System.Text.Encoding.UTF8.GetBytes("Subject: Test\r\n\r\nBody");
        var command = SpamAssassinClient.BuildCheckCommand(rawMessage);
        Assert.StartsWith("CHECK SPAMC/1.5\r\n", command, StringComparison.Ordinal);
        Assert.Contains("Content-length:", command, StringComparison.Ordinal);
    }
}
```

**Step 3: Run tests to verify they fail**

Run: `dotnet test tests/MailPeek.Tests/ --filter "FullyQualifiedName~SpamAssassinClientTests" --verbosity quiet`
Expected: FAIL — class does not exist

**Step 4: Write the implementation**

```csharp
// src/MailPeek/Services/SpamAssassinClient.cs
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using MailPeek.Configuration;
using MailPeek.Models;
using Microsoft.Extensions.Logging;

namespace MailPeek.Services;

public static partial class SpamAssassinClient
{
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(5);

    public static async Task<SpamCheckResult?> CheckAsync(
        byte[] rawMessage,
        SpamAssassinOptions options,
        ILogger logger)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(options.Host, options.Port);
            if (await Task.WhenAny(connectTask, Task.Delay(ConnectionTimeout)).ConfigureAwait(false) != connectTask)
            {
                logger.LogWarning("SpamAssassin connection timed out ({Host}:{Port})", options.Host, options.Port);
                return null;
            }

            await connectTask.ConfigureAwait(false); // propagate any exception

            await using var stream = client.GetStream();
            var command = BuildCheckCommand(rawMessage);
            var commandBytes = Encoding.UTF8.GetBytes(command);

            await stream.WriteAsync(commandBytes).ConfigureAwait(false);
            await stream.WriteAsync(rawMessage).ConfigureAwait(false);
            client.Client.Shutdown(SocketShutdown.Send);

            using var reader = new StreamReader(stream, Encoding.UTF8);
            var response = await reader.ReadToEndAsync().ConfigureAwait(false);
            return ParseCheckResponse(response);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogWarning(ex, "SpamAssassin check failed ({Host}:{Port})", options.Host, options.Port);
            return null;
        }
    }

    public static string BuildCheckCommand(byte[] rawMessage)
    {
        var sb = new StringBuilder();
        sb.Append("CHECK SPAMC/1.5\r\n");
        sb.Append(CultureInfo.InvariantCulture, $"Content-length: {rawMessage.Length}\r\n");
        sb.Append("\r\n");
        return sb.ToString();
    }

    public static SpamCheckResult? ParseCheckResponse(string response)
    {
        var match = SpamScoreRegex().Match(response);
        if (!match.Success) return null;

        if (!double.TryParse(match.Groups["score"].Value, CultureInfo.InvariantCulture, out var score))
            return null;

        return new SpamCheckResult
        {
            Score = score,
            Source = "spamassassin"
        };
    }

    [GeneratedRegex(@"Spam:\s*(True|False)\s*;\s*(?<score>[\d.]+)\s*/\s*[\d.]+", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SpamScoreRegex();
}
```

**Step 5: Run tests to verify they pass**

Run: `dotnet test tests/MailPeek.Tests/ --filter "FullyQualifiedName~SpamAssassinClientTests" --verbosity quiet`
Expected: PASS (4 tests)

**Step 6: Commit**

```bash
git add src/MailPeek/Services/SpamAssassinClient.cs src/MailPeek/Configuration/SpamAssassinOptions.cs src/MailPeek/Configuration/MailPeekSmtpOptions.cs tests/MailPeek.Tests/Services/SpamAssassinClientTests.cs
git commit -m "feat: add SpamAssassin spamd client with TCP protocol support"
```

---

### Task 11: Spam Score — API + SignalR + DI Wiring

**Files:**
- Modify: `src/MailPeek/Hubs/MailPeekHubNotifier.cs` (add NotifySpamCheckComplete)
- Modify: `src/MailPeek/Middleware/DashboardApiExtensions.cs` (add GET spam endpoint)
- Modify: `src/MailPeek/Extensions/ServiceCollectionExtensions.cs` (register SpamScorer)
- Modify: `src/MailPeek/Extensions/ApplicationBuilderExtensions.cs` (start SpamScorer)
- Modify: `src/MailPeek/Services/SpamScorer.cs` (integrate SpamAssassin fallback)
- Test: `tests/MailPeek.Tests/Middleware/DashboardApiMiddlewareTests.cs`

**Step 1: Write the failing API tests**

Add to `DashboardApiMiddlewareTests.cs`:

```csharp
[Fact]
public async Task GetSpam_Returns202WhenChecking()
{
    var msg = new StoredMessage { From = "a@b.com", To = ["c@d.com"], Subject = "Test" };
    _store.Add(msg);
    var response = await _client!.GetAsync($"/mailpeek/api/messages/{msg.Id}/spam");
    Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
}

[Fact]
public async Task GetSpam_Returns200WhenComplete()
{
    var msg = new StoredMessage
    {
        From = "a@b.com", To = ["c@d.com"], Subject = "Test",
        SpamCheckComplete = true,
        SpamCheckResult = new SpamCheckResult { Score = 2.5, Source = "builtin" }
    };
    _store.Add(msg);
    var response = await _client!.GetAsync($"/mailpeek/api/messages/{msg.Id}/spam");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}

[Fact]
public async Task GetSpam_ReturnsNotFoundForMissing()
{
    var response = await _client!.GetAsync($"/mailpeek/api/messages/{Guid.NewGuid()}/spam");
    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MailPeek.Tests/ --filter "FullyQualifiedName~GetSpam" --verbosity quiet`
Expected: FAIL — route not found

**Step 3: Add the API endpoint**

In `DashboardApiExtensions.cs`, add after the compatibility endpoint:

```csharp
endpoints.MapGet($"{api}/messages/{{id:guid}}/spam", (Guid id, IMessageStore store) =>
{
    var msg = store.GetById(id);
    if (msg is null) return Results.NotFound();
    if (!msg.SpamCheckComplete)
        return Results.Json(new { status = "checking" }, JsonOptions, statusCode: 202);
    return Results.Json(msg.SpamCheckResult, JsonOptions);
});
```

**Step 4: Add SignalR notification**

In `MailPeekHubNotifier.cs`:

```csharp
public Task NotifySpamCheckComplete(Guid id) =>
    hubContext.Clients.All.SendAsync("SpamCheckComplete", id);
```

**Step 5: Update SpamScorer to try SpamAssassin first**

Modify `SpamScorer` constructor to also accept `IOptions<MailPeekSmtpOptions>` and `ILogger`:

```csharp
public partial class SpamScorer(
    IMessageStore store,
    MailPeekHubNotifier hubNotifier,
    IOptions<MailPeekSmtpOptions> options,
    ILogger<SpamScorer> logger)
```

Add `using Microsoft.Extensions.Options;` and `using MailPeek.Configuration;`.

Update `RunCheck` to try SpamAssassin first if configured:

```csharp
private async Task RunCheck(StoredMessage message)
{
    try
    {
        var config = options.Value;
        SpamCheckResult? result = null;

        if (config.SpamAssassin is { Enabled: true } saOptions && message.RawMessage is not null)
        {
            result = await SpamAssassinClient.CheckAsync(message.RawMessage, saOptions, logger).ConfigureAwait(false);
        }

        result ??= Analyze(message);

        message.SpamCheckResult = result;
        message.SpamCheckComplete = true;
        await hubNotifier.NotifySpamCheckComplete(message.Id).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Spam check failed for message {MessageId}", message.Id);
        message.SpamCheckResult = new SpamCheckResult { Score = -1, Source = "builtin" };
        message.SpamCheckComplete = true;
    }
}
```

**Step 6: Register and start the service**

In `ServiceCollectionExtensions.cs`, add after `HtmlCompatibilityChecker` registration:

```csharp
services.AddSingleton<SpamScorer>();
```

(Add to both `AddMailPeek` overloads.)

Update the options configuration in the first overload to include SpamAssassin:

```csharp
opts.SpamAssassin = options.SpamAssassin;
```

In `ApplicationBuilderExtensions.cs`, add after `htmlCompatChecker.Start();`:

```csharp
var spamScorer = app.ApplicationServices.GetRequiredService<SpamScorer>();
spamScorer.Start();
```

**Step 7: Run all tests**

Run: `dotnet test tests/MailPeek.Tests/ --verbosity quiet`
Expected: All tests PASS

**Step 8: Commit**

```bash
git add src/MailPeek/Hubs/MailPeekHubNotifier.cs src/MailPeek/Middleware/DashboardApiExtensions.cs src/MailPeek/Extensions/ServiceCollectionExtensions.cs src/MailPeek/Extensions/ApplicationBuilderExtensions.cs src/MailPeek/Services/SpamScorer.cs tests/MailPeek.Tests/Middleware/DashboardApiMiddlewareTests.cs
git commit -m "feat: wire spam scorer API, SignalR, DI, and SpamAssassin fallback"
```

---

### Task 12: Spam Score — Dashboard UI Tab

**Files:**
- Modify: `src/MailPeek/Assets/index.html` (add Spam tab)
- Modify: `src/MailPeek/Assets/js/dashboard.js` (fetch + render spam results)
- Modify: `src/MailPeek/Assets/css/dashboard.css` (spam score styles)

**Step 1: Add tab to index.html**

After the Compatibility tab button:

```html
<button class="tab" data-tab="spam">Spam</button>
```

After the compatibility panel:

```html
<div id="spamPanel" class="tab-panel">
    <div id="spamContent"></div>
</div>
```

**Step 2: Add CSS**

```css
.spam-score {
    display: inline-block;
    font-size: 1.5rem;
    font-weight: 700;
    padding: 8px 16px;
    border-radius: 8px;
    margin-bottom: 8px;
}
.spam-score.risk-low { background: #22c55e22; color: #22c55e; }
.spam-score.risk-medium { background: #eab30822; color: #eab308; }
.spam-score.risk-high { background: #ef444422; color: #ef4444; }
.spam-source { font-size: 0.8rem; color: var(--text-muted); margin-bottom: 16px; }

.spam-rule {
    padding: 8px 12px;
    margin-bottom: 8px;
    background: var(--bg);
    border-radius: 4px;
    display: flex;
    justify-content: space-between;
    align-items: center;
}
.spam-rule-name { font-weight: 600; font-size: 0.85rem; font-family: monospace; }
.spam-rule-desc { font-size: 0.85rem; color: var(--text-muted); }
.spam-rule-score { font-weight: 700; color: #ef4444; white-space: nowrap; }
```

**Step 3: Add JS to fetch and render**

```javascript
async function loadSpam(id) {
    const container = document.getElementById('spamContent');
    container.innerHTML = '<p class="text-muted">Checking spam score...</p>';

    const resp = await fetch(`${basePath}/api/messages/${id}/spam`);
    if (resp.status === 202) {
        container.innerHTML = '<p class="text-muted">Spam analysis in progress...</p>';
        return;
    }
    const data = await resp.json();

    const riskClass = data.score <= 5 ? 'risk-low' : data.score <= 12 ? 'risk-medium' : 'risk-high';
    const riskLabel = data.score <= 5 ? 'Low Risk' : data.score <= 12 ? 'Medium Risk' : 'High Risk';
    let html = `<div class="spam-score ${riskClass}">${data.score.toFixed(1)} — ${riskLabel}</div>`;
    html += `<div class="spam-source">Source: ${data.source}</div>`;

    if (data.rules && data.rules.length > 0) {
        for (const rule of data.rules) {
            html += `<div class="spam-rule">
                <div><span class="spam-rule-name">${rule.name}</span><br><span class="spam-rule-desc">${rule.description}</span></div>
                <div class="spam-rule-score">+${rule.score.toFixed(1)}</div>
            </div>`;
        }
    } else {
        html += '<p class="text-muted">No spam indicators found.</p>';
    }

    container.innerHTML = html;
}
```

Call `loadSpam(id)` when the Spam tab is activated (in the tab click handler).

Add SignalR handler:

```javascript
connection.on('SpamCheckComplete', (id) => {
    if (currentMessageId === id) loadSpam(id);
});
```

**Step 4: Run all tests**

Run: `dotnet test tests/MailPeek.Tests/ --verbosity quiet`
Expected: All tests PASS

**Step 5: Commit**

```bash
git add src/MailPeek/Assets/index.html src/MailPeek/Assets/js/dashboard.js src/MailPeek/Assets/css/dashboard.css
git commit -m "feat: add Spam tab in message detail view"
```

---

### Task 13: Update Documentation + Final Verification

**Files:**
- Modify: `docs/FEATURES.md` (mark 4 items done)
- Modify: `README.md` (add new API endpoints, update features)

**Step 1: Update FEATURES.md**

Change these lines from `[ ]` to `[x]`:
- `**Message preview snippet**`
- `**Keyboard shortcuts**`
- `**HTML compatibility scoring**`
- `**SpamAssassin integration**`

**Step 2: Update README.md**

Add to the REST API table:
```
| `GET` | `/mailpeek/api/messages/{id}/compatibility` | HTML compatibility score and issues |
| `GET` | `/mailpeek/api/messages/{id}/spam` | Spam score analysis |
```

**Step 3: Run full test suite**

Run: `dotnet test tests/MailPeek.Tests/ --verbosity quiet`
Expected: All tests PASS, 0 warnings

**Step 4: Commit**

```bash
git add docs/FEATURES.md README.md
git commit -m "docs: mark P2 features complete, update README API table"
```
