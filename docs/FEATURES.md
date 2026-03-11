# MailPeek Feature Backlog

Feature candidates for future releases, inspired by Mailpit and developer feedback.
Pick from this list when planning the next sprint.

## Legend

| Priority | Meaning |
|----------|---------|
| P1 | High value, low effort — do first |
| P2 | High value, medium effort |
| P3 | Nice to have |

---

## Dashboard UX

- [x] **Read/unread status** — Visual indicator for unseen messages, bold row styling (P1)
- [x] **Browser notifications** — Desktop notification when new email arrives via SignalR (P1)
- [x] **Bulk operations** — Checkbox selection, bulk delete (P1)
- [x] **Sort options** — Sort by date, sender, subject (ascending/descending) (P1)
- [ ] **Message preview snippet** — Show first ~100 chars of body in inbox row (P2)
- [ ] **Keyboard shortcuts** — j/k navigate, Enter open, Delete remove, Esc back (P2)
- [ ] **Dark/light theme toggle** — Manual override in addition to OS prefers-color-scheme (P3)
- [ ] **Column customization** — Show/hide columns in inbox table (P3)

## Email Analysis & Testing

- [x] **Link checking** — Validate all URLs in HTML/text body, report broken links (P1)
- [ ] **HTML compatibility scoring** — Score email HTML against common client rendering rules (P2)
- [ ] **SpamAssassin integration** — Spam score analysis with fix suggestions (P2)
- [ ] **List-Unsubscribe validation** — Check unsubscribe header syntax (P3)
- [ ] **Screenshot generation** — Render HTML email to image for visual inspection (P3)
- [ ] **Accessibility check** — Validate email HTML for screen reader compatibility (P3)

## Message Management

- [x] **Message tagging** — Manual tags + auto-tagging by sender, subject pattern, plus-addressing (P1)
- [ ] **Advanced search** — Search in body, CC/BCC, headers; field-specific filters (P2)
- [ ] **Date range filtering** — Filter messages by received date range (P2)
- [ ] **Starred/flagged messages** — Pin important messages to top (P3)
- [ ] **Message export** — Export single or multiple messages as .eml files (P3)

## Integration & Automation

- [x] **Webhook support** — POST to configurable URL when email arrives (P1)
- [ ] **SMTP relay/forwarding** — Forward captured email to a real SMTP server on demand (P2)
- [ ] **SMTP chaos mode** — Return configurable random SMTP errors for resilience testing (P2)
- [ ] **GraphQL API** — Alternative to REST for flexible querying (P3)
- [ ] **OpenTelemetry traces** — Emit traces for received/processed emails (P3)

## SMTP Server

- [ ] **STARTTLS support** — TLS encryption for SMTP connections (P2)
- [ ] **SMTP authentication** — Accept-any auth mode for apps that require SMTP auth (P2)
- [ ] **Rate limiting** — Configurable max messages per second (P3)
- [ ] **Max recipients per message** — Configurable limit (P3)

## Additional Protocols

- [ ] **POP3 server** — Let email clients (Outlook, Thunderbird) connect and read captured mail (P3)

## Storage

- [ ] **Persistent storage option** — SQLite or LiteDB backend for messages that survive restarts (P2)
- [ ] **Configurable retention** — Auto-delete messages older than N hours/days (P2)
- [ ] **Storage metrics** — Dashboard widget showing message count, storage size, oldest message (P3)
