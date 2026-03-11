namespace MailPeek.Configuration;

public class SpamAssassinOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 783;
}
