using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MailPeek.Extensions;
using Xunit;

namespace MailPeek.Tests.Middleware;

public class DashboardStaticFilesMiddlewareTests
{
    [Fact]
    public async Task Index_html_contains_favicon_link()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services => services.AddMailPeek());
                webBuilder.Configure(app => app.UseMailPeek());
            })
            .StartAsync();

        var client = host.GetTestServer().CreateClient();
        var response = await client.GetAsync("/mailpeek");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("rel=\"icon\"", html);
    }

    [Fact]
    public async Task Index_html_contains_message_count_badge()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services => services.AddMailPeek());
                webBuilder.Configure(app => app.UseMailPeek());
            })
            .StartAsync();

        var client = host.GetTestServer().CreateClient();
        var response = await client.GetAsync("/mailpeek");

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("id=\"messageCount\"", html);
    }

    [Fact]
    public async Task Index_html_contains_delete_column_header()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services => services.AddMailPeek());
                webBuilder.Configure(app => app.UseMailPeek());
            })
            .StartAsync();

        var client = host.GetTestServer().CreateClient();
        var response = await client.GetAsync("/mailpeek");

        var html = await response.Content.ReadAsStringAsync();
        // The 5th column header should no longer be empty
        Assert.DoesNotContain("<th></th>", html);
    }

    [Fact]
    public async Task Css_contains_empty_placeholder_class()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services => services.AddMailPeek());
                webBuilder.Configure(app => app.UseMailPeek());
            })
            .StartAsync();

        var client = host.GetTestServer().CreateClient();
        var response = await client.GetAsync("/mailpeek/assets/css/dashboard.css");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var css = await response.Content.ReadAsStringAsync();
        Assert.Contains(".empty-placeholder", css);
    }
}
