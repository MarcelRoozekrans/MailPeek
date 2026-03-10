# Bulk Operations & Sort Options Design

**Goal:** Add checkbox-based bulk delete and clickable column header sorting to the MailPeek inbox.

## Feature 1: Bulk Operations

### UI

- Checkbox column added as first column in inbox table
- Thead checkbox = "select all on current page"
- Floating bottom bar appears when 1+ items selected: shows count, Delete button, Clear selection button
- Delete triggers a confirm dialog before executing
- Selected rows get subtle highlight background
- Selection resets on page/search/tag change

### Backend

- `DELETE /api/messages/bulk` — accepts `{ ids: ["guid1", ...] }`, returns `{ deleted: N }`
- `IMessageStore.DeleteMany(IReadOnlyList<Guid> ids)` — removes from dictionary + linked list, returns count
- SignalR `MessagesDeleted` event with deleted IDs for cross-tab sync

### CSS

- Checkbox column: ~40px fixed width
- `.bulk-bar`: fixed bottom, dark background, white text, slide-up animation
- `.selected` row class: subtle blue highlight

## Feature 2: Sort Options

### UI

- Clickable column headers (From, Subject, Date) toggle sorting
- Arrow indicator (▲/▼) on active sort column
- Default: date descending (newest first)
- Click same column = toggle direction; click different column = set it descending
- Sort resets to default on search/tag change

### Backend

- Extend `GetPage()`: add `string? sortBy = null, bool sortDescending = true`
- Supported fields: `date` (ReceivedAt), `from` (From), `subject` (Subject)
- API gets `sortBy` and `sortDesc` query params

### CSS

- Sortable headers: `cursor: pointer`, hover effect
- Active sort header: arrow via `::after` pseudo-element

## Shared Concerns

- Both features modify `loadMessages()` in dashboard.js
- Both need mobile-responsive handling
- Checkbox column hidden on mobile (use floating action instead or keep compact)
- Sort indicators should be subtle and not break mobile layout
