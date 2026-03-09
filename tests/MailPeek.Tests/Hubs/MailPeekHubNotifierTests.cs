using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using MailPeek.Hubs;
using MailPeek.Models;
using MailPeek.Storage;

namespace MailPeek.Tests.Hubs;

public class MailPeekHubNotifierTests
{
    [Fact]
    public async Task WhenMessageAdded_NotifiesClients()
    {
        var store = new InMemoryMessageStore();
        var hubContext = Substitute.For<IHubContext<MailPeekHub>>();
        var clients = Substitute.For<IHubClients>();
        var clientProxy = Substitute.For<IClientProxy>();
        hubContext.Clients.Returns(clients);
        clients.All.Returns(clientProxy);

        var notifier = new MailPeekHubNotifier(store, hubContext);
        notifier.Start();

        var msg = new StoredMessage
        {
            From = "test@test.com",
            To = ["r@test.com"],
            Subject = "Test"
        };
        store.Add(msg);

        await clientProxy.Received(1).SendCoreAsync(
            "NewMessage",
            Arg.Any<object?[]>(),
            Arg.Any<CancellationToken>());
    }
}
