using Microsoft.AspNetCore.SignalR;

namespace SmtpServer.Dashboard.Hubs;

public class SmtpDashboardHub : Hub
{
    // Client methods: NewMessage, MessageDeleted, MessagesCleared
    // No server-callable methods needed — dashboard is read-only via SignalR
}
