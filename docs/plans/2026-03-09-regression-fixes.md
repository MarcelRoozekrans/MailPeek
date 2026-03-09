# Regression Report Fixes Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Address all four findings from the 2026-03-09 regression test report: missing favicon, message count badge, per-row delete buttons, and styled empty text body placeholder.

**Architecture:** All changes are in the embedded dashboard frontend (HTML, CSS, JS) and the static files middleware (C#). No new NuGet dependencies. The favicon is an inline SVG data URI in the HTML `<head>`. The message count badge, delete buttons, and text placeholder are JS rendering changes with supporting CSS.

**Tech Stack:** Vanilla JS, CSS, ASP.NET Core embedded resources, xUnit

---

### Task 1: Add Inline SVG Favicon

**Context:** The browser requests `/favicon.ico` on every page load, producing a 404 console error. Since all assets are embedded in the DLL, the simplest fix is an inline SVG favicon as a `<link>` tag in `<head>` — no new files, no middleware changes.

**Files:**
- Modify: `src/MailPeek/Assets/index.html:5` (add favicon link after viewport meta)

**Step 1: Write the failing test**

Create a test that verifies the HTML served by the dashboard contains a favicon link.

File: `tests/MailPeek.Tests/Middleware/DashboardStaticFilesMiddlewareTests.cs` (create)

```csharp
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MailPeek.Extensions;
using Xunit;

namespace MailPeek.Tests.Middleware;

public class DashboardStaticFilesMiddlewareTests
{
    [Fact]
    public async Task Index_html_contains_favicon_link()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services => services.AddMailPeek());
                webBuilder.Configure(app => app.UseMailPeek());
            })
            .StartAsync();

        var client = host.GetTestServer().CreateClient();
        var response = await client.GetAsync("/mailpeek");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("rel=\"icon\"", html);
    }
}
```

**Step 2: Run the test to verify it fails**

Run: `dotnet test tests/MailPeek.Tests --filter "DashboardStaticFilesMiddlewareTests.Index_html_contains_favicon_link" --verbosity normal`

Expected: FAIL — the current `index.html` has no `rel="icon"` link.

**Step 3: Add the inline SVG favicon**

Edit `src/MailPeek/Assets/index.html`. After line 5 (`<title>{{TITLE}}</title>`), before line 6 (`<link rel="stylesheet"`), add:

```html
    <link rel="icon" type="image/svg+xml" href="data:image/svg+xml,<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><text y='.9em' font-size='90'>📧</text></svg>" />
```

The full `<head>` should now be:

```html
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>{{TITLE}}</title>
    <link rel="icon" type="image/svg+xml" href="data:image/svg+xml,<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><text y='.9em' font-size='90'>📧</text></svg>" />
    <link rel="stylesheet" href="{{PATH_PREFIX}}/assets/css/dashboard.css" />
</head>
```

**Step 4: Run the test to verify it passes**

Run: `dotnet test tests/MailPeek.Tests --filter "DashboardStaticFilesMiddlewareTests.Index_html_contains_favicon_link" --verbosity normal`

Expected: PASS

**Step 5: Commit**

```bash
git add src/MailPeek/Assets/index.html tests/MailPeek.Tests/Middleware/DashboardStaticFilesMiddlewareTests.cs
git commit -m "fix: add inline SVG favicon to prevent 404 console error"
```

---

### Task 2: Add Message Count Badge in Header

**Context:** The regression report suggests adding a message count indicator in the header (e.g., "3 messages") for quick visibility. The count comes from the `totalCount` field already returned by `GET /api/messages`. The badge sits next to the `<h1>` title in the header.

**Files:**
- Modify: `src/MailPeek/Assets/index.html:11` (add badge span after h1)
- Modify: `src/MailPeek/Assets/css/dashboard.css` (add badge styles)
- Modify: `src/MailPeek/Assets/js/dashboard.js` (update badge on load)

**Step 1: Write the failing test**

Add a test to the file created in Task 1 that verifies the HTML contains a message-count badge element.

File: `tests/MailPeek.Tests/Middleware/DashboardStaticFilesMiddlewareTests.cs` (modify — add test)

```csharp
[Fact]
public async Task Index_html_contains_message_count_badge()
{
    using var host = await new HostBuilder()
        .ConfigureWebHost(webBuilder =>
        {
            webBuilder.UseTestServer();
            webBuilder.ConfigureServices(services => services.AddMailPeek());
            webBuilder.Configure(app => app.UseMailPeek());
        })
        .StartAsync();

    var client = host.GetTestServer().CreateClient();
    var response = await client.GetAsync("/mailpeek");

    var html = await response.Content.ReadAsStringAsync();
    Assert.Contains("id=\"messageCount\"", html);
}
```

**Step 2: Run the test to verify it fails**

Run: `dotnet test tests/MailPeek.Tests --filter "DashboardStaticFilesMiddlewareTests.Index_html_contains_message_count_badge" --verbosity normal`

Expected: FAIL — no element with id `messageCount` exists in the HTML.

**Step 3: Add the badge HTML element**

Edit `src/MailPeek/Assets/index.html`. Change the header from:

```html
    <header>
        <h1>{{TITLE}}</h1>
```

To:

```html
    <header>
        <div class="header-title">
            <h1>{{TITLE}}</h1>
            <span id="messageCount" class="badge"></span>
        </div>
```

**Step 4: Add badge CSS**

Edit `src/MailPeek/Assets/css/dashboard.css`. After the `header h1` block (around line 76), add:

```css
.header-title {
    display: flex;
    align-items: center;
    gap: 10px;
}

.badge {
    display: none;
    padding: 2px 10px;
    border-radius: 12px;
    background: var(--primary);
    color: #fff;
    font-size: 0.75rem;
    font-weight: 600;
    line-height: 1.4;
}

.badge.visible {
    display: inline-block;
}
```

**Step 5: Update JS to populate the badge**

Edit `src/MailPeek/Assets/js/dashboard.js`. In the `renderInbox` function, after the line `var totalCount = data.totalCount || 0;` (around line 95), add:

```javascript
        // Update message count badge
        var badge = document.getElementById('messageCount');
        if (totalCount > 0) {
            badge.textContent = totalCount + (totalCount === 1 ? ' message' : ' messages');
            badge.classList.add('visible');
        } else {
            badge.classList.remove('visible');
        }
```

**Step 6: Run the test to verify it passes**

Run: `dotnet test tests/MailPeek.Tests --filter "DashboardStaticFilesMiddlewareTests.Index_html_contains_message_count_badge" --verbosity normal`

Expected: PASS

**Step 7: Commit**

```bash
git add src/MailPeek/Assets/index.html src/MailPeek/Assets/css/dashboard.css src/MailPeek/Assets/js/dashboard.js tests/MailPeek.Tests/Middleware/DashboardStaticFilesMiddlewareTests.cs
git commit -m "feat: add message count badge in dashboard header"
```

---

### Task 3: Add Per-Row Delete Buttons in Inbox Table

**Context:** The inbox table has an empty 5th column (currently showing the attachment paperclip icon). The regression report suggests adding per-row delete buttons here. This lets users delete messages directly from the inbox without opening the detail view. The delete API endpoint `DELETE /api/messages/{id}` already exists.

**Files:**
- Modify: `src/MailPeek/Assets/index.html:26` (add column header text)
- Modify: `src/MailPeek/Assets/js/dashboard.js` (add delete button to each row)
- Modify: `src/MailPeek/Assets/css/dashboard.css` (add delete button styles)

**Step 1: Write the failing test**

Add a test to `DashboardApiMiddlewareTests.cs` that verifies a message can be deleted and the inbox refreshes. We already have a `Delete_message_returns_ok` test — but we need a test that the inbox HTML structure contains the delete column header.

File: `tests/MailPeek.Tests/Middleware/DashboardStaticFilesMiddlewareTests.cs` (modify — add test)

```csharp
[Fact]
public async Task Index_html_contains_delete_column_header()
{
    using var host = await new HostBuilder()
        .ConfigureWebHost(webBuilder =>
        {
            webBuilder.UseTestServer();
            webBuilder.ConfigureServices(services => services.AddMailPeek());
            webBuilder.Configure(app => app.UseMailPeek());
        })
        .StartAsync();

    var client = host.GetTestServer().CreateClient();
    var response = await client.GetAsync("/mailpeek");

    var html = await response.Content.ReadAsStringAsync();
    // The 5th column header should no longer be empty — it should indicate actions
    Assert.DoesNotContain("<th></th>", html);
}
```

**Step 2: Run the test to verify it fails**

Run: `dotnet test tests/MailPeek.Tests --filter "DashboardStaticFilesMiddlewareTests.Index_html_contains_delete_column_header" --verbosity normal`

Expected: FAIL — the current HTML has `<th></th>` (empty 5th column header).

**Step 3: Update the column header**

Edit `src/MailPeek/Assets/index.html`. Change line 26 from:

```html
                        <th></th>
```

To:

```html
                        <th class="col-actions"></th>
```

Wait — the test checks for `<th></th>` not existing. Let's use a visually hidden label instead:

```html
                        <th><span class="sr-only">Actions</span></th>
```

**Step 4: Add screen-reader-only CSS class**

Edit `src/MailPeek/Assets/css/dashboard.css`. Add near the `.hidden` utility (around line 249):

```css
.sr-only {
    position: absolute;
    width: 1px;
    height: 1px;
    padding: 0;
    margin: -1px;
    overflow: hidden;
    clip: rect(0, 0, 0, 0);
    border: 0;
}
```

Add delete button styles after the `.btn-sm` block (around line 141):

```css
.btn-delete-row {
    padding: 3px 8px;
    border: 1px solid transparent;
    border-radius: var(--radius);
    background: none;
    color: var(--text-muted);
    font-size: 0.85rem;
    cursor: pointer;
    transition: color 0.15s, background 0.15s;
    line-height: 1;
}

.btn-delete-row:hover {
    color: var(--danger);
    background: rgba(239, 68, 68, 0.1);
}
```

**Step 5: Update JS to render delete buttons in each row**

Edit `src/MailPeek/Assets/js/dashboard.js`. In the `renderInbox` function, replace the `tdAttach` column construction (around lines 131-132):

```javascript
            var tdAttach = document.createElement('td');
            tdAttach.textContent = msg.hasAttachments ? '\uD83D\uDCCE' : '';
```

With a cell that contains both the attachment icon and a delete button:

```javascript
            var tdActions = document.createElement('td');
            tdActions.setAttribute('data-label', 'Actions');
            if (msg.hasAttachments) {
                var attachSpan = document.createElement('span');
                attachSpan.textContent = '\uD83D\uDCCE ';
                attachSpan.title = 'Has attachments';
                tdActions.appendChild(attachSpan);
            }
            var delBtn = document.createElement('button');
            delBtn.className = 'btn-delete-row';
            delBtn.title = 'Delete';
            delBtn.textContent = '\u2715';
            delBtn.addEventListener('click', function (e) {
                e.stopPropagation();
                deleteMessage(msg.id);
            });
            tdActions.appendChild(delBtn);
```

Then update the `tr.appendChild(tdAttach);` line to `tr.appendChild(tdActions);`.

**Step 6: Run the test to verify it passes**

Run: `dotnet test tests/MailPeek.Tests --filter "DashboardStaticFilesMiddlewareTests.Index_html_contains_delete_column_header" --verbosity normal`

Expected: PASS

**Step 7: Run all tests**

Run: `dotnet test tests/MailPeek.Tests --verbosity normal`

Expected: All tests pass (28 existing + 3 new = 31 total).

**Step 8: Commit**

```bash
git add src/MailPeek/Assets/index.html src/MailPeek/Assets/css/dashboard.css src/MailPeek/Assets/js/dashboard.js tests/MailPeek.Tests/Middleware/DashboardStaticFilesMiddlewareTests.cs
git commit -m "feat: add per-row delete buttons in inbox table"
```

---

### Task 4: Style Empty Text Body Placeholder

**Context:** When a message has no text body, the Text tab shows `(no text body)` in plain monospace text. The regression report suggests styling this as a muted/italic placeholder for better visual hierarchy. This is a CSS-only change (the JS already renders the placeholder text).

**Files:**
- Modify: `src/MailPeek/Assets/js/dashboard.js:211` (add CSS class when no text body)
- Modify: `src/MailPeek/Assets/css/dashboard.css` (add placeholder style)

**Step 1: Write the failing test**

File: `tests/MailPeek.Tests/Middleware/DashboardStaticFilesMiddlewareTests.cs` (modify — add test)

```csharp
[Fact]
public async Task Index_html_contains_empty_placeholder_css_class()
{
    using var host = await new HostBuilder()
        .ConfigureWebHost(webBuilder =>
        {
            webBuilder.UseTestServer();
            webBuilder.ConfigureServices(services => services.AddMailPeek());
            webBuilder.Configure(app => app.UseMailPeek());
        })
        .StartAsync();

    var client = host.GetTestServer().CreateClient();
    var response = await client.GetAsync("/mailpeek/assets/css/dashboard.css");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var css = await response.Content.ReadAsStringAsync();
    Assert.Contains(".empty-placeholder", css);
}
```

**Step 2: Run the test to verify it fails**

Run: `dotnet test tests/MailPeek.Tests --filter "DashboardStaticFilesMiddlewareTests.Index_html_contains_empty_placeholder_css_class" --verbosity normal`

Expected: FAIL — no `.empty-placeholder` class in the CSS.

**Step 3: Add the CSS class**

Edit `src/MailPeek/Assets/css/dashboard.css`. After the `#textPreview` block (around line 349), add:

```css
.empty-placeholder {
    color: var(--text-muted);
    font-style: italic;
    font-family: var(--font);
    font-size: 0.9rem;
}
```

**Step 4: Update JS to apply the class**

Edit `src/MailPeek/Assets/js/dashboard.js`. Replace the text body rendering (around line 211):

```javascript
            // Text body
            var textPre = document.getElementById('textPreview');
            textPre.textContent = msg.textBody || '(no text body)';
```

With:

```javascript
            // Text body
            var textPre = document.getElementById('textPreview');
            if (msg.textBody) {
                textPre.textContent = msg.textBody;
                textPre.classList.remove('empty-placeholder');
            } else {
                textPre.textContent = 'No text body';
                textPre.classList.add('empty-placeholder');
            }
```

**Step 5: Run the test to verify it passes**

Run: `dotnet test tests/MailPeek.Tests --filter "DashboardStaticFilesMiddlewareTests.Index_html_contains_empty_placeholder_css_class" --verbosity normal`

Expected: PASS

**Step 6: Run all tests**

Run: `dotnet test tests/MailPeek.Tests --verbosity normal`

Expected: All tests pass (31 existing + 1 new = 32 total).

**Step 7: Commit**

```bash
git add src/MailPeek/Assets/css/dashboard.css src/MailPeek/Assets/js/dashboard.js tests/MailPeek.Tests/Middleware/DashboardStaticFilesMiddlewareTests.cs
git commit -m "feat: style empty text body placeholder with muted italic"
```

---

### Task 5: Run Full Test Suite and Verify Build

**Step 1: Build both targets**

Run: `dotnet build src/MailPeek/MailPeek.csproj --configuration Release`

Expected: Build succeeded for net8.0 and net9.0 with no warnings.

**Step 2: Run all tests**

Run: `dotnet test tests/MailPeek.Tests --verbosity normal`

Expected: 32 tests passed, 0 failed, 0 skipped.

**Step 3: Commit (if any fixes were needed)**

Only commit if adjustments were required in previous steps.
