using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MailPeek.Extensions;
using MailPeek.Storage;

namespace MailPeek.Tests.Integration;

public class EndToEndTests : IAsyncLifetime
{
    private IHost? _host;
    private const int SmtpPort = 12525; // Use high port to avoid conflicts

    public async Task InitializeAsync()
    {
        _host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddMailPeek(opts => opts.Port = SmtpPort);
                });
                webBuilder.Configure(app =>
                {
                    app.UseMailPeek();
                });
            })
            .StartAsync();

        // Give SMTP server time to start
        await Task.Delay(1000);
    }

    public async Task DisposeAsync()
    {
        if (_host is not null) await _host.StopAsync();
        _host?.Dispose();
    }

    [Fact]
    public async Task SendEmail_AppearsInStore()
    {
        // Send via MailKit SMTP client
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Test", "test@example.com"));
        message.To.Add(new MailboxAddress("Recipient", "recipient@example.com"));
        message.Subject = "E2E Test";
        message.Body = new TextPart("plain") { Text = "Hello from E2E test" };

        using var smtpClient = new MailKit.Net.Smtp.SmtpClient();
        await smtpClient.ConnectAsync("localhost", SmtpPort, false);
        await smtpClient.SendAsync(message);
        await smtpClient.DisconnectAsync(true);

        // Small delay for processing
        await Task.Delay(500);

        // Verify via IMessageStore
        var store = _host!.Services.GetRequiredService<IMessageStore>();
        var messages = store.GetAll();

#pragma warning disable HLQ005
        Assert.Single(messages);
#pragma warning restore HLQ005
        Assert.Equal("test@example.com", messages[0].From);
        Assert.Equal("E2E Test", messages[0].Subject);
        Assert.Equal("Hello from E2E test", messages[0].TextBody);
    }
}
