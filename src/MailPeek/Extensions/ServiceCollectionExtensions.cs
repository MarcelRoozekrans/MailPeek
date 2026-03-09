using Microsoft.Extensions.DependencyInjection;
using MailPeek.Configuration;
using MailPeek.Hubs;
using MailPeek.Smtp;
using MailPeek.Storage;

namespace MailPeek.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMailPeek(
        this IServiceCollection services,
        Action<MailPeekSmtpOptions>? configureOptions = null)
    {
        var options = new MailPeekSmtpOptions();
        configureOptions?.Invoke(options);

        services.Configure<MailPeekSmtpOptions>(opts =>
        {
            opts.Port = options.Port;
            opts.Hostname = options.Hostname;
            opts.MaxMessages = options.MaxMessages;
            opts.MaxMessageSize = options.MaxMessageSize;
        });

        services.AddSingleton<IMessageStore>(new InMemoryMessageStore(options.MaxMessages));
        services.AddSingleton<MailPeekHubNotifier>();
        services.AddHostedService<MailPeekSmtpHostedService>();
        services.AddSignalR();

        return services;
    }
}
