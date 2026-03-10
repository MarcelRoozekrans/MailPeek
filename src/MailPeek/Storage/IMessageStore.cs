using MailPeek.Models;

namespace MailPeek.Storage;

public interface IMessageStore
{
    void Add(StoredMessage message);
    IReadOnlyList<StoredMessage> GetAll();
    StoredMessage? GetById(Guid id);
    bool Delete(Guid id);
    bool MarkAsRead(Guid id);
    void Clear();
    PagedResult<StoredMessage> GetPage(int pageNumber, int pageSize, string? searchTerm = null);
#pragma warning disable MA0046
    event Action<StoredMessage>? OnMessageReceived;
#pragma warning restore MA0046
}
