namespace MailPeek.Models;

public class StoredMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public string From { get; set; } = string.Empty;
    public IReadOnlyList<string> To { get; set; } = [];
    public IReadOnlyList<string> Cc { get; set; } = [];
    public IReadOnlyList<string> Bcc { get; set; } = [];
    public string Subject { get; set; } = string.Empty;
    public string? TextBody { get; set; }
    public string? HtmlBody { get; set; }
    public IList<StoredAttachment> Attachments { get; set; } = [];
    public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
    public byte[]? RawMessage { get; set; }
    public bool ParseError { get; set; }
    public string? ParseErrorMessage { get; set; }
    public bool IsRead { get; set; }
#pragma warning disable MA0016
    public List<string> Tags { get; set; } = [];
    public List<LinkCheckResult>? LinkCheckResults { get; set; }
#pragma warning restore MA0016
    public bool LinkCheckComplete { get; set; }

    public bool HasAttachments => Attachments.Count > 0;

    public MessageSummary ToSummary() => new()
    {
        Id = Id,
        From = From,
        To = To,
        Subject = Subject,
        HasAttachments = HasAttachments,
        ReceivedAt = ReceivedAt,
        IsRead = IsRead,
        Tags = Tags
    };
}
