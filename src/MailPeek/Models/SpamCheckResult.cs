namespace MailPeek.Models;

public class SpamCheckResult
{
    public double Score { get; set; }
    public required string Source { get; set; }
#pragma warning disable MA0016
    public List<SpamCheckRule> Rules { get; set; } = [];
#pragma warning restore MA0016
}
