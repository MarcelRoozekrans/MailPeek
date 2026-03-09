using MailPeek.Authorization;

namespace MailPeek.Configuration;

public class MailPeekDashboardOptions
{
    public string PathPrefix { get; set; } = "/mailpeek";
    public IMailPeekAuthorizationFilter[] Authorization { get; set; } = [];
    public string Title { get; set; } = "MailPeek";
}
