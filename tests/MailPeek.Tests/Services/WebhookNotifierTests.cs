using System.Net;
using MailPeek.Configuration;
using MailPeek.Models;
using MailPeek.Services;
using MailPeek.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace MailPeek.Tests.Services;

public class WebhookNotifierTests
{
    [Fact]
    public async Task OnMessage_PostsToWebhookUrl()
    {
        var handler = new TestHandler(HttpStatusCode.OK);
        var httpClient = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("Webhook").Returns(httpClient);

        var options = Options.Create(new MailPeekSmtpOptions { WebhookUrl = "https://example.com/hook" });
        var store = new InMemoryMessageStore();
        var logger = NullLogger<WebhookNotifier>.Instance;
        var notifier = new WebhookNotifier(store, factory, options, logger);
        notifier.Start();

        var msg = new StoredMessage
        {
            From = "sender@test.com",
            To = ["recipient@test.com"],
            Subject = "Hello"
        };
        store.Add(msg);

        await Task.Delay(500).ConfigureAwait(true);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("https://example.com/hook", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public void Start_DoesNothingWhenNoWebhookUrl()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        var options = Options.Create(new MailPeekSmtpOptions { WebhookUrl = null });
        var store = new InMemoryMessageStore();
        var logger = NullLogger<WebhookNotifier>.Instance;
        var notifier = new WebhookNotifier(store, factory, options, logger);
        notifier.Start();

        store.Add(new StoredMessage { From = "a@b.com", To = ["c@d.com"], Subject = "Test" });
        // No exception = webhook not subscribed
    }

    private sealed class TestHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }
}
