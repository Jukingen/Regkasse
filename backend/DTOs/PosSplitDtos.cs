using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace KasseAPI_Final.DTOs;

public sealed class StartSplitRequest
{
    /// <summary>Cart row id (<see cref="Models.Cart.Id"/> uuid).</summary>
    [Required]
    public Guid CartId { get; set; }
}

public sealed class AssignItemRequest
{
    [Required]
    public Guid ItemId { get; set; }

    /// <summary>Empty clears assignment (item returns to open pool).</summary>
    [MaxLength(200)]
    public string CustomerName { get; set; } = string.Empty;

    [Range(0, 99)]
    public int SeatNumber { get; set; }
}

public sealed class SplitItemDto
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public Guid? SourceCartItemId { get; init; }
    public int Quantity { get; init; }
    public decimal Price { get; init; }
    public decimal LineTotal { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public int SeatNumber { get; init; }
}

public sealed class SplitSessionDto
{
    public Guid Id { get; init; }
    public Guid OriginalCartId { get; init; }
    public string OriginalCartKey { get; init; } = string.Empty;
    public int? TableNumber { get; init; }
    public bool IsCompleted { get; init; }
    public DateTime CreatedAt { get; init; }
    public IReadOnlyList<SplitItemDto> Items { get; init; } = Array.Empty<SplitItemDto>();
    public decimal GrandTotal { get; init; }

    [JsonIgnore]
    public IReadOnlyList<SplitItemDto> SplitItems => Items;
}

public sealed class MergeSplitSessionsRequest
{
    [Required]
    [MinLength(2)]
    public List<Guid> SessionIds { get; set; } = new();

    [Range(1, 99)]
    public int TargetTableNumber { get; set; }
}
