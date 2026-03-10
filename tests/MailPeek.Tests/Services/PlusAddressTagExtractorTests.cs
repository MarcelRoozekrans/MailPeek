using MailPeek.Models;
using MailPeek.Services;

namespace MailPeek.Tests.Services;

public class PlusAddressTagExtractorTests
{
    [Fact]
    public void ExtractTags_ExtractsFromPlusAddress()
    {
        var msg = new StoredMessage { To = ["test+signup@example.com"] };
        var tags = PlusAddressTagExtractor.ExtractTags(msg);
        Assert.Equal(["signup"], tags);
    }

    [Fact]
    public void ExtractTags_ReturnsEmptyForNoPlusAddress()
    {
        var msg = new StoredMessage { To = ["test@example.com"] };
        Assert.Empty(PlusAddressTagExtractor.ExtractTags(msg));
    }

    [Fact]
    public void ExtractTags_HandlesMultipleRecipients()
    {
        var msg = new StoredMessage { To = ["a+tag1@x.com", "b+tag2@y.com", "c@z.com"] };
        var tags = PlusAddressTagExtractor.ExtractTags(msg);
        Assert.Equal(2, tags.Count);
        Assert.Contains("tag1", tags);
        Assert.Contains("tag2", tags);
    }

    [Fact]
    public void ExtractTags_DeduplicatesTags()
    {
        var msg = new StoredMessage { To = ["a+signup@x.com", "b+signup@y.com"] };
#pragma warning disable xUnit2013
        Assert.Equal(1, PlusAddressTagExtractor.ExtractTags(msg).Count);
#pragma warning restore xUnit2013
    }
}
