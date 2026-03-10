using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MailPeek.Models;
using MailPeek.Storage;
using MailPeek.Middleware;

namespace MailPeek.Tests.Middleware;

public class DashboardApiMiddlewareTests : IAsyncLifetime
{
    private IHost? _host;
    private HttpClient? _client;
    private InMemoryMessageStore _store = new();

    public async Task InitializeAsync()
    {
        _store = new InMemoryMessageStore();
        _host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddSingleton<IMessageStore>(_store);
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapMailPeekApi("/mailpeek");
                    });
                });
            })
            .StartAsync();

        _client = _host.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_host is not null) await _host.StopAsync();
        _host?.Dispose();
    }

    [Fact]
    public async Task GetMessages_ReturnsEmptyList()
    {
        var response = await _client!.GetAsync("/mailpeek/api/messages");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, result.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task GetMessages_ReturnsStoredMessages()
    {
        _store.Add(new StoredMessage
        {
            From = "test@test.com",
            To = ["r@test.com"],
            Subject = "Hello"
        });

        var response = await _client!.GetAsync("/mailpeek/api/messages");
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, result.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task GetMessageById_ReturnsMessage()
    {
        var msg = new StoredMessage
        {
            From = "test@test.com",
            To = ["r@test.com"],
            Subject = "Test"
        };
        _store.Add(msg);

        var response = await _client!.GetAsync($"/mailpeek/api/messages/{msg.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMessageById_Returns404ForMissing()
    {
        var response = await _client!.GetAsync($"/mailpeek/api/messages/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMessage_RemovesMessage()
    {
        var msg = new StoredMessage
        {
            From = "test@test.com",
            To = ["r@test.com"],
            Subject = "Test"
        };
        _store.Add(msg);

        var response = await _client!.DeleteAsync($"/mailpeek/api/messages/{msg.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_store.GetAll());
    }

    [Fact]
    public async Task MarkAsRead_ReturnsOk()
    {
        var msg = new StoredMessage
        {
            From = "test@test.com",
            To = ["r@test.com"],
            Subject = "Test"
        };
        _store.Add(msg);

        var response = await _client!.PutAsync($"/mailpeek/api/messages/{msg.Id}/read", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(_store.GetById(msg.Id)!.IsRead);
    }

    [Fact]
    public async Task MarkAsRead_ReturnsNotFoundForMissing()
    {
        var response = await _client!.PutAsync($"/mailpeek/api/messages/{Guid.NewGuid()}/read", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAllMessages_ClearsStore()
    {
        _store.Add(new StoredMessage { From = "a@t.com", To = ["b@t.com"], Subject = "1" });
        _store.Add(new StoredMessage { From = "c@t.com", To = ["d@t.com"], Subject = "2" });

        var response = await _client!.DeleteAsync("/mailpeek/api/messages");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_store.GetAll());
    }
}
