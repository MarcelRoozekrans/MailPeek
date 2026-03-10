using System.Text.Json.Serialization;

namespace MailPeek.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LinkStatus
{
    Ok,
    Broken,
    Timeout,
    Error
}

public class LinkCheckResult
{
    public required string Url { get; set; }
    public int? StatusCode { get; set; }
    public LinkStatus Status { get; set; }
}
