using System.Text.Json.Serialization;

namespace MailPeek.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IssueSeverity
{
    Critical,
    Major,
    Minor
}
