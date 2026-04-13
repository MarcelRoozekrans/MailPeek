using System.Buffers;
using MimeKit;
using MailPeek.Models;
using SmtpLib = global::SmtpServer;

namespace MailPeek.Smtp;

public class MailPeekSmtpMessageStore(MailPeek.Storage.IMessageStore messageStore)
    : SmtpLib.Storage.MessageStore
{
    public override async Task<SmtpLib.Protocol.SmtpResponse> SaveAsync(
        SmtpLib.ISessionContext context,
        SmtpLib.IMessageTransaction transaction,
        ReadOnlySequence<byte> buffer,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false);
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
            storedMessage.Snippet = Services.SnippetExtractor.Extract(storedMessage);

            foreach (var header in mime.Headers)
            {
                storedMessage.Headers[header.Field] = header.Value;
            }

            foreach (var part in mime.BodyParts)
            {
                if (part.IsAttachment || (part is MimePart mimePart && !string.IsNullOrEmpty(mimePart.ContentId)))
                {
                    if (part is MimePart mPart)
                    {
                        using var ms = new MemoryStream();
                        mPart.Content?.DecodeTo(ms);

                        storedMessage.Attachments.Add(new StoredAttachment
                        {
                            FileName = mPart.FileName ?? "unknown",
                            ContentType = mPart.ContentType.MimeType,
                            Content = ms.ToArray(),
                            ContentId = mPart.ContentId
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            storedMessage.ParseError = true;
            storedMessage.ParseErrorMessage = ex.ToString();
        }

        messageStore.Add(storedMessage);
    }
}
