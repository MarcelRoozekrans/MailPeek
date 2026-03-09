# Fake SMTP Server with Dashboard — Design Document

## Overview

A NuGet package (`SmtpServer.Dashboard`) that provides an in-memory fake SMTP server with a real-time web dashboard, similar in DX to Hangfire. Consumers add it to their ASP.NET Core app with two lines of code.

## Target Frameworks

- .NET 8 (LTS) + .NET 9 (latest), multi-target

## Dependencies

- `SmtpServer` — SMTP protocol listener
- `MimeKit` — Full MIME message parsing (headers, CC/BCC, multiple recipients, attachments, embedded images)
- `Microsoft.AspNetCore.SignalR` — Real-time dashboard updates

## Project Structure

```
src/
  SmtpServer.Dashboard/              → Main NuGet library
tests/
  SmtpServer.Dashboard.Tests/        → Unit + integration tests
samples/
  SampleApp/                         → Example ASP.NET Core app
```

## Consumer API

```csharp
// Service registration
builder.Services.AddFakeSmtp(options =>
{
    options.Port = 2525;
    options.MaxMessages = 1000;
});

// Middleware (dashboard + API)
app.UseFakeSmtpDashboard(options =>
{
    options.PathPrefix = "/smtp";
    options.Authorization = new[] { new MyAuthFilter() };
});
```

## Architecture

```
┌──────────────────────────────────────────────────┐
│  Consumer's ASP.NET Core App                      │
│                                                   │
│  ┌──────────────┐   ┌──────────────────────────┐ │
│  │ SMTP Listener │   │ Dashboard Middleware      │ │
│  │ (IHostedSvc)  │   │ GET /smtp/*              │ │
│  │ SmtpServer lib│   │ Embedded static assets   │ │
│  │ + MimeKit     │   │ REST API + SignalR hub   │ │
│  └──────┬───────┘   └───────────┬──────────────┘ │
│         │                       │                  │
│         ▼                       ▼                  │
│  ┌──────────────────────────────────────────────┐ │
│  │         IMessageStore (singleton)             │ │
│  │  ConcurrentDictionary<Guid, StoredMessage>    │ │
│  │  Add / Delete / Clear / GetAll                │ │
│  │  Max capacity + FIFO eviction                 │ │
│  │  Event: OnMessageReceived                     │ │
│  └──────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────┘
```

### Data Flow

1. Email arrives on SMTP port → `SmtpServer` handles protocol → passes raw message to our handler
2. MimeKit parses into `StoredMessage` (from, to, cc, bcc, subject, text body, html body, attachments, headers, date)
3. `IMessageStore` stores it, fires `OnMessageReceived`
4. SignalR hub pushes `NewMessage` notification to connected dashboard clients
5. Dashboard JS fetches full detail via REST API on demand

## Key Interfaces

### IMessageStore

Abstraction over message storage. Default: in-memory. Consumers could implement persistent storage.

Also injectable in tests for assertions:
```csharp
var store = services.GetRequiredService<IMessageStore>();
Assert.Single(store.GetAll(), m => m.To.Contains("user@test.com"));
```

### ISmtpDashboardAuthorizationFilter

```csharp
bool Authorize(DashboardContext context);
```

Extensible auth — works with OAuth, Azure AD, cookie auth, etc. via ASP.NET Core's authentication middleware. The filter inspects `HttpContext.User`.

## Configuration

### FakeSmtpOptions

| Property | Default | Description |
|----------|---------|-------------|
| Port | 2525 | SMTP listen port |
| Hostname | "localhost" | SMTP hostname |
| MaxMessages | 1000 | FIFO eviction when exceeded |
| MaxMessageSize | 10,000,000 | Max message size in bytes (10MB) |

### FakeDashboardOptions

| Property | Default | Description |
|----------|---------|-------------|
| PathPrefix | "/smtp" | Dashboard URL prefix (configurable) |
| Authorization | empty | Array of `ISmtpDashboardAuthorizationFilter` |
| Title | "Fake SMTP Dashboard" | Customizable page title |

## Dashboard

### Pages

| Route | Description |
|-------|-------------|
| `/smtp` | Inbox — message list (from, to, subject, date, attachment icon), newest first |
| `/smtp/message/{id}` | Detail — tabs for HTML preview (sandboxed iframe), plain text, headers, attachments |

### REST API

| Method | Path | Description |
|--------|------|-------------|
| GET | `/smtp/api/messages` | List messages (paged, search/filter) |
| GET | `/smtp/api/messages/{id}` | Single message detail |
| GET | `/smtp/api/messages/{id}/html` | HTML body for iframe rendering |
| GET | `/smtp/api/messages/{id}/attachments/{index}` | Download attachment |
| DELETE | `/smtp/api/messages/{id}` | Delete single message |
| DELETE | `/smtp/api/messages` | Clear all messages |

### SignalR Hub (`/smtp/hub`)

Server → Client events:
- `NewMessage(summary)` — new email received
- `MessageDeleted(id)` — single message deleted
- `MessagesCleared` — all messages cleared

### UI

- Embedded static HTML/JS/CSS as assembly resources (no frontend build toolchain)
- Clean minimal design, respects `prefers-color-scheme` (dark/light)
- Table-based inbox with clickable rows

## Error Handling

- **SMTP listener failures**: Log and continue, never crash the host app
- **Malformed messages**: Store raw data with parse-error flag, still visible in dashboard
- **Store full**: FIFO eviction (oldest removed first)
- **Dashboard auth failure**: Return 401/403

## Lifetime

- SMTP listener runs as `IHostedService`, stops gracefully with the app
- `IMessageStore` registered as singleton
