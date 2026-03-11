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
    public async Task SetTags_ReturnsOk()
    {
        var msg = new StoredMessage
        {
            From = "test@test.com",
            To = ["r@test.com"],
            Subject = "Test"
        };
        _store.Add(msg);

        var response = await _client!.PutAsJsonAsync($"/mailpeek/api/messages/{msg.Id}/tags", new[] { "welcome", "test" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["welcome", "test"], _store.GetById(msg.Id)!.Tags);
    }

    [Fact]
    public async Task SetTags_ReturnsNotFoundForMissing()
    {
        var response = await _client!.PutAsJsonAsync($"/mailpeek/api/messages/{Guid.NewGuid()}/tags", new[] { "tag" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMessages_FiltersByTag()
    {
        var msg1 = new StoredMessage { From = "a@t.com", To = ["b@t.com"], Subject = "Tagged" };
        var msg2 = new StoredMessage { From = "c@t.com", To = ["d@t.com"], Subject = "Untagged" };
        _store.Add(msg1);
        _store.Add(msg2);
        _store.SetTags(msg1.Id, ["welcome"]);

        var response = await _client!.GetAsync("/mailpeek/api/messages?tag=welcome");
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, result.GetProperty("totalCount").GetInt32());
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

    [Fact]
    public async Task GetLinks_Returns202WhenChecking()
    {
        var msg = new StoredMessage { From = "a@b.com", To = ["c@d.com"], Subject = "Test" };
        _store.Add(msg);
        var response = await _client!.GetAsync($"/mailpeek/api/messages/{msg.Id}/links");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task GetLinks_ReturnsNotFoundForMissing()
    {
        var response = await _client!.GetAsync($"/mailpeek/api/messages/{Guid.NewGuid()}/links");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task BulkDelete_RemovesSelectedMessages()
    {
        var msg1 = new StoredMessage { From = "a@t.com", To = ["b@t.com"], Subject = "1" };
        var msg2 = new StoredMessage { From = "c@t.com", To = ["d@t.com"], Subject = "2" };
        var msg3 = new StoredMessage { From = "e@t.com", To = ["f@t.com"], Subject = "3" };
        _store.Add(msg1);
        _store.Add(msg2);
        _store.Add(msg3);

        var request = new HttpRequestMessage(HttpMethod.Delete, "/mailpeek/api/messages/bulk")
        {
            Content = JsonContent.Create(new { ids = new[] { msg1.Id, msg3.Id } })
        };
        var response = await _client!.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, result.GetProperty("deleted").GetInt32());
#pragma warning disable HLQ005
        Assert.Single(_store.GetAll());
#pragma warning restore HLQ005
    }

    [Fact]
    public async Task BulkDelete_Returns400ForMissingBody()
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, "/mailpeek/api/messages/bulk");
        var response = await _client!.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetMessages_SortsByFromAscending()
    {
        _store.Add(new StoredMessage { From = "charlie@t.com", To = ["r@t.com"], Subject = "C" });
        _store.Add(new StoredMessage { From = "alice@t.com", To = ["r@t.com"], Subject = "A" });
        _store.Add(new StoredMessage { From = "bob@t.com", To = ["r@t.com"], Subject = "B" });

        var response = await _client!.GetAsync("/mailpeek/api/messages?sortBy=from&sortDesc=false");
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = result.GetProperty("items");
        Assert.Equal("alice@t.com", items[0].GetProperty("from").GetString());
        Assert.Equal("bob@t.com", items[1].GetProperty("from").GetString());
        Assert.Equal("charlie@t.com", items[2].GetProperty("from").GetString());
    }

    [Fact]
    public async Task GetLinks_Returns200WhenComplete()
    {
        var msg = new StoredMessage
        {
            From = "a@b.com", To = ["c@d.com"], Subject = "Test",
            LinkCheckComplete = true,
            LinkCheckResults = [new LinkCheckResult { Url = "https://example.com", Status = LinkStatus.Ok, StatusCode = 200 }]
        };
        _store.Add(msg);
        var response = await _client!.GetAsync($"/mailpeek/api/messages/{msg.Id}/links");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
