namespace SmtpServer.Dashboard.Authorization;

public interface ISmtpDashboardAuthorizationFilter
{
    bool Authorize(DashboardContext context);
}
