using System.Collections.Concurrent;
using MailPeek.Models;

namespace MailPeek.Storage;

public class InMemoryMessageStore(int maxMessages = 1000) : IMessageStore
{
    private readonly ConcurrentDictionary<Guid, StoredMessage> _messages = new();
    private readonly LinkedList<Guid> _order = new();
#if NET9_0_OR_GREATER
    private readonly System.Threading.Lock _orderLock = new();
#else
    private readonly object _orderLock = new();
#endif

    public event Action<StoredMessage>? OnMessageReceived;

    public void Add(StoredMessage message)
    {
        _messages[message.Id] = message;

        lock (_orderLock)
        {
            _order.AddFirst(message.Id);

            while (_order.Count > maxMessages)
            {
                var oldest = _order.Last!.Value;
                _order.RemoveLast();
                _messages.TryRemove(oldest, out _);
            }
        }

        OnMessageReceived?.Invoke(message);
    }

    public IReadOnlyList<StoredMessage> GetAll()
    {
        lock (_orderLock)
        {
            return _order
                .Select(id => _messages.GetValueOrDefault(id))
                .Where(m => m is not null)
                .ToList()!;
        }
    }

    public StoredMessage? GetById(Guid id) =>
        _messages.GetValueOrDefault(id);

    public bool Delete(Guid id)
    {
        if (!_messages.TryRemove(id, out _))
            return false;

        lock (_orderLock)
        {
            _order.Remove(id);
        }

        return true;
    }

    public bool MarkAsRead(Guid id)
    {
        if (!_messages.TryGetValue(id, out var message))
            return false;
        message.IsRead = true;
        return true;
    }

    public void Clear()
    {
        lock (_orderLock)
        {
            _order.Clear();
        }

        _messages.Clear();
    }

    public PagedResult<StoredMessage> GetPage(int pageNumber, int pageSize, string? searchTerm = null)
    {
        var all = GetAll();

        IEnumerable<StoredMessage> filtered = all;
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLowerInvariant();
            filtered = all.Where(m =>
                m.Subject.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                m.From.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                m.To.Any(t => t.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        var list = filtered.ToList();

        return new PagedResult<StoredMessage>
        {
            Items = list.Skip(pageNumber * pageSize).Take(pageSize).ToList(),
            TotalCount = list.Count
        };
    }
}
