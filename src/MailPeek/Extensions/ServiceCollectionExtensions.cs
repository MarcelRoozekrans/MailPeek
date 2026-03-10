using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MailPeek.Configuration;
using MailPeek.Hubs;
using MailPeek.Services;
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
            opts.AutoTagPlusAddressing = options.AutoTagPlusAddressing;
            opts.WebhookUrl = options.WebhookUrl;
        });

        services.AddSingleton<IMessageStore>(new InMemoryMessageStore(options.MaxMessages));
        services.AddSingleton<MailPeekHubNotifier>();
        services.AddSingleton<AutoTagger>();
        services.AddHttpClient("LinkChecker");
        services.AddSingleton<LinkChecker>();
        services.AddHttpClient("Webhook");
        services.AddSingleton<WebhookNotifier>();
        services.AddHostedService<MailPeekSmtpHostedService>();
        services.AddSignalR();

        return services;
    }

    public static IServiceCollection AddMailPeek(
        this IServiceCollection services,
        string connectionName,
        Action<MailPeekSmtpOptions>? configureOptions = null)
    {
        services.AddSingleton<IConfigureOptions<MailPeekSmtpOptions>>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var connectionString = config.GetConnectionString(connectionName);

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException(
                    $"Connection string '{connectionName}' not found in configuration.",
                    nameof(connectionName));
            }

            var uri = new Uri(connectionString);
            var hostname = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 2525;

            return new ConfigureNamedOptions<MailPeekSmtpOptions>(null, opts =>
            {
                opts.Hostname = hostname;
                opts.Port = port;
                configureOptions?.Invoke(opts);
            });
        });

        services.AddSingleton<IMessageStore>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MailPeekSmtpOptions>>().Value;
            return new InMemoryMessageStore(options.MaxMessages);
        });
        services.AddSingleton<MailPeekHubNotifier>();
        services.AddSingleton<AutoTagger>();
        services.AddHttpClient("LinkChecker");
        services.AddSingleton<LinkChecker>();
        services.AddHttpClient("Webhook");
        services.AddSingleton<WebhookNotifier>();
        services.AddHostedService<MailPeekSmtpHostedService>();
        services.AddSignalR();

        return services;
    }
}
