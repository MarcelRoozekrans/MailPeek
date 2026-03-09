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
}
