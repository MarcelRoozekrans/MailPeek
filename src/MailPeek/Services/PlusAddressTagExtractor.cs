using MailPeek.Models;

namespace MailPeek.Services;

public static class PlusAddressTagExtractor
{
#pragma warning disable MA0016
    public static List<string> ExtractTags(StoredMessage message)
#pragma warning restore MA0016
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var address in message.To)
        {
            var plusIndex = address.IndexOf('+', StringComparison.Ordinal);
            var atIndex = address.IndexOf('@', StringComparison.Ordinal);
            if (plusIndex >= 0 && atIndex > plusIndex)
            {
                var tag = address[(plusIndex + 1)..atIndex];
                if (!string.IsNullOrWhiteSpace(tag))
                    tags.Add(tag);
            }
        }
        return [.. tags];
    }
}
