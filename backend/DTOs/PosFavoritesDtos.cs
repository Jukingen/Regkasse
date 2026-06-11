using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

public sealed class CashierFavoriteDto
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal ProductPrice { get; init; }
    public int SortOrder { get; init; }
}

public sealed class AddFavoriteRequest
{
    [Required]
    public Guid ProductId { get; set; }
}

public sealed class ReorderFavoritesRequest
{
    [Required]
    [MinLength(1)]
    public List<Guid> OrderIds { get; set; } = new();
}
