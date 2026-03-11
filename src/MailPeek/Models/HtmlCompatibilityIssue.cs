namespace MailPeek.Models;

public class HtmlCompatibilityIssue
{
    public required string RuleId { get; set; }
    public required string Description { get; set; }
    public required IssueSeverity Severity { get; set; }
#pragma warning disable MA0016
    public required List<string> AffectedClients { get; set; }
#pragma warning restore MA0016
}
