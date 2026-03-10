using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace MailPeek.Hosting.Aspire;

public class MailPeekResource(string name, int smtpPort)
    : Resource(name), IResourceWithConnectionString
{
    public int SmtpPort { get; } = smtpPort;

    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"smtp://localhost:{SmtpPort.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
}
