using Microsoft.AspNetCore.Http;

namespace MailPeek.Authorization;

public class MailPeekAuthContext(HttpContext httpContext)
{
    public HttpContext HttpContext { get; } = httpContext;
}
