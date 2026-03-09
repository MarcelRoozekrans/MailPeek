using MailPeek.Models;
using MailPeek.Storage;

namespace MailPeek.Tests.Storage;

public class InMemoryMessageStoreTests
{
    private readonly InMemoryMessageStore _store = new(maxMessages: 5);

    [Fact]
    public void Add_StoresMessage()
    {
        var msg = CreateMessage("test@example.com", "Hello");
        _store.Add(msg);

        var all = _store.GetAll();
#pragma warning disable HLQ005
        Assert.Single(all);
#pragma warning restore HLQ005
        Assert.Equal("Hello", all[0].Subject);
    }

    [Fact]
    public void GetAll_ReturnsNewestFirst()
    {
        var msg1 = CreateMessage("a@test.com", "First");
        var msg2 = CreateMessage("b@test.com", "Second");
        _store.Add(msg1);
        _store.Add(msg2);

        var all = _store.GetAll().ToList();
        Assert.Equal("Second", all[0].Subject);
        Assert.Equal("First", all[1].Subject);
    }

    [Fact]
    public void GetById_ReturnsCorrectMessage()
    {
        var msg = CreateMessage("test@example.com", "Hello");
        _store.Add(msg);

        var found = _store.GetById(msg.Id);
        Assert.NotNull(found);
        Assert.Equal(msg.Id, found.Id);
    }

    [Fact]
    public void GetById_ReturnsNullForMissing()
    {
        Assert.Null(_store.GetById(Guid.NewGuid()));
    }

    [Fact]
    public void Delete_RemovesMessage()
    {
        var msg = CreateMessage("test@example.com", "Hello");
        _store.Add(msg);

        var deleted = _store.Delete(msg.Id);
        Assert.True(deleted);
        Assert.Empty(_store.GetAll());
    }

    [Fact]
    public void Delete_ReturnsFalseForMissing()
    {
        Assert.False(_store.Delete(Guid.NewGuid()));
    }

    [Fact]
    public void Clear_RemovesAllMessages()
    {
        _store.Add(CreateMessage("a@test.com", "First"));
        _store.Add(CreateMessage("b@test.com", "Second"));

        _store.Clear();
        Assert.Empty(_store.GetAll());
    }

    [Fact]
    public void Add_EvictsOldestWhenMaxReached()
    {
        for (var i = 0; i < 6; i++)
        {
            _store.Add(CreateMessage($"user{i}@test.com", $"Message {i}"));
        }

        var all = _store.GetAll().ToList();
        Assert.Equal(5, all.Count);
        Assert.DoesNotContain(all, m => string.Equals(m.Subject, "Message 0", StringComparison.Ordinal));
        Assert.Contains(all, m => string.Equals(m.Subject, "Message 5", StringComparison.Ordinal));
    }

    [Fact]
    public void OnMessageReceived_FiresWhenMessageAdded()
    {
        StoredMessage? received = null;
        _store.OnMessageReceived += msg => received = msg;

        var msg = CreateMessage("test@example.com", "Hello");
        _store.Add(msg);

        Assert.NotNull(received);
        Assert.Equal(msg.Id, received.Id);
    }

    [Fact]
    public void GetPage_ReturnsPaginatedResults()
    {
        var store = new InMemoryMessageStore(maxMessages: 20);
        for (var i = 0; i < 10; i++)
        {
            store.Add(CreateMessage($"user{i}@test.com", $"Message {i}"));
        }

        var page = store.GetPage(pageNumber: 1, pageSize: 3);
        Assert.Equal(3, page.Items.Count);
        Assert.Equal(10, page.TotalCount);
    }

    private static StoredMessage CreateMessage(string from, string subject) => new()
    {
        From = from,
        To = ["recipient@test.com"],
        Subject = subject
    };
}
