using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmtpServer.Dashboard.Configuration;
using SmtpServer.Dashboard.Extensions;
using SmtpServer.Dashboard.Hubs;
using SmtpServer.Dashboard.Storage;

namespace SmtpServer.Dashboard.Tests.Extensions;

public class ServiceRegistrationTests
{
    [Fact]
    public void AddFakeSmtp_RegistersRequiredServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddFakeSmtp(opts =>
        {
            opts.Port = 3000;
            opts.MaxMessages = 500;
        });

        var provider = services.BuildServiceProvider();

        var store = provider.GetService<IMessageStore>();
        Assert.NotNull(store);

        var hostedServices = provider.GetServices<IHostedService>();
        Assert.Contains(hostedServices, s => s.GetType().Name == "FakeSmtpHostedService");

        var notifier = provider.GetService<SmtpDashboardHubNotifier>();
        Assert.NotNull(notifier);
    }

    [Fact]
    public void AddFakeSmtp_ConfiguresOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddFakeSmtp(opts =>
        {
            opts.Port = 3000;
            opts.MaxMessages = 500;
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<FakeSmtpOptions>>();

        Assert.Equal(3000, options.Value.Port);
        Assert.Equal(500, options.Value.MaxMessages);
    }
}
