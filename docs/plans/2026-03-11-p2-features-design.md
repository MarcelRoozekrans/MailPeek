# P2 Feature Batch Design

**Date:** 2026-03-11
**Features:** Message preview snippet, Keyboard shortcuts, HTML compatibility scoring, Spam score analysis

---

## 1. Message Preview Snippet

**Goal:** Show a short text preview below/beside the subject in the inbox row so users can scan message content without opening.

**Approach:** At ingest time (when `StoredMessage` is created from the MIME message), extract a plain-text snippet:
- Prefer `TextBody` if available
- Otherwise strip HTML tags from `HtmlBody` using a simple regex or the existing text extraction
- Truncate to 120 characters, append ellipsis if truncated
- Store as `Snippet` property on `StoredMessage`

**UI:** Display as a muted, smaller line below the subject text in the inbox table row. On mobile, the snippet may be hidden or shown below the subject in the card layout.

**API:** Include `snippet` in the message summary returned by `GET /api/messages`.

---

## 2. Keyboard Shortcuts

**Goal:** Enable power-user keyboard navigation of the inbox and message detail views.

**Shortcuts:**
| Key | Action |
|-----|--------|
| `j` | Move focus to next message row |
| `k` | Move focus to previous message row |
| `Enter` | Open focused message |
| `Delete` / `Backspace` | Delete focused message |
| `Esc` | Return to inbox from detail view |
| `?` | Show keyboard shortcut help overlay |

**Approach:** Pure JavaScript `keydown` listener on `document`. Maintain a `focusedIndex` state variable. Add a `.focused` CSS class to the highlighted row (distinct from selection). Shortcuts are only active when no input/textarea is focused (to avoid interfering with search).

**UI:** Focused row gets a subtle left border or background highlight (different from the checkbox selection highlight). A small `?` icon in the header area opens a shortcut cheat sheet modal.

---

## 3. HTML Compatibility Scoring

**Goal:** Analyze email HTML against common email client rendering rules and produce a compatibility score with actionable feedback.

**Approach:** Built-in rule engine that runs at ingest time (background, like link checking). Each rule checks for a known compatibility issue and reports severity + affected clients.

**Rules (~15-20):** Based on Can I Email data:
- CSS `display: flex` / `grid` (not supported in Outlook, many webmail)
- CSS `margin` on block elements (inconsistent in Outlook)
- `<div>` layout instead of `<table>` (Outlook uses Word renderer)
- Missing `width` attribute on `<table>` / `<img>` (Outlook ignores CSS width)
- CSS shorthand properties (partial support)
- `background-image` (not supported in Outlook)
- `<style>` in `<head>` vs inline styles (Gmail strips `<head>` styles)
- Media queries (limited support)
- `position: absolute/relative` (not supported in most email clients)
- `border-radius` (not supported in Outlook)
- Missing `alt` attribute on `<img>` (accessibility)
- `<video>` / `<audio>` tags (not supported)
- Custom fonts / `@font-face` (limited support)
- `max-width` / `min-width` (not supported in Outlook)
- Form elements `<input>`, `<button>` (not supported in most clients)

**Scoring:** Each rule has a weight (critical=10, major=5, minor=2). Score = 100 - sum of triggered weights, clamped to 0. Issues list with severity, description, and affected clients.

**Data model:**
```csharp
public class HtmlCompatibilityResult
{
    public int Score { get; set; }                    // 0-100
    public List<HtmlCompatibilityIssue> Issues { get; set; }
}

public class HtmlCompatibilityIssue
{
    public string RuleId { get; set; }                // e.g. "no-flex"
    public string Description { get; set; }
    public string Severity { get; set; }              // critical, major, minor
    public List<string> AffectedClients { get; set; } // e.g. ["Outlook", "Gmail"]
}
```

**Storage:** `HtmlCompatibilityResult` stored on `StoredMessage`, similar to `LinkCheckResults`.

**API:** `GET /api/messages/{id}/compatibility` returns the result (202 while checking, 200 when complete).

**UI:** New "Compatibility" tab in message detail view. Shows score as a colored badge (green 80-100, yellow 50-79, red 0-49) and a list of issues with severity icons.

---

## 4. Spam Score Analysis

**Goal:** Score emails for spam characteristics to help developers verify their emails won't be flagged.

### 4a. Built-in Heuristic Scorer

Runs at ingest time. Checks ~10-15 common spam signals:

| Check | Weight | Description |
|-------|--------|-------------|
| Missing `Message-ID` header | 2 | Standard header expected |
| Missing `Date` header | 2 | Standard header expected |
| Missing `From` display name | 1 | "user@example.com" vs "John <user@example.com>" |
| ALL CAPS subject | 3 | "FREE MONEY NOW" |
| Excessive punctuation in subject | 2 | "Buy now!!!" |
| Suspicious phrases | 2 | "act now", "limited time", "click here", "free" |
| HTML-only (no text part) | 2 | Multipart with text alternative is best practice |
| High image-to-text ratio | 2 | Image-heavy emails flagged as spam |
| Missing `List-Unsubscribe` header | 1 | Expected for bulk/marketing mail |
| URL shorteners in body | 2 | bit.ly, tinyurl, etc. |
| Mismatched From/Reply-To domains | 2 | Phishing indicator |
| Empty subject | 3 | Major spam signal |
| Excessive links | 1 | >10 links in body |

**Scoring:** Sum of triggered weights. Display as "Spam Score: X/25" with color coding (green 0-5, yellow 6-12, red 13+). Lower is better.

### 4b. Optional SpamAssassin Integration

**Config:**
```csharp
builder.Services.AddMailPeek(options =>
{
    options.SpamAssassin = new SpamAssassinOptions
    {
        Enabled = true,
        Host = "localhost",
        Port = 783
    };
});
```

**Protocol:** Connect to spamd via TCP, send `CHECK` command with raw message, parse response for score and matched rules. Falls back to built-in scorer if spamd is unreachable.

**Data model:**
```csharp
public class SpamCheckResult
{
    public double Score { get; set; }
    public List<SpamCheckRule> Rules { get; set; }
    public string Source { get; set; }  // "builtin" or "spamassassin"
}

public class SpamCheckRule
{
    public string Name { get; set; }
    public double Score { get; set; }
    public string Description { get; set; }
}
```

**Storage:** `SpamCheckResult` on `StoredMessage`.

**API:** `GET /api/messages/{id}/spam` returns the result.

**UI:** New "Spam" tab in message detail. Shows score, source indicator, and rule breakdown.

---

## Testing Strategy

- **Snippet:** Unit test extraction from HTML and plain text, truncation, empty body handling
- **Keyboard:** JS-level (manual testing via Playwright MCP for regression)
- **HTML compat:** Unit test each rule with minimal HTML snippets, test scoring math
- **Spam score:** Unit test each heuristic check, test SpamAssassin protocol parser with mock TCP, integration test the API endpoints
