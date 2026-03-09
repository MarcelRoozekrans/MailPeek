using SmtpServer.Dashboard.Models;

namespace SmtpServer.Dashboard.Storage;

public class PagedResult<T>
{
    public required List<T> Items { get; set; }
    public required int TotalCount { get; set; }
}

public interface IMessageStore
{
    void Add(StoredMessage message);
    IReadOnlyList<StoredMessage> GetAll();
    StoredMessage? GetById(Guid id);
    bool Delete(Guid id);
    void Clear();
    PagedResult<StoredMessage> GetPage(int pageNumber, int pageSize, string? searchTerm = null);
    event Action<StoredMessage>? OnMessageReceived;
}
