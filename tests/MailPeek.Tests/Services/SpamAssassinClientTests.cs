using MailPeek.Services;

namespace MailPeek.Tests.Services;

public class SpamAssassinClientTests
{
    [Fact]
    public void ParseResponse_ParsesScoreAndRules()
    {
        var response = "SPAMD/1.1 0 EX_OK\r\nSpam: True ; 8.5 / 5.0\r\n\r\n";
        var result = SpamAssassinClient.ParseCheckResponse(response);
        Assert.NotNull(result);
        Assert.Equal(8.5, result!.Score);
        Assert.Equal("spamassassin", result.Source);
    }

    [Fact]
    public void ParseResponse_HandlesCleanMessage()
    {
        var response = "SPAMD/1.1 0 EX_OK\r\nSpam: False ; 1.2 / 5.0\r\n\r\n";
        var result = SpamAssassinClient.ParseCheckResponse(response);
        Assert.NotNull(result);
        Assert.Equal(1.2, result!.Score);
    }

    [Fact]
    public void ParseResponse_ReturnsNullForInvalidResponse()
    {
        var result = SpamAssassinClient.ParseCheckResponse("garbage");
        Assert.Null(result);
    }

    [Fact]
    public void BuildCheckCommand_FormatsCorrectly()
    {
        var rawMessage = System.Text.Encoding.UTF8.GetBytes("Subject: Test\r\n\r\nBody");
        var command = SpamAssassinClient.BuildCheckCommand(rawMessage);
        Assert.StartsWith("CHECK SPAMC/1.5\r\n", command, StringComparison.Ordinal);
        Assert.Contains("Content-length:", command, StringComparison.Ordinal);
    }
}
