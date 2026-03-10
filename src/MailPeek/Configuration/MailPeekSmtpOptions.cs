namespace MailPeek.Configuration;

public class MailPeekSmtpOptions
{
    public int Port { get; set; } = 2525;
    public string Hostname { get; set; } = "localhost";
    public int MaxMessages { get; set; } = 1000;
    public long MaxMessageSize { get; set; } = 10_000_000;
    public bool AutoTagPlusAddressing { get; set; } = true;
}
