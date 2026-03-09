using System.Buffers;
using MimeKit;
using SmtpServer.Dashboard.Models;
using SmtpLib = global::SmtpServer;

namespace SmtpServer.Dashboard.Smtp;

public class FakeSmtpMessageStore(Dashboard.Storage.IMessageStore messageStore)
    : SmtpLib.Storage.MessageStore
{
    public override async Task<SmtpLib.Protocol.SmtpResponse> SaveAsync(
        SmtpLib.ISessionContext context,
        SmtpLib.IMessageTransaction transaction,
        ReadOnlySequence<byte> buffer,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        var raw = buffer.ToArray();
        ParseAndStore(raw);
        return new SmtpLib.Protocol.SmtpResponse(SmtpLib.Protocol.SmtpReplyCode.Ok, "Message saved");
    }

    public void ParseAndStore(byte[] raw)
    {
        var storedMessage = new StoredMessage { RawMessage = raw };

        try
        {
            using var stream = new MemoryStream(raw);
            var mime = MimeMessage.Load(stream);

            storedMessage.From = mime.From.Mailboxes.FirstOrDefault()?.Address ?? string.Empty;
            storedMessage.To = mime.To.Mailboxes.Select(m => m.Address).ToList();
            storedMessage.Cc = mime.Cc.Mailboxes.Select(m => m.Address).ToList();
            storedMessage.Bcc = mime.Bcc.Mailboxes.Select(m => m.Address).ToList();
            storedMessage.Subject = mime.Subject ?? string.Empty;
            storedMessage.TextBody = mime.TextBody;
            storedMessage.HtmlBody = mime.HtmlBody;

            foreach (var header in mime.Headers)
            {
                storedMessage.Headers[header.Field] = header.Value;
            }

            foreach (var attachment in mime.Attachments)
            {
                if (attachment is MimePart part)
                {
                    using var ms = new MemoryStream();
                    part.Content?.DecodeTo(ms);

                    storedMessage.Attachments.Add(new StoredAttachment
                    {
                        FileName = part.FileName ?? "unknown",
                        ContentType = part.ContentType.MimeType,
                        Content = ms.ToArray()
                    });
                }
            }
        }
        catch (Exception ex)
        {
            storedMessage.ParseError = true;
            storedMessage.ParseErrorMessage = ex.Message;
        }

        messageStore.Add(storedMessage);
    }
}
