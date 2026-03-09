using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MailPeek.Configuration;
using SmtpLib = global::SmtpServer;

namespace MailPeek.Smtp;

public class MailPeekSmtpHostedService(
    IOptions<MailPeekSmtpOptions> options,
    Storage.IMessageStore messageStore,
    ILogger<MailPeekSmtpHostedService> logger) : IHostedService, IDisposable
{
    private SmtpLib.SmtpServer? _smtpServer;
    private CancellationTokenSource? _cts;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var opts = options.Value;
        logger.LogInformation("Starting fake SMTP server on {Hostname}:{Port}", opts.Hostname, opts.Port);

        var serverOptions = new SmtpLib.SmtpServerOptionsBuilder()
            .ServerName(opts.Hostname)
            .Port(opts.Port)
            .MaxMessageSize((int)opts.MaxMessageSize)
            .Build();

        var serviceProvider = new SmtpLib.ComponentModel.ServiceProvider();
        serviceProvider.Add(new MailPeekSmtpMessageStore(messageStore) as SmtpLib.Storage.IMessageStore);

        _smtpServer = new SmtpLib.SmtpServer(serverOptions, serviceProvider);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _ = Task.Run(async () =>
        {
            try
            {
                await _smtpServer.StartAsync(_cts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                logger.LogError(ex, "SMTP server error");
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping fake SMTP server");
        _cts?.Cancel();
        _smtpServer?.Shutdown();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _cts?.Dispose();
    }
}
