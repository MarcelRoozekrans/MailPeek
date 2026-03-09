using SmtpServer.Dashboard.Authorization;

namespace SmtpServer.Dashboard.Configuration;

public class FakeDashboardOptions
{
    public string PathPrefix { get; set; } = "/smtp";
    public ISmtpDashboardAuthorizationFilter[] Authorization { get; set; } = [];
    public string Title { get; set; } = "Fake SMTP Dashboard";
}
