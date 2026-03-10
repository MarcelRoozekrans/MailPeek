using MailPeek.Models;

namespace MailPeek.Storage;

public interface IMessageStore
{
    void Add(StoredMessage message);
    IReadOnlyList<StoredMessage> GetAll();
    StoredMessage? GetById(Guid id);
    bool Delete(Guid id);
    int DeleteMany(IReadOnlyList<Guid> ids);
    bool MarkAsRead(Guid id);
    bool SetTags(Guid id, IList<string> tags);
    void Clear();
    int GetUnreadCount();
    PagedResult<StoredMessage> GetPage(int pageNumber, int pageSize, string? searchTerm = null, string? tag = null);
#pragma warning disable MA0046
    event Action<StoredMessage>? OnMessageReceived;
#pragma warning restore MA0046
}
