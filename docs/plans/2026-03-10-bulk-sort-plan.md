# Bulk Operations & Sort Options Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add checkbox-based bulk delete and clickable column header sorting to the MailPeek inbox dashboard.

**Architecture:** Extend the existing `IMessageStore`/`InMemoryMessageStore` with `DeleteMany()` and sorted `GetPage()`. Add bulk-delete and sort query params to the REST API. Update the embedded dashboard JS/CSS/HTML for checkboxes, floating bulk bar, and sortable column headers.

**Tech Stack:** C# (.NET 8/9), xUnit + NSubstitute, vanilla JS, CSS, SignalR

---

### Task 1: Add `DeleteMany` to store interface and implementation

**Files:**
- Modify: `src/MailPeek/Storage/IMessageStore.cs`
- Modify: `src/MailPeek/Storage/InMemoryMessageStore.cs`
- Test: `tests/MailPeek.Tests/Storage/InMemoryMessageStoreTests.cs`

**Step 1: Write the failing tests**

Add to `InMemoryMessageStoreTests.cs` after the existing `Delete_ReturnsFalseForMissing` test:

```csharp
[Fact]
public void DeleteMany_RemovesMultipleMessages()
{
    var msg1 = CreateMessage("a@test.com", "First");
    var msg2 = CreateMessage("b@test.com", "Second");
    var msg3 = CreateMessage("c@test.com", "Third");
    _store.Add(msg1);
    _store.Add(msg2);
    _store.Add(msg3);

    var deleted = _store.DeleteMany([msg1.Id, msg3.Id]);
    Assert.Equal(2, deleted);
    Assert.Equal(1, _store.GetAll().Count);
    Assert.Equal("Second", _store.GetAll()[0].Subject);
}

[Fact]
public void DeleteMany_IgnoresMissingIds()
{
    var msg = CreateMessage("a@test.com", "First");
    _store.Add(msg);

    var deleted = _store.DeleteMany([msg.Id, Guid.NewGuid()]);
    Assert.Equal(1, deleted);
    Assert.Empty(_store.GetAll());
}

[Fact]
public void DeleteMany_ReturnsZeroForEmptyList()
{
    _store.Add(CreateMessage("a@test.com", "First"));
    var deleted = _store.DeleteMany([]);
    Assert.Equal(0, deleted);
    Assert.Equal(1, _store.GetAll().Count);
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MailPeek.Tests --filter "FullyQualifiedName~DeleteMany" -v minimal`
Expected: FAIL — `DeleteMany` does not exist

**Step 3: Add `DeleteMany` to the interface**

In `IMessageStore.cs`, add after `bool Delete(Guid id);`:

```csharp
int DeleteMany(IReadOnlyList<Guid> ids);
```

**Step 4: Implement `DeleteMany` in `InMemoryMessageStore`**

Add after the `Delete` method:

```csharp
public int DeleteMany(IReadOnlyList<Guid> ids)
{
    var count = 0;
    lock (_orderLock)
    {
        foreach (var id in ids)
        {
            if (_messages.TryRemove(id, out _))
            {
                _order.Remove(id);
                count++;
            }
        }
    }
    return count;
}
```

**Step 5: Run tests to verify they pass**

Run: `dotnet test tests/MailPeek.Tests --filter "FullyQualifiedName~DeleteMany" -v minimal`
Expected: 3 tests PASS

**Step 6: Commit**

```bash
git add src/MailPeek/Storage/IMessageStore.cs src/MailPeek/Storage/InMemoryMessageStore.cs tests/MailPeek.Tests/Storage/InMemoryMessageStoreTests.cs
git commit -m "feat: add DeleteMany to IMessageStore for bulk delete"
```

---

### Task 2: Add sorting support to `GetPage`

**Files:**
- Modify: `src/MailPeek/Storage/IMessageStore.cs`
- Modify: `src/MailPeek/Storage/InMemoryMessageStore.cs`
- Test: `tests/MailPeek.Tests/Storage/InMemoryMessageStoreTests.cs`

**Step 1: Write the failing tests**

Add to `InMemoryMessageStoreTests.cs`:

```csharp
[Fact]
public void GetPage_SortsByFromAscending()
{
    var store = new InMemoryMessageStore(maxMessages: 20);
    store.Add(CreateMessage("charlie@test.com", "C"));
    store.Add(CreateMessage("alice@test.com", "A"));
    store.Add(CreateMessage("bob@test.com", "B"));

    var page = store.GetPage(0, 10, sortBy: "from", sortDescending: false);
    Assert.Equal("alice@test.com", page.Items[0].From);
    Assert.Equal("bob@test.com", page.Items[1].From);
    Assert.Equal("charlie@test.com", page.Items[2].From);
}

[Fact]
public void GetPage_SortsBySubjectDescending()
{
    var store = new InMemoryMessageStore(maxMessages: 20);
    store.Add(CreateMessage("a@test.com", "Alpha"));
    store.Add(CreateMessage("b@test.com", "Charlie"));
    store.Add(CreateMessage("c@test.com", "Bravo"));

    var page = store.GetPage(0, 10, sortBy: "subject", sortDescending: true);
    Assert.Equal("Charlie", page.Items[0].Subject);
    Assert.Equal("Bravo", page.Items[1].Subject);
    Assert.Equal("Alpha", page.Items[2].Subject);
}

[Fact]
public void GetPage_SortsByDateAscending()
{
    var store = new InMemoryMessageStore(maxMessages: 20);
    var msg1 = CreateMessage("a@test.com", "Old");
    msg1.ReceivedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    var msg2 = CreateMessage("b@test.com", "New");
    msg2.ReceivedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    store.Add(msg1);
    store.Add(msg2);

    var page = store.GetPage(0, 10, sortBy: "date", sortDescending: false);
    Assert.Equal("Old", page.Items[0].Subject);
    Assert.Equal("New", page.Items[1].Subject);
}

[Fact]
public void GetPage_DefaultSortIsDateDescending()
{
    var store = new InMemoryMessageStore(maxMessages: 20);
    var msg1 = CreateMessage("a@test.com", "Old");
    msg1.ReceivedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    var msg2 = CreateMessage("b@test.com", "New");
    msg2.ReceivedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    store.Add(msg1);
    store.Add(msg2);

    var page = store.GetPage(0, 10);
    // Default (no sort params) preserves insertion order = newest first
    Assert.Equal("New", page.Items[0].Subject);
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MailPeek.Tests --filter "FullyQualifiedName~SortsBy" -v minimal`
Expected: FAIL — `GetPage` doesn't have `sortBy`/`sortDescending` params

**Step 3: Update `GetPage` signature in interface**

Change the `GetPage` signature in `IMessageStore.cs` to:

```csharp
PagedResult<StoredMessage> GetPage(int pageNumber, int pageSize, string? searchTerm = null, string? tag = null, string? sortBy = null, bool sortDescending = true);
```

**Step 4: Update `GetPage` implementation**

Replace the `GetPage` method in `InMemoryMessageStore.cs`:

```csharp
public PagedResult<StoredMessage> GetPage(int pageNumber, int pageSize, string? searchTerm = null, string? tag = null, string? sortBy = null, bool sortDescending = true)
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

    if (!string.IsNullOrWhiteSpace(sortBy))
    {
        list = sortBy.ToLowerInvariant() switch
        {
            "from" => sortDescending
                ? list.OrderByDescending(m => m.From, StringComparer.OrdinalIgnoreCase).ToList()
                : list.OrderBy(m => m.From, StringComparer.OrdinalIgnoreCase).ToList(),
            "subject" => sortDescending
                ? list.OrderByDescending(m => m.Subject, StringComparer.OrdinalIgnoreCase).ToList()
                : list.OrderBy(m => m.Subject, StringComparer.OrdinalIgnoreCase).ToList(),
            "date" => sortDescending
                ? list.OrderByDescending(m => m.ReceivedAt).ToList()
                : list.OrderBy(m => m.ReceivedAt).ToList(),
            _ => list
        };
    }

    return new PagedResult<StoredMessage>
    {
        Items = list.Skip(pageNumber * pageSize).Take(pageSize).ToList(),
        TotalCount = list.Count
    };
}
```

**Step 5: Run all tests**

Run: `dotnet test tests/MailPeek.Tests -v minimal`
Expected: All tests PASS (existing + 4 new sort tests)

**Step 6: Commit**

```bash
git add src/MailPeek/Storage/IMessageStore.cs src/MailPeek/Storage/InMemoryMessageStore.cs tests/MailPeek.Tests/Storage/InMemoryMessageStoreTests.cs
git commit -m "feat: add sort support to GetPage with sortBy and sortDescending params"
```

---

### Task 3: Add bulk-delete API endpoint

**Files:**
- Modify: `src/MailPeek/Middleware/DashboardApiExtensions.cs`
- Modify: `src/MailPeek/Hubs/MailPeekHubNotifier.cs`
- Test: `tests/MailPeek.Tests/Middleware/DashboardApiMiddlewareTests.cs`

**Step 1: Write the failing tests**

Add to `DashboardApiMiddlewareTests.cs`:

```csharp
[Fact]
public async Task BulkDelete_RemovesSelectedMessages()
{
    var msg1 = new StoredMessage { From = "a@t.com", To = ["b@t.com"], Subject = "1" };
    var msg2 = new StoredMessage { From = "c@t.com", To = ["d@t.com"], Subject = "2" };
    var msg3 = new StoredMessage { From = "e@t.com", To = ["f@t.com"], Subject = "3" };
    _store.Add(msg1);
    _store.Add(msg2);
    _store.Add(msg3);

    var request = new HttpRequestMessage(HttpMethod.Delete, "/mailpeek/api/messages/bulk")
    {
        Content = JsonContent.Create(new { ids = new[] { msg1.Id, msg3.Id } })
    };
    var response = await _client!.SendAsync(request);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
    Assert.Equal(2, result.GetProperty("deleted").GetInt32());
    Assert.Equal(1, _store.GetAll().Count);
}

[Fact]
public async Task BulkDelete_Returns400ForMissingBody()
{
    var request = new HttpRequestMessage(HttpMethod.Delete, "/mailpeek/api/messages/bulk");
    var response = await _client!.SendAsync(request);
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MailPeek.Tests --filter "FullyQualifiedName~BulkDelete" -v minimal`
Expected: FAIL — 404 (route doesn't exist yet)

**Step 3: Add `NotifyMessagesDeleted` to hub notifier**

In `MailPeekHubNotifier.cs`, add after `NotifyMessageDeleted`:

```csharp
public Task NotifyMessagesDeleted(IReadOnlyList<Guid> ids) =>
    hubContext.Clients.All.SendAsync("MessagesDeleted", ids);
```

**Step 4: Add bulk-delete endpoint**

In `DashboardApiExtensions.cs`, add after the existing `MapDelete` for single messages (the `DELETE /api/messages/{id:guid}` endpoint) and **before** the `MapDelete` for clearing all messages:

```csharp
endpoints.MapDelete($"{api}/messages/bulk", async (HttpContext context, IMessageStore store) =>
{
    var notifier = context.RequestServices.GetService<MailPeekHubNotifier>();
    var body = await context.Request.ReadFromJsonAsync<BulkDeleteRequest>().ConfigureAwait(false);
    if (body?.Ids is null || body.Ids.Count == 0) return Results.BadRequest();

    var deleted = store.DeleteMany(body.Ids);

    if (notifier is not null)
        await notifier.NotifyMessagesDeleted(body.Ids).ConfigureAwait(false);

    return Results.Json(new { deleted }, JsonOptions);
});
```

Also add a private record at the bottom of the class (outside the method, inside the class):

```csharp
private sealed record BulkDeleteRequest(List<Guid> Ids);
```

**Important:** The `MapDelete` for `/messages/bulk` must be registered **before** the `MapDelete` for `/messages` (the clear-all endpoint) to avoid route conflicts.

**Step 5: Run tests**

Run: `dotnet test tests/MailPeek.Tests --filter "FullyQualifiedName~BulkDelete" -v minimal`
Expected: 2 tests PASS

**Step 6: Commit**

```bash
git add src/MailPeek/Middleware/DashboardApiExtensions.cs src/MailPeek/Hubs/MailPeekHubNotifier.cs tests/MailPeek.Tests/Middleware/DashboardApiMiddlewareTests.cs
git commit -m "feat: add DELETE /api/messages/bulk endpoint for bulk delete"
```

---

### Task 4: Add sort query params to GET messages API

**Files:**
- Modify: `src/MailPeek/Middleware/DashboardApiExtensions.cs`
- Test: `tests/MailPeek.Tests/Middleware/DashboardApiMiddlewareTests.cs`

**Step 1: Write the failing test**

Add to `DashboardApiMiddlewareTests.cs`:

```csharp
[Fact]
public async Task GetMessages_SortsByFromAscending()
{
    _store.Add(new StoredMessage { From = "charlie@t.com", To = ["r@t.com"], Subject = "C" });
    _store.Add(new StoredMessage { From = "alice@t.com", To = ["r@t.com"], Subject = "A" });
    _store.Add(new StoredMessage { From = "bob@t.com", To = ["r@t.com"], Subject = "B" });

    var response = await _client!.GetAsync("/mailpeek/api/messages?sortBy=from&sortDesc=false");
    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
    var items = result.GetProperty("items");
    Assert.Equal("alice@t.com", items[0].GetProperty("from").GetString());
    Assert.Equal("bob@t.com", items[1].GetProperty("from").GetString());
    Assert.Equal("charlie@t.com", items[2].GetProperty("from").GetString());
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MailPeek.Tests --filter "FullyQualifiedName~SortsByFrom" -v minimal`
Expected: FAIL — results are in insertion order, not sorted by From

**Step 3: Update the GET messages endpoint**

In `DashboardApiExtensions.cs`, update the `MapGet` for messages to parse sort params and pass them to `GetPage`:

```csharp
endpoints.MapGet($"{api}/messages", (HttpContext context, IMessageStore store) =>
{
    var page = int.TryParse(context.Request.Query["page"], CultureInfo.InvariantCulture, out var p) ? p : 0;
    var size = int.TryParse(context.Request.Query["size"], CultureInfo.InvariantCulture, out var s) ? s : 50;
    var search = context.Request.Query["search"].FirstOrDefault();
    var tag = context.Request.Query["tag"].FirstOrDefault();
    var sortBy = context.Request.Query["sortBy"].FirstOrDefault();
    var sortDesc = !bool.TryParse(context.Request.Query["sortDesc"], out var sd) || sd;

    var result = store.GetPage(page, size, search, tag, sortBy, sortDesc);
    var unreadCount = store.GetUnreadCount();
    return Results.Json(new
    {
        items = result.Items.Select(m => m.ToSummary()),
        result.TotalCount,
        unreadCount
    }, JsonOptions);
});
```

**Step 4: Run all tests**

Run: `dotnet test tests/MailPeek.Tests -v minimal`
Expected: All tests PASS

**Step 5: Commit**

```bash
git add src/MailPeek/Middleware/DashboardApiExtensions.cs tests/MailPeek.Tests/Middleware/DashboardApiMiddlewareTests.cs
git commit -m "feat: add sortBy and sortDesc query params to GET /api/messages"
```

---

### Task 5: Add checkbox column and bulk bar to HTML

**Files:**
- Modify: `src/MailPeek/Assets/index.html`

**Step 1: Add checkbox column header**

Replace the `<thead>` section with:

```html
<thead>
    <tr>
        <th class="col-checkbox"><input type="checkbox" id="selectAll" title="Select all" /></th>
        <th class="sortable" data-sort="from">From</th>
        <th>To</th>
        <th class="sortable" data-sort="subject">Subject</th>
        <th class="sortable active" data-sort="date">Date <span class="sort-arrow">&#9660;</span></th>
        <th><span class="sr-only">Actions</span></th>
    </tr>
</thead>
```

**Step 2: Add floating bulk bar**

Add just before the closing `</main>` tag:

```html
<div id="bulkBar" class="bulk-bar hidden">
    <span id="bulkCount">0 selected</span>
    <button id="bulkDelete" class="btn btn-danger btn-sm">Delete</button>
    <button id="bulkClear" class="btn btn-sm">Clear selection</button>
</div>
```

**Step 3: Commit**

```bash
git add src/MailPeek/Assets/index.html
git commit -m "feat: add checkbox column, sortable headers, and bulk bar to HTML"
```

---

### Task 6: Add bulk operations and sort CSS

**Files:**
- Modify: `src/MailPeek/Assets/css/dashboard.css`

**Step 1: Add styles**

Add before the `/* ── Responsive */` section:

```css
/* ── Checkbox Column ─────────────────────────────────────── */
.col-checkbox {
    width: 40px;
    text-align: center;
}

.col-checkbox input[type="checkbox"],
.row-checkbox {
    width: 16px;
    height: 16px;
    cursor: pointer;
    accent-color: var(--primary);
}

#messageTable tbody tr.selected {
    background: color-mix(in srgb, var(--primary) 8%, var(--surface));
}

/* ── Sortable Headers ────────────────────────────────────── */
#messageTable thead th.sortable {
    cursor: pointer;
    user-select: none;
    transition: color 0.15s;
}

#messageTable thead th.sortable:hover {
    color: var(--primary);
}

.sort-arrow {
    font-size: 0.7rem;
    margin-left: 4px;
    opacity: 0.6;
}

#messageTable thead th.sortable.active {
    color: var(--primary);
}

#messageTable thead th.sortable.active .sort-arrow {
    opacity: 1;
}

/* ── Bulk Action Bar ─────────────────────────────────────── */
.bulk-bar {
    position: fixed;
    bottom: 0;
    left: 0;
    right: 0;
    z-index: 200;
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 12px;
    padding: 12px 24px;
    background: var(--header-bg);
    color: var(--header-text);
    box-shadow: 0 -2px 8px rgba(0, 0, 0, 0.15);
    transform: translateY(100%);
    transition: transform 0.2s ease;
}

.bulk-bar.visible {
    transform: translateY(0);
}

.bulk-bar #bulkCount {
    font-weight: 600;
    font-size: 0.9rem;
}
```

**Step 2: Update responsive section**

In the `@media (max-width: 768px)` block, add:

```css
#messageTable tbody td.col-checkbox {
    display: none;
}

#messageTable thead th.col-checkbox {
    display: none;
}
```

Note: On mobile, checkboxes are hidden. Bulk operations are only available on desktop/tablet.

**Step 3: Fix unread indicator selector**

The existing unread indicator targets `td:first-child::after`. With the new checkbox column, `td:first-child` is now the checkbox. Update the selector from:

```css
#messageTable tbody tr.unread td:first-child::after {
```

to:

```css
#messageTable tbody tr.unread td:nth-child(2)::after {
```

**Step 4: Commit**

```bash
git add src/MailPeek/Assets/css/dashboard.css
git commit -m "feat: add CSS for checkboxes, sortable headers, and floating bulk bar"
```

---

### Task 7: Implement bulk operations and sorting in JavaScript

**Files:**
- Modify: `src/MailPeek/Assets/js/dashboard.js`

**Step 1: Add state variables**

After `let currentTag = null;` add:

```javascript
let selectedIds = new Set();
let currentSort = 'date';
let sortDescending = true;
```

**Step 2: Update `loadMessages()` to include sort params**

Replace the URL building in `loadMessages()`:

```javascript
var url = pathPrefix + '/api/messages?page=' + currentPage + '&size=' + pageSize + '&search=' + encodeURIComponent(search);
if (currentTag) {
    url += '&tag=' + encodeURIComponent(currentTag);
}
if (currentSort) {
    url += '&sortBy=' + currentSort + '&sortDesc=' + sortDescending;
}
```

**Step 3: Add checkbox cell to each row in `renderInbox()`**

Inside the `items.forEach(function (msg) { ... })` block, after creating `tr` and before creating `tdFrom`, add:

```javascript
var tdCheck = document.createElement('td');
tdCheck.className = 'col-checkbox';
var checkbox = document.createElement('input');
checkbox.type = 'checkbox';
checkbox.className = 'row-checkbox';
checkbox.checked = selectedIds.has(msg.id);
checkbox.addEventListener('click', function (e) {
    e.stopPropagation();
});
checkbox.addEventListener('change', function (e) {
    if (e.target.checked) {
        selectedIds.add(msg.id);
        tr.classList.add('selected');
    } else {
        selectedIds.delete(msg.id);
        tr.classList.remove('selected');
    }
    updateBulkBar();
    updateSelectAll();
});
tdCheck.appendChild(checkbox);
```

Also add `if (selectedIds.has(msg.id)) { tr.classList.add('selected'); }` after the unread class logic.

Update the `tr.appendChild` calls to include `tdCheck` first:

```javascript
tr.appendChild(tdCheck);
tr.appendChild(tdFrom);
tr.appendChild(tdTo);
tr.appendChild(tdSubject);
tr.appendChild(tdDate);
tr.appendChild(tdActions);
```

**Step 4: Update renderInbox to sync select-all checkbox**

At the end of `renderInbox`, after `renderPagination(totalCount)`, add:

```javascript
updateSelectAll();
updateBulkBar();
```

**Step 5: Add helper functions**

Add before the `return { init: init };`:

```javascript
// ── Bulk Operations ──────────────────────────────────
function updateBulkBar() {
    var bar = document.getElementById('bulkBar');
    var count = document.getElementById('bulkCount');
    if (selectedIds.size > 0) {
        bar.classList.remove('hidden');
        bar.classList.add('visible');
        count.textContent = selectedIds.size + ' selected';
    } else {
        bar.classList.add('hidden');
        bar.classList.remove('visible');
    }
}

function updateSelectAll() {
    var selectAll = document.getElementById('selectAll');
    var checkboxes = document.querySelectorAll('.row-checkbox');
    if (checkboxes.length === 0) {
        selectAll.checked = false;
        selectAll.indeterminate = false;
        return;
    }
    var checkedCount = selectedIds.size;
    selectAll.checked = checkedCount === checkboxes.length && checkedCount > 0;
    selectAll.indeterminate = checkedCount > 0 && checkedCount < checkboxes.length;
}

async function bulkDelete() {
    if (selectedIds.size === 0) return;
    if (!confirm('Delete ' + selectedIds.size + ' message(s)?')) return;
    try {
        var response = await fetch(pathPrefix + '/api/messages/bulk', {
            method: 'DELETE',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ ids: Array.from(selectedIds) })
        });
        if (!response.ok) throw new Error('Bulk delete failed');
        selectedIds.clear();
        loadMessages();
    } catch (err) {
        console.error('Error during bulk delete:', err);
    }
}

function clearSelection() {
    selectedIds.clear();
    document.querySelectorAll('.row-checkbox').forEach(function (cb) { cb.checked = false; });
    document.querySelectorAll('#messageTable tbody tr.selected').forEach(function (tr) { tr.classList.remove('selected'); });
    updateBulkBar();
    updateSelectAll();
}

// ── Sorting ──────────────────────────────────────────
function setSort(field) {
    if (currentSort === field) {
        sortDescending = !sortDescending;
    } else {
        currentSort = field;
        sortDescending = true;
    }
    currentPage = 0;
    updateSortHeaders();
    loadMessages();
}

function updateSortHeaders() {
    document.querySelectorAll('#messageTable thead th.sortable').forEach(function (th) {
        var field = th.getAttribute('data-sort');
        var arrow = th.querySelector('.sort-arrow');
        if (field === currentSort) {
            th.classList.add('active');
            if (!arrow) {
                arrow = document.createElement('span');
                arrow.className = 'sort-arrow';
                th.appendChild(arrow);
            }
            arrow.innerHTML = sortDescending ? '&#9660;' : '&#9650;';
        } else {
            th.classList.remove('active');
            if (arrow) arrow.remove();
        }
    });
}
```

**Step 6: Add event listeners in `setupEventListeners()`**

Add after the existing event listeners:

```javascript
// Select all checkbox
document.getElementById('selectAll').addEventListener('change', function (e) {
    var checkboxes = document.querySelectorAll('.row-checkbox');
    checkboxes.forEach(function (cb) {
        cb.checked = e.target.checked;
        var tr = cb.closest('tr');
        var id = tr.getAttribute('data-id');
        if (e.target.checked) {
            selectedIds.add(id);
            tr.classList.add('selected');
        } else {
            selectedIds.delete(id);
            tr.classList.remove('selected');
        }
    });
    updateBulkBar();
});

// Bulk bar buttons
document.getElementById('bulkDelete').addEventListener('click', bulkDelete);
document.getElementById('bulkClear').addEventListener('click', clearSelection);

// Sortable column headers
document.querySelectorAll('#messageTable thead th.sortable').forEach(function (th) {
    th.addEventListener('click', function () {
        setSort(th.getAttribute('data-sort'));
    });
});
```

**Step 7: Add `data-id` attribute to rows**

In the `renderInbox` function, right after `var tr = document.createElement('tr');`, add:

```javascript
tr.setAttribute('data-id', msg.id);
```

**Step 8: Reset selection on page/search/tag change**

In the `loadMessages` function, add at the very start (before `var search = ...`):

```javascript
selectedIds.clear();
```

**Step 9: Add SignalR handler for `MessagesDeleted`**

In `setupSignalR()`, after the `MessagesCleared` handler, add:

```javascript
connection.on('MessagesDeleted', function () {
    loadMessages();
});
```

**Step 10: Commit**

```bash
git add src/MailPeek/Assets/js/dashboard.js
git commit -m "feat: add bulk select/delete and sortable column headers to dashboard JS"
```

---

### Task 8: Build, test, and verify

**Step 1: Run all backend tests**

Run: `dotnet test tests/MailPeek.Tests -v minimal`
Expected: All tests PASS (existing + new)

**Step 2: Build the full solution**

Run: `dotnet build --no-incremental 2>&1 | grep -c "warning"`
Expected: 0 warnings

**Step 3: Commit if any fixes were needed**

```bash
git add -A
git commit -m "chore: fix any build warnings"
```

---

### Task 9: Update FEATURES.md

**Files:**
- Modify: `docs/FEATURES.md`

**Step 1: Mark features as done**

Change:
- `- [ ] **Bulk operations**` → `- [x] **Bulk operations**`
- `- [ ] **Sort options**` → `- [x] **Sort options**`

**Step 2: Commit**

```bash
git add docs/FEATURES.md
git commit -m "docs: mark bulk operations and sort options as done"
```
