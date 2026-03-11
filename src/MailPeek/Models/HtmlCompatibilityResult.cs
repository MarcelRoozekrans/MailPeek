namespace MailPeek.Models;

public class HtmlCompatibilityResult
{
    public int Score { get; set; }
#pragma warning disable MA0016
    public List<HtmlCompatibilityIssue> Issues { get; set; } = [];
#pragma warning restore MA0016
}
