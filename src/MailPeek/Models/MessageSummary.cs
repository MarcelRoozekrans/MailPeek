namespace MailPeek.Models;

public class MessageSummary
{
    public required Guid Id { get; set; }
    public required string From { get; set; }
    public required IReadOnlyList<string> To { get; set; }
    public required string Subject { get; set; }
    public required bool HasAttachments { get; set; }
    public required DateTimeOffset ReceivedAt { get; set; }
}
