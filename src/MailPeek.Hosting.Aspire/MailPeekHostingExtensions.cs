using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace MailPeek.Hosting.Aspire;

public static class MailPeekHostingExtensions
{
    public static IResourceBuilder<MailPeekResource> AddMailPeek(
        this IDistributedApplicationBuilder builder,
        string name = "mailpeek",
        int smtpPort = 2525)
    {
        var resource = new MailPeekResource(name, smtpPort);
        return builder.AddResource(resource);
    }
}
