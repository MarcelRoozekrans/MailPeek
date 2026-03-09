namespace SmtpServer.Dashboard.Models;

public class StoredMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public string From { get; set; } = string.Empty;
    public List<string> To { get; set; } = [];
    public List<string> Cc { get; set; } = [];
    public List<string> Bcc { get; set; } = [];
    public string Subject { get; set; } = string.Empty;
    public string? TextBody { get; set; }
    public string? HtmlBody { get; set; }
    public List<StoredAttachment> Attachments { get; set; } = [];
    public Dictionary<string, string> Headers { get; set; } = [];
    public byte[]? RawMessage { get; set; }
    public bool ParseError { get; set; }
    public string? ParseErrorMessage { get; set; }

    public bool HasAttachments => Attachments.Count > 0;

    public MessageSummary ToSummary() => new()
    {
        Id = Id,
        From = From,
        To = To,
        Subject = Subject,
        HasAttachments = HasAttachments,
        ReceivedAt = ReceivedAt
    };
}
