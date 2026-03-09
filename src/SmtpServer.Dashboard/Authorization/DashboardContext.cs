using Microsoft.AspNetCore.Http;

namespace SmtpServer.Dashboard.Authorization;

public class DashboardContext(HttpContext httpContext)
{
    public HttpContext HttpContext { get; } = httpContext;
}
