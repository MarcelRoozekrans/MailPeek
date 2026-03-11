namespace MailPeek.Models;

public class SpamCheckRule
{
    public required string Name { get; set; }
    public double Score { get; set; }
    public required string Description { get; set; }
}
