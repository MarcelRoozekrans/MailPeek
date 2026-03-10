namespace MailPeek.Models;

public class LinkCheckResult
{
    public required string Url { get; set; }
    public int? StatusCode { get; set; }
    public LinkStatus Status { get; set; }
}
