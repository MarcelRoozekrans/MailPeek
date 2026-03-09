namespace MailPeek.Authorization;

public interface IMailPeekAuthorizationFilter
{
    bool Authorize(MailPeekAuthContext context);
}
