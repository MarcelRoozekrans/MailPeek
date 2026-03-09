using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using SmtpServer.Dashboard.Hubs;
using SmtpServer.Dashboard.Models;
using SmtpServer.Dashboard.Storage;

namespace SmtpServer.Dashboard.Tests.Hubs;

public class SmtpDashboardHubNotifierTests
{
    [Fact]
    public void WhenMessageAdded_NotifiesClients()
    {
        var store = new InMemoryMessageStore();
        var hubContext = Substitute.For<IHubContext<SmtpDashboardHub>>();
        var clients = Substitute.For<IHubClients>();
        var clientProxy = Substitute.For<IClientProxy>();
        hubContext.Clients.Returns(clients);
        clients.All.Returns(clientProxy);

        var notifier = new SmtpDashboardHubNotifier(store, hubContext);
        notifier.Start();

        var msg = new StoredMessage
        {
            From = "test@test.com",
            To = ["r@test.com"],
            Subject = "Test"
        };
        store.Add(msg);

        clientProxy.Received(1).SendCoreAsync(
            "NewMessage",
            Arg.Any<object?[]>(),
            Arg.Any<CancellationToken>());
    }
}
