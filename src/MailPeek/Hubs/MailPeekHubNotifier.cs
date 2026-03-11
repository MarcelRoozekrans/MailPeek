using Microsoft.AspNetCore.SignalR;
using MailPeek.Models;
using MailPeek.Storage;

namespace MailPeek.Hubs;

public class MailPeekHubNotifier(
    IMessageStore store,
    IHubContext<MailPeekHub> hubContext)
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

    public Task NotifyMessagesDeleted(IReadOnlyList<Guid> ids) =>
        hubContext.Clients.All.SendAsync("MessagesDeleted", ids);

    public Task NotifyMessagesCleared() =>
        hubContext.Clients.All.SendAsync("MessagesCleared");

    public Task NotifyLinkCheckComplete(Guid id) =>
        hubContext.Clients.All.SendAsync("LinkCheckComplete", id);
}
