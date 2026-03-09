using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MailPeek.Configuration;
using MailPeek.Extensions;
using MailPeek.Hubs;
using MailPeek.Storage;

namespace MailPeek.Tests.Extensions;

public class ServiceRegistrationTests
{
    [Fact]
    public void AddMailPeek_RegistersRequiredServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMailPeek(opts =>
        {
            opts.Port = 3000;
            opts.MaxMessages = 500;
        });

        var provider = services.BuildServiceProvider();

        var store = provider.GetService<IMessageStore>();
        Assert.NotNull(store);

        var hostedServices = provider.GetServices<IHostedService>();
        Assert.Contains(hostedServices, s => string.Equals(s.GetType().Name, "MailPeekSmtpHostedService", StringComparison.Ordinal));

        var notifier = provider.GetService<MailPeekHubNotifier>();
        Assert.NotNull(notifier);
    }

    [Fact]
    public void AddMailPeek_ConfiguresOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMailPeek(opts =>
        {
            opts.Port = 3000;
            opts.MaxMessages = 500;
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MailPeekSmtpOptions>>();

        Assert.Equal(3000, options.Value.Port);
        Assert.Equal(500, options.Value.MaxMessages);
    }
}
