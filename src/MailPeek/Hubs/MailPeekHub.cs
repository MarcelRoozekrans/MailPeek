using Microsoft.AspNetCore.SignalR;

namespace MailPeek.Hubs;

public class MailPeekHub : Hub
{
    // Client methods: NewMessage, MessageDeleted, MessagesCleared
    // No server-callable methods needed — dashboard is read-only via SignalR
}
