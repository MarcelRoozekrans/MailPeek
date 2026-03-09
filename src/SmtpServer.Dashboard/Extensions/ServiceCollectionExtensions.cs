using Microsoft.Extensions.DependencyInjection;
using SmtpServer.Dashboard.Configuration;
using SmtpServer.Dashboard.Hubs;
using SmtpServer.Dashboard.Smtp;
using SmtpServer.Dashboard.Storage;

namespace SmtpServer.Dashboard.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFakeSmtp(
        this IServiceCollection services,
        Action<FakeSmtpOptions>? configureOptions = null)
    {
        var options = new FakeSmtpOptions();
        configureOptions?.Invoke(options);

        services.Configure<FakeSmtpOptions>(opts =>
        {
            opts.Port = options.Port;
            opts.Hostname = options.Hostname;
            opts.MaxMessages = options.MaxMessages;
            opts.MaxMessageSize = options.MaxMessageSize;
        });

        services.AddSingleton<IMessageStore>(new InMemoryMessageStore(options.MaxMessages));
        services.AddSingleton<SmtpDashboardHubNotifier>();
        services.AddHostedService<FakeSmtpHostedService>();
        services.AddSignalR();

        return services;
    }
}
