using MailPeek.Models;

namespace MailPeek.Tests.Models;

public class StoredMessageTests
{
    [Fact]
    public void Constructor_SetsIdAndReceivedDate()
    {
        var msg = new StoredMessage();

        Assert.NotEqual(Guid.Empty, msg.Id);
        Assert.True(msg.ReceivedAt <= DateTimeOffset.UtcNow);
        Assert.True(msg.ReceivedAt > DateTimeOffset.UtcNow.AddSeconds(-5));
    }

    [Fact]
    public void HasAttachments_ReturnsTrueWhenAttachmentsExist()
    {
        var msg = new StoredMessage();
        msg.Attachments.Add(new StoredAttachment
        {
            FileName = "test.txt",
            ContentType = "text/plain",
            Content = new byte[] { 1, 2, 3 }
        });

        Assert.True(msg.HasAttachments);
    }

    [Fact]
    public void HasAttachments_ReturnsFalseWhenEmpty()
    {
        var msg = new StoredMessage();
        Assert.False(msg.HasAttachments);
    }

    [Fact]
    public void IsRead_DefaultsFalse()
    {
        var msg = new StoredMessage();
        Assert.False(msg.IsRead);
    }

    [Fact]
    public void ToSummary_IncludesIsRead()
    {
        var msg = new StoredMessage { IsRead = true };
        var summary = msg.ToSummary();
        Assert.True(summary.IsRead);
    }

    [Fact]
    public void Tags_DefaultsEmpty()
    {
        var msg = new StoredMessage();
        Assert.Empty(msg.Tags);
    }

    [Fact]
    public void ToSummary_IncludesTags()
    {
        var msg = new StoredMessage { Tags = ["welcome", "signup"] };
        var summary = msg.ToSummary();
        Assert.Equal(["welcome", "signup"], summary.Tags);
    }

    [Fact]
    public void ToSummary_MapsCorrectly()
    {
        var msg = new StoredMessage
        {
            From = "sender@test.com",
            To = ["recipient@test.com"],
            Subject = "Hello",
        };
        msg.Attachments.Add(new StoredAttachment
        {
            FileName = "file.pdf",
            ContentType = "application/pdf",
            Content = []
        });

        var summary = msg.ToSummary();

        Assert.Equal(msg.Id, summary.Id);
        Assert.Equal("sender@test.com", summary.From);
        Assert.Equal("recipient@test.com", summary.To.First());
        Assert.Equal("Hello", summary.Subject);
        Assert.True(summary.HasAttachments);
        Assert.Equal(msg.ReceivedAt, summary.ReceivedAt);
    }
}
