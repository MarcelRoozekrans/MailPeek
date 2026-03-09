using MimeKit;
using MailPeek.Smtp;
using MailPeek.Storage;

namespace MailPeek.Tests.Smtp;

public class MailPeekSmtpMessageStoreTests
{
    [Fact]
    public async Task ParseAndStore_ParsesSimpleMessage()
    {
        var store = new InMemoryMessageStore();
        var handler = new MailPeekSmtpMessageStore(store);

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress("Sender", "sender@test.com"));
        mime.To.Add(new MailboxAddress("Recipient", "recipient@test.com"));
        mime.Cc.Add(new MailboxAddress("CC User", "cc@test.com"));
        mime.Subject = "Test Subject";
        mime.Body = new TextPart("plain") { Text = "Hello World" };

        using var stream = new MemoryStream();
        await mime.WriteToAsync(stream);
        var raw = stream.ToArray();

        handler.ParseAndStore(raw);

        var messages = store.GetAll();
        Assert.Single(messages);
        var msg = messages[0];
        Assert.Equal("sender@test.com", msg.From);
        Assert.Contains("recipient@test.com", msg.To);
        Assert.Contains("cc@test.com", msg.Cc);
        Assert.Equal("Test Subject", msg.Subject);
        Assert.Equal("Hello World", msg.TextBody?.TrimEnd());
    }

    [Fact]
    public async Task ParseAndStore_ParsesHtmlMessage()
    {
        var store = new InMemoryMessageStore();
        var handler = new MailPeekSmtpMessageStore(store);

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress("Sender", "sender@test.com"));
        mime.To.Add(new MailboxAddress("Recipient", "recipient@test.com"));
        mime.Subject = "HTML Test";

        var builder = new BodyBuilder
        {
            TextBody = "Plain text",
            HtmlBody = "<h1>Hello</h1>"
        };
        mime.Body = builder.ToMessageBody();

        using var stream = new MemoryStream();
        await mime.WriteToAsync(stream);

        handler.ParseAndStore(stream.ToArray());

        var msg = store.GetAll().Single();
        Assert.Equal("Plain text", msg.TextBody);
        Assert.Equal("<h1>Hello</h1>", msg.HtmlBody);
    }

    [Fact]
    public async Task ParseAndStore_ParsesAttachments()
    {
        var store = new InMemoryMessageStore();
        var handler = new MailPeekSmtpMessageStore(store);

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress("Sender", "sender@test.com"));
        mime.To.Add(new MailboxAddress("Recipient", "recipient@test.com"));
        mime.Subject = "With Attachment";

        var builder = new BodyBuilder { TextBody = "See attached" };
        builder.Attachments.Add("test.txt", System.Text.Encoding.UTF8.GetBytes("file content"),
            new ContentType("text", "plain"));
        mime.Body = builder.ToMessageBody();

        using var stream = new MemoryStream();
        await mime.WriteToAsync(stream);

        handler.ParseAndStore(stream.ToArray());

        var msg = store.GetAll().Single();
        Assert.True(msg.HasAttachments);
        Assert.Single(msg.Attachments);
        Assert.Equal("test.txt", msg.Attachments[0].FileName);
    }

    [Fact]
    public void ParseAndStore_HandlesMalformedMessage()
    {
        var store = new InMemoryMessageStore();
        var handler = new MailPeekSmtpMessageStore(store);

        handler.ParseAndStore([0xFF, 0xFE, 0x00]);

        var msg = store.GetAll().Single();
        Assert.True(msg.ParseError);
        Assert.NotNull(msg.ParseErrorMessage);
        Assert.NotNull(msg.RawMessage);
    }
}
