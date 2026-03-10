# Feature Batch Implementation Plan: Read/Unread, Link Checking, Tagging, Webhooks

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add four features to MailPeek — read/unread status with browser notifications, link checking, message tagging, and webhook support.

**Architecture:** Each feature extends `StoredMessage` / `MessageSummary` models, adds methods to `IMessageStore` / `InMemoryMessageStore`, exposes new API endpoints in `DashboardApiExtensions`, and updates the embedded dashboard UI. Link checking and webhooks subscribe to `IMessageStore.OnMessageReceived` as background services. All new services register in `ServiceCollectionExtensions`.

**Tech Stack:** .NET 8/9, xUnit + NSubstitute, SignalR, embedded HTML/CSS/JS, IHttpClientFactory

---

## Task 1: Add `IsRead` to Models

**Files:**
- Modify: `src/MailPeek/Models/StoredMessage.cs`
- Modify: `src/MailPeek/Models/MessageSummary.cs`
- Modify: `tests/MailPeek.Tests/Models/StoredMessageTests.cs`

**Step 1: Write the failing test**

In `tests/MailPeek.Tests/Models/StoredMessageTests.cs`, add:

```csharp
[Fact]
public void IsRead_DefaultsFalse()
{
    var msg = new StoredMessage();
    Assert.False(msg.IsRead);
}

[Fact]
public void ToSummary_IncludesIsRead()
{
    var msg = new StoredMessage { IsRead = true };
    var summary = msg.ToSummary();
    Assert.True(summary.IsRead);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MailPeek.Tests --filter "IsRead_DefaultsFalse|ToSummary_IncludesIsRead" --no-restore -v n`
Expected: FAIL — `IsRead` property doesn't exist yet.

**Step 3: Write minimal implementation**

In `src/MailPeek/Models/StoredMessage.cs`, add after line 18 (`ParseErrorMessage`):
```csharp
public bool IsRead { get; set; }
```

In `src/MailPeek/Models/MessageSummary.cs`, add a new property:
```csharp
public required bool IsRead { get; set; }
```

In `StoredMessage.ToSummary()`, add `IsRead = IsRead` to the initializer.

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/MailPeek.Tests --filter "IsRead_DefaultsFalse|ToSummary_IncludesIsRead" --no-restore -v n`
Expected: PASS

**Step 5: Commit**

```bash
git add src/MailPeek/Models/StoredMessage.cs src/MailPeek/Models/MessageSummary.cs tests/MailPeek.Tests/Models/StoredMessageTests.cs
git commit -m "feat: add IsRead property to StoredMessage and MessageSummary"
```

---

## Task 2: Add `MarkAsRead` to Store

**Files:**
- Modify: `src/MailPeek/Storage/IMessageStore.cs`
- Modify: `src/MailPeek/Storage/InMemoryMessageStore.cs`
- Modify: `tests/MailPeek.Tests/Storage/InMemoryMessageStoreTests.cs`

**Step 1: Write the failing tests**

In `tests/MailPeek.Tests/Storage/InMemoryMessageStoreTests.cs`, add:

```csharp
[Fact]
public void MarkAsRead_SetsIsReadTrue()
{
    var msg = CreateMessage("test@example.com", "Hello");
    _store.Add(msg);

    var result = _store.MarkAsRead(msg.Id);

    Assert.True(result);
    Assert.True(_store.GetById(msg.Id)!.IsRead);
}

[Fact]
public void MarkAsRead_ReturnsFalseForMissing()
{
    Assert.False(_store.MarkAsRead(Guid.NewGuid()));
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MailPeek.Tests --filter "MarkAsRead" --no-restore -v n`
Expected: FAIL — `MarkAsRead` doesn't exist.

**Step 3: Write minimal implementation**

In `src/MailPeek/Storage/IMessageStore.cs`, add to the interface:
```csharp
bool MarkAsRead(Guid id);
```

In `src/MailPeek/Storage/InMemoryMessageStore.cs`, add method:
```csharp
public bool MarkAsRead(Guid id)
{
    if (!_messages.TryGetValue(id, out var message))
        return false;

    message.IsRead = true;
    return true;
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/MailPeek.Tests --filter "MarkAsRead" --no-restore -v n`
Expected: PASS

**Step 5: Run all tests**

Run: `dotnet test tests/MailPeek.Tests --no-restore -v n`
Expected: ALL PASS (existing tests still work with new interface method).

**Step 6: Commit**

```bash
git add src/MailPeek/Storage/IMessageStore.cs src/MailPeek/Storage/InMemoryMessageStore.cs tests/MailPeek.Tests/Storage/InMemoryMessageStoreTests.cs
git commit -m "feat: add MarkAsRead to IMessageStore"
```

---

## Task 3: Add `PUT /api/messages/{id}/read` Endpoint

**Files:**
- Modify: `src/MailPeek/Middleware/DashboardApiExtensions.cs`
- Modify: `tests/MailPeek.Tests/Middleware/DashboardApiMiddlewareTests.cs`

**Step 1: Write the failing test**

In `tests/MailPeek.Tests/Middleware/DashboardApiMiddlewareTests.cs`, add a test that calls `PUT /mailpeek/api/messages/{id}/read` and asserts a 200 response plus the message becoming read. Check the existing test setup pattern in that file for how the test host is configured.

```csharp
[Fact]
public async Task MarkAsRead_ReturnsOk()
{
    // Arrange: add a message to the store
    var store = _host.Services.GetRequiredService<IMessageStore>();
    var msg = new StoredMessage { From = "a@b.com", To = ["c@d.com"], Subject = "Test" };
    store.Add(msg);

    // Act
    var response = await _client.PutAsync($"/mailpeek/api/messages/{msg.Id}/read", null).ConfigureAwait(true);

    // Assert
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.True(store.GetById(msg.Id)!.IsRead);
}

[Fact]
public async Task MarkAsRead_ReturnsNotFoundForMissing()
{
    var response = await _client.PutAsync($"/mailpeek/api/messages/{Guid.NewGuid()}/read", null).ConfigureAwait(true);
    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MailPeek.Tests --filter "MarkAsRead_Returns" --no-restore -v n`
Expected: FAIL — endpoint doesn't exist (404).

**Step 3: Write minimal implementation**

In `src/MailPeek/Middleware/DashboardApiExtensions.cs`, add inside `MapMailPeekApi` (after the DELETE endpoints, before `return endpoints;`):

```csharp
endpoints.MapPut($"{api}/messages/{{id:guid}}/read", (Guid id, IMessageStore store) =>
{
    return store.MarkAsRead(id) ? Results.Ok() : Results.NotFound();
});
```

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/MailPeek.Tests --filter "MarkAsRead_Returns" --no-restore -v n`
Expected: PASS

**Step 5: Commit**

```bash
git add src/MailPeek/Middleware/DashboardApiExtensions.cs tests/MailPeek.Tests/Middleware/DashboardApiMiddlewareTests.cs
git commit -m "feat: add PUT /api/messages/{id}/read endpoint"
```

---

## Task 4: Read/Unread UI — Bold Rows, Blue Dot, Badge

**Files:**
- Modify: `src/MailPeek/Assets/js/dashboard.js`
- Modify: `src/MailPeek/Assets/css/dashboard.css`

**Step 1: Update badge to show "unread / total" format**

In `dashboard.js`, in the `renderInbox` function (around line 97-104), change the badge logic. The API response `items` already includes `isRead` from `MessageSummary`. We need to track `totalCount` and `unreadCount`. Add `unreadCount` to the API response or compute it client-side from items:

Actually, we need the unread count from the server. But to keep it simple, we'll update the badge format using what's available. The summary items include `isRead`, so we can count unread from the page. However, for the header badge we want the **total** unread count, not just the page. So add an `unreadCount` field to the paged API response.

In `DashboardApiExtensions.cs`, update the GET messages endpoint to include `unreadCount`:
```csharp
var allMessages = store.GetAll();
var unreadCount = allMessages.Count(m => !m.IsRead);
// ... existing paging ...
return Results.Json(new
{
    items = result.Items.Select(m => m.ToSummary()),
    result.TotalCount,
    unreadCount
}, JsonOptions);
```

In `dashboard.js`, update `renderInbox` badge section:
```javascript
var unreadCount = data.unreadCount || 0;
if (totalCount > 0) {
    badge.textContent = unreadCount > 0
        ? unreadCount + ' / ' + totalCount
        : totalCount + (totalCount === 1 ? ' message' : ' messages');
    badge.classList.add('visible');
} else {
    badge.classList.remove('visible');
}
```

**Step 2: Add bold + blue dot for unread rows**

In `dashboard.js`, in the `renderInbox` row loop (around line 118), add:
```javascript
if (!msg.isRead) {
    tr.classList.add('unread');
}
```

**Step 3: Auto-mark as read when opening**

In `dashboard.js`, in `showMessage` function, after fetching the message (line 210-211), add:
```javascript
if (!msg.isRead) {
    fetch(pathPrefix + '/api/messages/' + id + '/read', { method: 'PUT' });
}
```

**Step 4: Add CSS for unread styling**

In `dashboard.css`, add after the table styles (around line 231):
```css
/* ── Unread Indicator ────────────────────────────────────── */
#messageTable tbody tr.unread td {
    font-weight: 600;
}

#messageTable tbody tr.unread td:first-child::before {
    content: '';
    display: inline-block;
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background: var(--primary);
    margin-right: 8px;
    vertical-align: middle;
}
```

**Step 5: Commit**

```bash
git add src/MailPeek/Assets/js/dashboard.js src/MailPeek/Assets/css/dashboard.css src/MailPeek/Middleware/DashboardApiExtensions.cs
git commit -m "feat: add read/unread UI with bold rows, blue dot, and unread badge"
```

---

## Task 5: Browser Notifications

**Files:**
- Modify: `src/MailPeek/Assets/js/dashboard.js`

**Step 1: Add notification permission request**

In `dashboard.js`, in the `init` function, after `loadMessages()`:
```javascript
if ('Notification' in window && Notification.permission === 'default') {
    Notification.requestPermission();
}
```

**Step 2: Show notification on new message**

In `dashboard.js`, in `setupSignalR`, update the `NewMessage` handler (line 27-29):
```javascript
connection.on('NewMessage', function (summary) {
    loadMessages();
    if ('Notification' in window && Notification.permission === 'granted') {
        var n = new Notification(summary.subject || '(no subject)', {
            body: 'From: ' + (summary.from || 'unknown'),
            tag: summary.id
        });
        n.onclick = function () {
            window.focus();
            showMessage(summary.id);
            n.close();
        };
    }
});
```

**Step 3: Commit**

```bash
git add src/MailPeek/Assets/js/dashboard.js
git commit -m "feat: add browser notifications for new messages"
```

---

## Task 6: Add `Tags` to Models

**Files:**
- Modify: `src/MailPeek/Models/StoredMessage.cs`
- Modify: `src/MailPeek/Models/MessageSummary.cs`
- Modify: `tests/MailPeek.Tests/Models/StoredMessageTests.cs`

**Step 1: Write the failing test**

In `tests/MailPeek.Tests/Models/StoredMessageTests.cs`:

```csharp
[Fact]
public void Tags_DefaultsEmpty()
{
    var msg = new StoredMessage();
    Assert.Empty(msg.Tags);
}

[Fact]
public void ToSummary_IncludesTags()
{
    var msg = new StoredMessage { Tags = ["welcome", "signup"] };
    var summary = msg.ToSummary();
    Assert.Equal(["welcome", "signup"], summary.Tags);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MailPeek.Tests --filter "Tags_DefaultsEmpty|ToSummary_IncludesTags" --no-restore -v n`
Expected: FAIL

**Step 3: Write minimal implementation**

In `src/MailPeek/Models/StoredMessage.cs`, add:
```csharp
public List<string> Tags { get; set; } = [];
```

In `src/MailPeek/Models/MessageSummary.cs`, add:
```csharp
public required IReadOnlyList<string> Tags { get; set; }
```

In `StoredMessage.ToSummary()`, add `Tags = Tags` to the initializer.

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/MailPeek.Tests --filter "Tags_DefaultsEmpty|ToSummary_IncludesTags" --no-restore -v n`
Expected: PASS

**Step 5: Commit**

```bash
git add src/MailPeek/Models/StoredMessage.cs src/MailPeek/Models/MessageSummary.cs tests/MailPeek.Tests/Models/StoredMessageTests.cs
git commit -m "feat: add Tags property to StoredMessage and MessageSummary"
```

---

## Task 7: Add `SetTags` and Tag Filtering to Store

**Files:**
- Modify: `src/MailPeek/Storage/IMessageStore.cs`
- Modify: `src/MailPeek/Storage/InMemoryMessageStore.cs`
- Modify: `tests/MailPeek.Tests/Storage/InMemoryMessageStoreTests.cs`

**Step 1: Write the failing tests**

In `tests/MailPeek.Tests/Storage/InMemoryMessageStoreTests.cs`:

```csharp
[Fact]
public void SetTags_UpdatesMessageTags()
{
    var msg = CreateMessage("test@example.com", "Hello");
    _store.Add(msg);

    var result = _store.SetTags(msg.Id, ["welcome", "test"]);

    Assert.True(result);
    Assert.Equal(["welcome", "test"], _store.GetById(msg.Id)!.Tags);
}

[Fact]
public void SetTags_ReturnsFalseForMissing()
{
    Assert.False(_store.SetTags(Guid.NewGuid(), ["tag"]));
}

[Fact]
public void GetPage_FiltersByTag()
{
    var store = new InMemoryMessageStore(maxMessages: 20);
    var msg1 = CreateMessage("a@test.com", "Tagged");
    var msg2 = CreateMessage("b@test.com", "Untagged");
    store.Add(msg1);
    store.Add(msg2);
    store.SetTags(msg1.Id, ["welcome"]);

    var page = store.GetPage(0, 50, tag: "welcome");

    Assert.Single(page.Items);
    Assert.Equal("Tagged", page.Items[0].Subject);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MailPeek.Tests --filter "SetTags|GetPage_FiltersByTag" --no-restore -v n`
Expected: FAIL

**Step 3: Write minimal implementation**

In `src/MailPeek/Storage/IMessageStore.cs`, add:
```csharp
bool SetTags(Guid id, List<string> tags);
```

Update `GetPage` signature to include an optional `tag` parameter:
```csharp
PagedResult<StoredMessage> GetPage(int pageNumber, int pageSize, string? searchTerm = null, string? tag = null);
```

In `src/MailPeek/Storage/InMemoryMessageStore.cs`, add:
```csharp
public bool SetTags(Guid id, List<string> tags)
{
    if (!_messages.TryGetValue(id, out var message))
        return false;

    message.Tags = tags;
    return true;
}
```

Update `GetPage` to accept and apply the `tag` filter:
```csharp
public PagedResult<StoredMessage> GetPage(int pageNumber, int pageSize, string? searchTerm = null, string? tag = null)
{
    var all = GetAll();

    IEnumerable<StoredMessage> filtered = all;
    if (!string.IsNullOrWhiteSpace(searchTerm))
    {
        var term = searchTerm.ToLowerInvariant();
        filtered = filtered.Where(m =>
            m.Subject.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            m.From.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            m.To.Any(t => t.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    if (!string.IsNullOrWhiteSpace(tag))
    {
        filtered = filtered.Where(m => m.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));
    }

    var list = filtered.ToList();

    return new PagedResult<StoredMessage>
    {
        Items = list.Skip(pageNumber * pageSize).Take(pageSize).ToList(),
        TotalCount = list.Count
    };
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/MailPeek.Tests --filter "SetTags|GetPage_FiltersByTag" --no-restore -v n`
Expected: PASS

**Step 5: Run all tests**

Run: `dotnet test tests/MailPeek.Tests --no-restore -v n`
Expected: ALL PASS

**Step 6: Commit**

```bash
git add src/MailPeek/Storage/IMessageStore.cs src/MailPeek/Storage/InMemoryMessageStore.cs tests/MailPeek.Tests/Storage/InMemoryMessageStoreTests.cs
git commit -m "feat: add SetTags and tag filtering to IMessageStore"
```

---

## Task 8: Add Tag API Endpoints

**Files:**
- Modify: `src/MailPeek/Middleware/DashboardApiExtensions.cs`
- Modify: `tests/MailPeek.Tests/Middleware/DashboardApiMiddlewareTests.cs`

**Step 1: Write the failing tests**

```csharp
[Fact]
public async Task SetTags_ReturnsOk()
{
    var store = _host.Services.GetRequiredService<IMessageStore>();
    var msg = new StoredMessage { From = "a@b.com", To = ["c@d.com"], Subject = "Test" };
    store.Add(msg);

    var response = await _client.PutAsJsonAsync($"/mailpeek/api/messages/{msg.Id}/tags", new[] { "welcome", "test" }).ConfigureAwait(true);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(["welcome", "test"], store.GetById(msg.Id)!.Tags);
}

[Fact]
public async Task GetMessages_FiltersByTag()
{
    var store = _host.Services.GetRequiredService<IMessageStore>();
    var msg1 = new StoredMessage { From = "a@b.com", To = ["c@d.com"], Subject = "Tagged" };
    var msg2 = new StoredMessage { From = "e@f.com", To = ["g@h.com"], Subject = "Other" };
    store.Add(msg1);
    store.Add(msg2);
    store.SetTags(msg1.Id, ["welcome"]);

    var response = await _client.GetStringAsync("/mailpeek/api/messages?tag=welcome").ConfigureAwait(true);

    Assert.Contains("Tagged", response, StringComparison.Ordinal);
    Assert.DoesNotContain("Other", response, StringComparison.Ordinal);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MailPeek.Tests --filter "SetTags_ReturnsOk|GetMessages_FiltersByTag" --no-restore -v n`
Expected: FAIL

**Step 3: Write minimal implementation**

In `DashboardApiExtensions.cs`:

Add `PUT /api/messages/{id}/tags`:
```csharp
endpoints.MapPut($"{api}/messages/{{id:guid}}/tags", async (Guid id, HttpContext context, IMessageStore store) =>
{
    var tags = await context.Request.ReadFromJsonAsync<List<string>>().ConfigureAwait(false);
    if (tags is null) return Results.BadRequest();
    return store.SetTags(id, tags) ? Results.Ok() : Results.NotFound();
});
```

Update the GET messages endpoint to read the `tag` query param and pass it to `GetPage`:
```csharp
var tag = context.Request.Query["tag"].FirstOrDefault();
var result = store.GetPage(page, size, search, tag);
```

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/MailPeek.Tests --filter "SetTags_ReturnsOk|GetMessages_FiltersByTag" --no-restore -v n`
Expected: PASS

**Step 5: Commit**

```bash
git add src/MailPeek/Middleware/DashboardApiExtensions.cs tests/MailPeek.Tests/Middleware/DashboardApiMiddlewareTests.cs
git commit -m "feat: add tag API endpoints (PUT tags, GET with tag filter)"
```

---

## Task 9: Plus-Addressing Auto-Tagging

**Files:**
- Modify: `src/MailPeek/Configuration/MailPeekSmtpOptions.cs`
- Create: `src/MailPeek/Services/PlusAddressTagExtractor.cs`
- Create: `tests/MailPeek.Tests/Services/PlusAddressTagExtractorTests.cs`

**Step 1: Write the failing tests**

Create `tests/MailPeek.Tests/Services/PlusAddressTagExtractorTests.cs`:

```csharp
using MailPeek.Models;
using MailPeek.Services;

namespace MailPeek.Tests.Services;

public class PlusAddressTagExtractorTests
{
    [Fact]
    public void ExtractTags_ExtractsFromPlusAddress()
    {
        var msg = new StoredMessage { To = ["test+signup@example.com"] };
        var tags = PlusAddressTagExtractor.ExtractTags(msg);
        Assert.Equal(["signup"], tags);
    }

    [Fact]
    public void ExtractTags_ReturnsEmptyForNoPlusAddress()
    {
        var msg = new StoredMessage { To = ["test@example.com"] };
        var tags = PlusAddressTagExtractor.ExtractTags(msg);
        Assert.Empty(tags);
    }

    [Fact]
    public void ExtractTags_HandlesMultipleRecipients()
    {
        var msg = new StoredMessage { To = ["a+tag1@x.com", "b+tag2@y.com", "c@z.com"] };
        var tags = PlusAddressTagExtractor.ExtractTags(msg);
        Assert.Equal(["tag1", "tag2"], tags);
    }

    [Fact]
    public void ExtractTags_DeduplicatesTags()
    {
        var msg = new StoredMessage { To = ["a+signup@x.com", "b+signup@y.com"] };
        var tags = PlusAddressTagExtractor.ExtractTags(msg);
        Assert.Single(tags);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MailPeek.Tests --filter "PlusAddressTagExtractor" --no-restore -v n`
Expected: FAIL — class doesn't exist.

**Step 3: Write minimal implementation**

Add `AutoTagPlusAddressing` option to `src/MailPeek/Configuration/MailPeekSmtpOptions.cs`:
```csharp
public bool AutoTagPlusAddressing { get; set; } = true;
```

Create `src/MailPeek/Services/PlusAddressTagExtractor.cs`:
```csharp
using MailPeek.Models;

namespace MailPeek.Services;

public static class PlusAddressTagExtractor
{
    public static List<string> ExtractTags(StoredMessage message)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var address in message.To)
        {
            var plusIndex = address.IndexOf('+', StringComparison.Ordinal);
            var atIndex = address.IndexOf('@', StringComparison.Ordinal);

            if (plusIndex >= 0 && atIndex > plusIndex)
            {
                var tag = address[(plusIndex + 1)..atIndex];
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    tags.Add(tag);
                }
            }
        }

        return [.. tags];
    }
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/MailPeek.Tests --filter "PlusAddressTagExtractor" --no-restore -v n`
Expected: PASS

**Step 5: Wire auto-tagging into message ingestion**

In `src/MailPeek/Hubs/MailPeekHubNotifier.cs`, update `OnMessageReceived` to apply auto-tagging before broadcasting. Alternatively, apply it in `InMemoryMessageStore.Add`. The cleaner place is a dedicated subscriber.

Create `src/MailPeek/Services/AutoTagger.cs`:
```csharp
using MailPeek.Configuration;
using MailPeek.Models;
using MailPeek.Storage;
using Microsoft.Extensions.Options;

namespace MailPeek.Services;

public class AutoTagger(IMessageStore store, IOptions<MailPeekSmtpOptions> options)
{
    public void Start()
    {
        if (options.Value.AutoTagPlusAddressing)
        {
            store.OnMessageReceived += OnMessageReceived;
        }
    }

    private void OnMessageReceived(StoredMessage message)
    {
        var tags = PlusAddressTagExtractor.ExtractTags(message);
        if (tags.Count > 0)
        {
            message.Tags = tags;
        }
    }
}
```

Register in `ServiceCollectionExtensions.cs` — add `services.AddSingleton<AutoTagger>();` to both overloads.

In `DashboardMiddlewareExtensions` (or wherever `Start()` is called on `MailPeekHubNotifier`), also call `AutoTagger.Start()`.

**Step 6: Commit**

```bash
git add src/MailPeek/Configuration/MailPeekSmtpOptions.cs src/MailPeek/Services/PlusAddressTagExtractor.cs src/MailPeek/Services/AutoTagger.cs tests/MailPeek.Tests/Services/PlusAddressTagExtractorTests.cs
git commit -m "feat: add plus-addressing auto-tagging"
```

---

## Task 10: Tag UI — Colored Pills in Inbox and Detail

**Files:**
- Modify: `src/MailPeek/Assets/js/dashboard.js`
- Modify: `src/MailPeek/Assets/css/dashboard.css`

**Step 1: Add tag pill rendering in inbox rows**

In `dashboard.js`, in the inbox row loop, after creating `tdSubject`, append tag pills:

```javascript
if (msg.tags && msg.tags.length > 0) {
    msg.tags.forEach(function (tag) {
        var pill = document.createElement('span');
        pill.className = 'tag-pill';
        pill.textContent = tag;
        pill.style.backgroundColor = tagColor(tag);
        pill.addEventListener('click', function (e) {
            e.stopPropagation();
            document.getElementById('search').value = '';
            filterByTag(tag);
        });
        tdSubject.appendChild(pill);
    });
}
```

**Step 2: Add tag color helper**

In `dashboard.js`, add helper function:

```javascript
function tagColor(tag) {
    var hash = 0;
    for (var i = 0; i < tag.length; i++) {
        hash = tag.charCodeAt(i) + ((hash << 5) - hash);
    }
    var hue = Math.abs(hash) % 360;
    return 'hsl(' + hue + ', 60%, 45%)';
}
```

**Step 3: Add tag filter function**

```javascript
var currentTag = null;

function filterByTag(tag) {
    currentTag = tag === currentTag ? null : tag;
    currentPage = 0;
    loadMessages();
}
```

Update `loadMessages` to include `tag` param:
```javascript
var url = pathPrefix + '/api/messages?page=' + currentPage + '&size=' + pageSize + '&search=' + encodeURIComponent(search);
if (currentTag) {
    url += '&tag=' + encodeURIComponent(currentTag);
}
```

**Step 4: Add tag pills in message detail header**

In `showMessage`, after the meta parts, add tag pills to the header HTML.

**Step 5: Add CSS for tag pills**

```css
/* ── Tag Pills ───────────────────────────────────────────── */
.tag-pill {
    display: inline-block;
    padding: 1px 8px;
    border-radius: 10px;
    color: #fff;
    font-size: 0.72rem;
    font-weight: 600;
    margin-left: 6px;
    cursor: pointer;
    vertical-align: middle;
    transition: opacity 0.15s;
}

.tag-pill:hover {
    opacity: 0.8;
}
```

**Step 6: Commit**

```bash
git add src/MailPeek/Assets/js/dashboard.js src/MailPeek/Assets/css/dashboard.css
git commit -m "feat: add tag pills UI with color coding and click-to-filter"
```

---

## Task 11: Add Link Checking Models

**Files:**
- Create: `src/MailPeek/Models/LinkCheckResult.cs`
- Modify: `src/MailPeek/Models/StoredMessage.cs`

**Step 1: Create the model**

Create `src/MailPeek/Models/LinkCheckResult.cs`:

```csharp
namespace MailPeek.Models;

public enum LinkStatus
{
    Ok,
    Broken,
    Timeout,
    Error
}

public class LinkCheckResult
{
    public required string Url { get; set; }
    public int? StatusCode { get; set; }
    public LinkStatus Status { get; set; }
}
```

**Step 2: Add to StoredMessage**

In `src/MailPeek/Models/StoredMessage.cs`, add:
```csharp
public List<LinkCheckResult>? LinkCheckResults { get; set; }
public bool LinkCheckComplete { get; set; }
```

**Step 3: Commit**

```bash
git add src/MailPeek/Models/LinkCheckResult.cs src/MailPeek/Models/StoredMessage.cs
git commit -m "feat: add LinkCheckResult model and link check fields to StoredMessage"
```

---

## Task 12: Link Checker Service

**Files:**
- Create: `src/MailPeek/Services/LinkChecker.cs`
- Create: `tests/MailPeek.Tests/Services/LinkCheckerTests.cs`

**Step 1: Write the failing tests**

Create `tests/MailPeek.Tests/Services/LinkCheckerTests.cs`:

```csharp
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
        Assert.Equal(["https://example.com", "https://test.com/page"], urls);
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
        var urls = LinkChecker.ExtractUrls(msg);
        Assert.Single(urls);
    }

    [Fact]
    public void ExtractUrls_ReturnsEmptyForNoUrls()
    {
        var msg = new StoredMessage { TextBody = "No links here" };
        var urls = LinkChecker.ExtractUrls(msg);
        Assert.Empty(urls);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MailPeek.Tests --filter "LinkCheckerTests" --no-restore -v n`
Expected: FAIL

**Step 3: Write minimal implementation**

Create `src/MailPeek/Services/LinkChecker.cs`:

```csharp
using System.Text.RegularExpressions;
using MailPeek.Hubs;
using MailPeek.Models;
using MailPeek.Storage;

namespace MailPeek.Services;

public partial class LinkChecker(
    IMessageStore store,
    IHttpClientFactory httpClientFactory,
    MailPeekHubNotifier hubNotifier)
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
            catch
            {
                result.Status = LinkStatus.Error;
            }
            results.Add(result);
        }

        message.LinkCheckResults = results;
        message.LinkCheckComplete = true;

        await hubNotifier.NotifyLinkCheckComplete(message.Id).ConfigureAwait(false);
    }

    public static List<string> ExtractUrls(StoredMessage message)
    {
        var urls = new HashSet<string>(StringComparer.Ordinal);

        if (!string.IsNullOrEmpty(message.HtmlBody))
        {
            foreach (Match match in HrefRegex().Matches(message.HtmlBody))
            {
                urls.Add(match.Groups[1].Value);
            }
        }

        if (!string.IsNullOrEmpty(message.TextBody))
        {
            foreach (Match match in UrlRegex().Matches(message.TextBody))
            {
                urls.Add(match.Value);
            }
        }

        return [.. urls];
    }

    [GeneratedRegex("href=\"(https?://[^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex HrefRegex();

    [GeneratedRegex("https?://[^\\s<>\"]+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();
}
```

**Step 4: Add `NotifyLinkCheckComplete` to hub notifier**

In `src/MailPeek/Hubs/MailPeekHubNotifier.cs`, add:
```csharp
public Task NotifyLinkCheckComplete(Guid id) =>
    hubContext.Clients.All.SendAsync("LinkCheckComplete", id);
```

**Step 5: Run test to verify it passes**

Run: `dotnet test tests/MailPeek.Tests --filter "LinkCheckerTests" --no-restore -v n`
Expected: PASS

**Step 6: Register in DI**

In `ServiceCollectionExtensions.cs`, add to both overloads:
```csharp
services.AddHttpClient("LinkChecker");
services.AddSingleton<LinkChecker>();
```

Call `LinkChecker.Start()` where `MailPeekHubNotifier.Start()` is called.

**Step 7: Commit**

```bash
git add src/MailPeek/Services/LinkChecker.cs src/MailPeek/Hubs/MailPeekHubNotifier.cs tests/MailPeek.Tests/Services/LinkCheckerTests.cs src/MailPeek/Extensions/ServiceCollectionExtensions.cs
git commit -m "feat: add LinkChecker service with URL extraction and background checking"
```

---

## Task 13: Add Link Check API Endpoint

**Files:**
- Modify: `src/MailPeek/Middleware/DashboardApiExtensions.cs`
- Modify: `tests/MailPeek.Tests/Middleware/DashboardApiMiddlewareTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public async Task GetLinks_Returns202WhenChecking()
{
    var store = _host.Services.GetRequiredService<IMessageStore>();
    var msg = new StoredMessage { From = "a@b.com", To = ["c@d.com"], Subject = "Test" };
    store.Add(msg);

    var response = await _client.GetAsync($"/mailpeek/api/messages/{msg.Id}/links").ConfigureAwait(true);
    Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
}

[Fact]
public async Task GetLinks_Returns200WhenComplete()
{
    var store = _host.Services.GetRequiredService<IMessageStore>();
    var msg = new StoredMessage
    {
        From = "a@b.com", To = ["c@d.com"], Subject = "Test",
        LinkCheckComplete = true,
        LinkCheckResults = [new LinkCheckResult { Url = "https://example.com", Status = LinkStatus.Ok, StatusCode = 200 }]
    };
    store.Add(msg);

    var response = await _client.GetAsync($"/mailpeek/api/messages/{msg.Id}/links").ConfigureAwait(true);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MailPeek.Tests --filter "GetLinks" --no-restore -v n`
Expected: FAIL

**Step 3: Write minimal implementation**

In `DashboardApiExtensions.cs`, add:
```csharp
endpoints.MapGet($"{api}/messages/{{id:guid}}/links", (Guid id, IMessageStore store) =>
{
    var msg = store.GetById(id);
    if (msg is null) return Results.NotFound();

    if (!msg.LinkCheckComplete)
        return Results.Json(new { status = "checking" }, statusCode: 202, options: JsonOptions);

    return Results.Json(msg.LinkCheckResults, JsonOptions);
});
```

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/MailPeek.Tests --filter "GetLinks" --no-restore -v n`
Expected: PASS

**Step 5: Commit**

```bash
git add src/MailPeek/Middleware/DashboardApiExtensions.cs tests/MailPeek.Tests/Middleware/DashboardApiMiddlewareTests.cs
git commit -m "feat: add GET /api/messages/{id}/links endpoint"
```

---

## Task 14: Links Tab in UI

**Files:**
- Modify: `src/MailPeek/Assets/index.html`
- Modify: `src/MailPeek/Assets/js/dashboard.js`
- Modify: `src/MailPeek/Assets/css/dashboard.css`

**Step 1: Add Links tab in HTML**

In `index.html`, after the Attachments tab button (line 45), add:
```html
<button class="tab" data-tab="links">Links</button>
```

After the attachments tab panel (line 53), add:
```html
<div id="tab-links" class="tab-panel">
    <div id="linksStatus" class="hidden">Checking links...</div>
    <table id="linksTable" class="hidden">
        <thead><tr><th>URL</th><th>Status</th></tr></thead>
        <tbody id="linksBody"></tbody>
    </table>
</div>
```

**Step 2: Add link check loading in dashboard.js**

In `showMessage`, after rendering attachments, add link loading:

```javascript
// Links
loadLinks(id);

// Listen for link check completion
if (connection) {
    connection.off('LinkCheckComplete');
    connection.on('LinkCheckComplete', function (completedId) {
        if (completedId === id) {
            loadLinks(id);
        }
    });
}
```

Add `loadLinks` function:

```javascript
async function loadLinks(id) {
    var statusEl = document.getElementById('linksStatus');
    var tableEl = document.getElementById('linksTable');
    var tbody = document.getElementById('linksBody');

    try {
        var response = await fetch(pathPrefix + '/api/messages/' + id + '/links');
        if (response.status === 202) {
            statusEl.classList.remove('hidden');
            tableEl.classList.add('hidden');
            return;
        }

        var links = await response.json();
        statusEl.classList.add('hidden');
        tbody.innerHTML = '';

        if (!links || links.length === 0) {
            statusEl.textContent = 'No links found.';
            statusEl.classList.remove('hidden');
            tableEl.classList.add('hidden');
            return;
        }

        tableEl.classList.remove('hidden');
        links.forEach(function (link) {
            var tr = document.createElement('tr');
            var tdUrl = document.createElement('td');
            var a = document.createElement('a');
            a.href = link.url;
            a.textContent = link.url;
            a.target = '_blank';
            a.rel = 'noopener';
            tdUrl.appendChild(a);

            var tdStatus = document.createElement('td');
            var statusSpan = document.createElement('span');
            statusSpan.className = 'link-status link-status-' + link.status.toString().toLowerCase();
            statusSpan.textContent = link.statusCode ? link.status + ' (' + link.statusCode + ')' : link.status;
            tdStatus.appendChild(statusSpan);

            tr.appendChild(tdUrl);
            tr.appendChild(tdStatus);
            tbody.appendChild(tr);
        });
    } catch (err) {
        console.error('Error loading links:', err);
    }
}
```

**Step 3: Add CSS for links**

```css
/* ── Link Status ─────────────────────────────────────────── */
#linksTable {
    width: 100%;
    border-collapse: collapse;
}

#linksTable th,
#linksTable td {
    padding: 8px 12px;
    border-bottom: 1px solid var(--border);
    font-size: 0.85rem;
    text-align: left;
}

#linksTable a {
    color: var(--primary);
    text-decoration: none;
    word-break: break-all;
}

#linksTable a:hover {
    text-decoration: underline;
}

.link-status { font-weight: 600; }
.link-status-ok { color: #22c55e; }
.link-status-broken { color: var(--danger); }
.link-status-timeout { color: #f59e0b; }
.link-status-error { color: var(--danger); }

#linksStatus {
    padding: 16px;
    color: var(--text-muted);
    font-style: italic;
}
```

**Step 4: Commit**

```bash
git add src/MailPeek/Assets/index.html src/MailPeek/Assets/js/dashboard.js src/MailPeek/Assets/css/dashboard.css
git commit -m "feat: add Links tab in message detail UI"
```

---

## Task 15: Webhook Support — Options and Service

**Files:**
- Modify: `src/MailPeek/Configuration/MailPeekSmtpOptions.cs`
- Create: `src/MailPeek/Services/WebhookNotifier.cs`
- Create: `tests/MailPeek.Tests/Services/WebhookNotifierTests.cs`

**Step 1: Add WebhookUrl to options**

In `MailPeekSmtpOptions.cs`, add:
```csharp
public string? WebhookUrl { get; set; }
```

**Step 2: Write the failing tests**

Create `tests/MailPeek.Tests/Services/WebhookNotifierTests.cs`:

```csharp
using System.Net;
using MailPeek.Configuration;
using MailPeek.Models;
using MailPeek.Services;
using MailPeek.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace MailPeek.Tests.Services;

public class WebhookNotifierTests
{
    [Fact]
    public async Task OnMessage_PostsToWebhookUrl()
    {
        // Use a custom HttpMessageHandler to capture the request
        var handler = new TestHandler(HttpStatusCode.OK);
        var httpClient = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("Webhook").Returns(httpClient);

        var options = Options.Create(new MailPeekSmtpOptions { WebhookUrl = "https://example.com/hook" });
        var store = new InMemoryMessageStore();
        var logger = NullLogger<WebhookNotifier>.Instance;
        var notifier = new WebhookNotifier(store, factory, options, logger);
        notifier.Start();

        var msg = new StoredMessage
        {
            From = "sender@test.com",
            To = ["recipient@test.com"],
            Subject = "Hello"
        };
        store.Add(msg);

        // Give the background task time to fire
        await Task.Delay(500).ConfigureAwait(true);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("https://example.com/hook", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public void Start_DoesNothingWhenNoWebhookUrl()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        var options = Options.Create(new MailPeekSmtpOptions { WebhookUrl = null });
        var store = new InMemoryMessageStore();
        var logger = NullLogger<WebhookNotifier>.Instance;
        var notifier = new WebhookNotifier(store, factory, options, logger);
        notifier.Start(); // Should not throw

        store.Add(new StoredMessage { From = "a@b.com", To = ["c@d.com"], Subject = "Test" });
        // No exception = success, webhook not subscribed
    }

    private sealed class TestHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }
}
```

**Step 3: Run test to verify it fails**

Run: `dotnet test tests/MailPeek.Tests --filter "WebhookNotifierTests" --no-restore -v n`
Expected: FAIL

**Step 4: Write minimal implementation**

Create `src/MailPeek/Services/WebhookNotifier.cs`:

```csharp
using System.Text.Json;
using MailPeek.Configuration;
using MailPeek.Models;
using MailPeek.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MailPeek.Services;

public class WebhookNotifier(
    IMessageStore store,
    IHttpClientFactory httpClientFactory,
    IOptions<MailPeekSmtpOptions> options,
    ILogger<WebhookNotifier> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void Start()
    {
        if (!string.IsNullOrEmpty(options.Value.WebhookUrl))
        {
            store.OnMessageReceived += OnMessageReceived;
        }
    }

    private void OnMessageReceived(StoredMessage message)
    {
        _ = Task.Run(() => SendWebhookAsync(message));
    }

    private async Task SendWebhookAsync(StoredMessage message)
    {
        try
        {
            using var client = httpClientFactory.CreateClient("Webhook");
            client.Timeout = TimeSpan.FromSeconds(5);

            var payload = new
            {
                id = message.Id,
                from = message.From,
                to = message.To,
                subject = message.Subject,
                receivedAt = message.ReceivedAt
            };

            var json = JsonSerializer.Serialize(payload, JsonOptions);
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            await client.PostAsync(options.Value.WebhookUrl, content).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send webhook for message {MessageId}", message.Id);
        }
    }
}
```

**Step 5: Run test to verify it passes**

Run: `dotnet test tests/MailPeek.Tests --filter "WebhookNotifierTests" --no-restore -v n`
Expected: PASS

**Step 6: Register in DI**

In `ServiceCollectionExtensions.cs`, add to both overloads:
```csharp
services.AddHttpClient("Webhook");
services.AddSingleton<WebhookNotifier>();
```

Wire `WebhookUrl` option in the configure lambda. Call `WebhookNotifier.Start()` alongside the other services.

**Step 7: Commit**

```bash
git add src/MailPeek/Configuration/MailPeekSmtpOptions.cs src/MailPeek/Services/WebhookNotifier.cs tests/MailPeek.Tests/Services/WebhookNotifierTests.cs src/MailPeek/Extensions/ServiceCollectionExtensions.cs
git commit -m "feat: add webhook support with fire-and-forget POST on new messages"
```

---

## Task 16: Wire Up All Service Start Calls

**Files:**
- Modify: `src/MailPeek/Middleware/DashboardMiddlewareExtensions.cs` (or wherever `MailPeekHubNotifier.Start()` is called)
- Modify: `src/MailPeek/Extensions/ServiceCollectionExtensions.cs`

**Step 1: Ensure all services are started**

Find where `MailPeekHubNotifier.Start()` is called (likely in `UseMailPeek` middleware extension). Add:
```csharp
var autoTagger = app.ApplicationServices.GetService<AutoTagger>();
autoTagger?.Start();

var linkChecker = app.ApplicationServices.GetService<LinkChecker>();
linkChecker?.Start();

var webhookNotifier = app.ApplicationServices.GetService<WebhookNotifier>();
webhookNotifier?.Start();
```

**Step 2: Ensure all options are wired**

In the `AddMailPeek(Action<MailPeekSmtpOptions>)` overload, ensure `WebhookUrl` and `AutoTagPlusAddressing` are copied in the configure lambda.

**Step 3: Run all tests**

Run: `dotnet test tests/MailPeek.Tests --no-restore -v n`
Expected: ALL PASS

**Step 4: Commit**

```bash
git add src/MailPeek/Middleware/ src/MailPeek/Extensions/ServiceCollectionExtensions.cs
git commit -m "feat: wire up AutoTagger, LinkChecker, and WebhookNotifier startup"
```

---

## Task 17: Full Integration Test

**Files:**
- Modify: `tests/MailPeek.Tests/Integration/EndToEndTests.cs`

**Step 1: Run full build and all tests**

Run: `dotnet build --no-restore -c Release && dotnet test --no-build -c Release -v n`
Expected: ALL PASS, zero warnings.

**Step 2: Commit any fixes**

If any test adjustments needed from the integration.

```bash
git commit -m "test: fix integration tests for new features"
```

---

## Summary of All New/Modified Files

**New files:**
- `src/MailPeek/Models/LinkCheckResult.cs`
- `src/MailPeek/Services/PlusAddressTagExtractor.cs`
- `src/MailPeek/Services/AutoTagger.cs`
- `src/MailPeek/Services/LinkChecker.cs`
- `src/MailPeek/Services/WebhookNotifier.cs`
- `tests/MailPeek.Tests/Services/PlusAddressTagExtractorTests.cs`
- `tests/MailPeek.Tests/Services/LinkCheckerTests.cs`
- `tests/MailPeek.Tests/Services/WebhookNotifierTests.cs`

**Modified files:**
- `src/MailPeek/Models/StoredMessage.cs` — `IsRead`, `Tags`, `LinkCheckResults`, `LinkCheckComplete`
- `src/MailPeek/Models/MessageSummary.cs` — `IsRead`, `Tags`
- `src/MailPeek/Storage/IMessageStore.cs` — `MarkAsRead`, `SetTags`, `GetPage` tag param
- `src/MailPeek/Storage/InMemoryMessageStore.cs` — implementations
- `src/MailPeek/Middleware/DashboardApiExtensions.cs` — new endpoints
- `src/MailPeek/Hubs/MailPeekHubNotifier.cs` — `NotifyLinkCheckComplete`
- `src/MailPeek/Configuration/MailPeekSmtpOptions.cs` — `AutoTagPlusAddressing`, `WebhookUrl`
- `src/MailPeek/Extensions/ServiceCollectionExtensions.cs` — DI registrations
- `src/MailPeek/Middleware/DashboardMiddlewareExtensions.cs` — service startup
- `src/MailPeek/Assets/index.html` — Links tab
- `src/MailPeek/Assets/js/dashboard.js` — all UI features
- `src/MailPeek/Assets/css/dashboard.css` — unread, tags, links styles
- `tests/MailPeek.Tests/Models/StoredMessageTests.cs`
- `tests/MailPeek.Tests/Storage/InMemoryMessageStoreTests.cs`
- `tests/MailPeek.Tests/Middleware/DashboardApiMiddlewareTests.cs`
