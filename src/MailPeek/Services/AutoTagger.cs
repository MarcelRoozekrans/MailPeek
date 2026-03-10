using MailPeek.Configuration;
using MailPeek.Models;
using MailPeek.Storage;
using Microsoft.Extensions.Options;

namespace MailPeek.Services;

public class AutoTagger(IMessageStore store, IOptions<MailPeekSmtpOptions> options)
{
    public void Start()
    {
        if (options.Value.AutoTagPlusAddressing)
            store.OnMessageReceived += OnMessageReceived;
    }

    private void OnMessageReceived(StoredMessage message)
    {
        var tags = PlusAddressTagExtractor.ExtractTags(message);
        if (tags.Count > 0)
            message.Tags = tags;
    }
}
