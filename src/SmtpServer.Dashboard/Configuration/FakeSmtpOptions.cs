namespace SmtpServer.Dashboard.Configuration;

public class FakeSmtpOptions
{
    public int Port { get; set; } = 2525;
    public string Hostname { get; set; } = "localhost";
    public int MaxMessages { get; set; } = 1000;
    public long MaxMessageSize { get; set; } = 10_000_000;
}
