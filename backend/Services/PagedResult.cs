namespace TicketeraOnline.Api.Services;

/// <summary>
/// Generic paginated result returned by list endpoints.
/// </summary>
/// <typeparam name="T">Type of the items in the page.</typeparam>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = new List<T>();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
