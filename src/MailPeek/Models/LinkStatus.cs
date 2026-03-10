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
