namespace UnshackledWord.Domain.WebApi;

public class PaginationResult<T>
{
    public ICollection<T> Items { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }

    public PaginationResult(ICollection<T> items, int totalCount, int pageSize)
    {
        TotalItems = totalCount;
        TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        Items = items;
    }
}
