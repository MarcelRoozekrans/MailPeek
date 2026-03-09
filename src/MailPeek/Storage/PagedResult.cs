namespace MailPeek.Storage;

public class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; set; }
    public required int TotalCount { get; set; }
}
