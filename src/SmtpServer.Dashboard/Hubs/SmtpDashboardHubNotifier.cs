using Microsoft.AspNetCore.SignalR;
using SmtpServer.Dashboard.Models;
using SmtpServer.Dashboard.Storage;

namespace SmtpServer.Dashboard.Hubs;

public class SmtpDashboardHubNotifier(
    IMessageStore store,
    IHubContext<SmtpDashboardHub> hubContext)
{
    public void Start()
    {
        store.OnMessageReceived += OnMessageReceived;
    }

    private void OnMessageReceived(StoredMessage message)
    {
        _ = hubContext.Clients.All.SendAsync("NewMessage", message.ToSummary());
    }

    public Task NotifyMessageDeleted(Guid id) =>
        hubContext.Clients.All.SendAsync("MessageDeleted", id);

    public Task NotifyMessagesCleared() =>
        hubContext.Clients.All.SendAsync("MessagesCleared");
}
