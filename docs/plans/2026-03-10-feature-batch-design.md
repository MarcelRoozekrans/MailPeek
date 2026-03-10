# Feature Batch Design: Read/Unread, Link Checking, Tagging, Webhooks

## Overview

Four new features for MailPeek, adding email analysis, organization, and integration capabilities.

## Feature 1: Read/Unread Status + Browser Notifications

**Model:** Add `bool IsRead` to `StoredMessage` (default false).

**Store:** `IMessageStore` gets `MarkAsRead(Guid id)`. `MessageSummary` includes `IsRead`. Header badge shows unread vs total count.

**API:** `PUT /api/messages/{id}/read` marks a message as read.

**UI:** Unread rows get bold styling + blue dot. Opening a message auto-marks as read. Badge shows "3 / 42" format.

**Browser notifications:** Web Notifications API. Request permission on dashboard load. On SignalR `NewMessage`, show desktop notification with sender + subject. Click navigates to message.

## Feature 2: Link Checking

**Backend:** `LinkChecker` service subscribes to `IMessageStore.OnMessageReceived`. On background thread: extracts URLs from HTML (`href` attributes) and text body, sends HEAD requests (5s timeout), stores results on the message.

**Model:** `LinkCheckResult { string Url, int? StatusCode, LinkStatus Status }` where `LinkStatus = Ok | Broken | Timeout | Error`. Stored as `List<LinkCheckResult>` on `StoredMessage`.

**API:** `GET /api/messages/{id}/links` returns results. Returns 202 with `{ "status": "checking" }` if still in progress.

**UI:** New "Links" tab in message detail. Table of URLs with green/red status. Badge on tab showing broken count.

**SignalR:** `LinkCheckComplete` event with message ID when done.

## Feature 3: Message Tagging

**Model:** Add `List<string> Tags` to `StoredMessage`.

**Auto-tagging:** Plus-addressing extraction (e.g., `test+signup@example.com` -> tag "signup"). Controlled by `options.AutoTagPlusAddressing` (default true).

**Store:** `IMessageStore` gets `SetTags(Guid id, List<string> tags)`. `GetPage` extended with optional `tag` filter parameter.

**API:**
- `PUT /api/messages/{id}/tags` — set tags (body: `["tag1", "tag2"]`)
- `GET /api/messages?tag=welcome` — filter by tag

**UI:** Colored pills in inbox rows and message detail. Clickable to filter. "+" button to add tags. Colors auto-assigned by tag name hash.

## Feature 4: Webhook Support

**Config:** `MailPeekSmtpOptions.WebhookUrl` (string?, default null).

**Backend:** `WebhookNotifier` singleton subscribes to `IMessageStore.OnMessageReceived`. POSTs JSON to configured URL on background thread. Uses `IHttpClientFactory`. Fire-and-forget with error logging, 5s timeout, no retries.

**Payload:**
```json
{
  "id": "guid",
  "from": "sender@example.com",
  "to": ["recipient@example.com"],
  "subject": "Hello",
  "receivedAt": "2026-03-10T14:00:00Z"
}
```
