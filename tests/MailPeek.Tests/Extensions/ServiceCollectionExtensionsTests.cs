using MailPeek.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MailPeek.Configuration;

namespace MailPeek.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMailPeek_WithConnectionName_ConfiguresFromConnectionString()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ConnectionStrings:mailpeek"] = "smtp://myhost:3025"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddMailPeek("mailpeek");

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MailPeekSmtpOptions>>().Value;

        Assert.Equal("myhost", options.Hostname);
        Assert.Equal(3025, options.Port);
    }

    [Fact]
    public void AddMailPeek_WithConnectionName_DefaultsPort2525WhenNoPortInUri()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ConnectionStrings:mailpeek"] = "smtp://localhost"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddMailPeek("mailpeek");

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MailPeekSmtpOptions>>().Value;

        Assert.Equal("localhost", options.Hostname);
        Assert.Equal(2525, options.Port);
    }

    [Fact]
    public void AddMailPeek_WithMissingConnectionString_ThrowsArgumentException()
    {
        var config = new ConfigurationBuilder().Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddMailPeek("nonexistent");

        var provider = services.BuildServiceProvider();
        Assert.Throws<ArgumentException>(() =>
            provider.GetRequiredService<IOptions<MailPeekSmtpOptions>>().Value);
    }
}
