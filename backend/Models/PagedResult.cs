using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models;

/// <summary>
/// Generic pagination envelope for list endpoints.
/// JSON keeps legacy <c>totalCount</c> (via <see cref="TotalCount"/>); <see cref="Total"/> is a code-side alias.
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();

    /// <summary>Total matching rows (code-side alias of <see cref="TotalCount"/>).</summary>
    [JsonIgnore]
    public int Total
    {
        get => TotalCount;
        set => TotalCount = value;
    }

    /// <summary>Serialized as <c>totalCount</c> — do not rename (invoice/receipt/cash-register API contract).</summary>
    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalPages { get; set; }
}
