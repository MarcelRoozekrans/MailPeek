using System.Text.Json;
using MailPeek.Configuration;
using MailPeek.Models;
using MailPeek.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MailPeek.Services;

public class WebhookNotifier(
    IMessageStore store,
    IHttpClientFactory httpClientFactory,
    IOptions<MailPeekSmtpOptions> options,
    ILogger<WebhookNotifier> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void Start()
    {
        if (!string.IsNullOrEmpty(options.Value.WebhookUrl))
            store.OnMessageReceived += OnMessageReceived;
    }

    private void OnMessageReceived(StoredMessage message)
    {
        _ = Task.Run(() => SendWebhookAsync(message));
    }

    private async Task SendWebhookAsync(StoredMessage message)
    {
        try
        {
            using var client = httpClientFactory.CreateClient("Webhook");
            client.Timeout = TimeSpan.FromSeconds(5);

            var payload = new
            {
                id = message.Id,
                from = message.From,
                to = message.To,
                subject = message.Subject,
                receivedAt = message.ReceivedAt
            };

            var json = JsonSerializer.Serialize(payload, JsonOptions);
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            await client.PostAsync(new Uri(options.Value.WebhookUrl!), content).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogError(ex, "Failed to send webhook for message {MessageId}", message.Id);
        }
    }
}
