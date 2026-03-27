using KasseAPI_Final.Models;

namespace KasseAPI_Final.Models.Inventory;

/// <summary>
/// Tekil stok hareketi satırı — global geçmiş listesi için (audit trail ile birlikte kullanım).
/// </summary>
public sealed class InventoryHistoryRowDto
{
    public Guid TransactionId { get; set; }
    public Guid InventoryId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductCategory { get; set; } = string.Empty;
    public TransactionType TransactionType { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string? Notes { get; set; }
    public DateTime TransactionDateUtc { get; set; }
}

/// <summary>
/// Sayfalı hareket geçmişi yanıtı.
/// </summary>
public sealed class InventoryHistoryPageDto
{
    public IReadOnlyList<InventoryHistoryRowDto> Items { get; set; } = Array.Empty<InventoryHistoryRowDto>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// Nachbestell-Vorschlag: aktueller Bestand vs. Mindest-/Meldebestand.
/// </summary>
public sealed class ReorderSuggestionDto
{
    public Guid InventoryId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductCategory { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public int MinStockLevel { get; set; }
    public int? ReorderPoint { get; set; }
    /// <summary>Heuristic: max(0, reorderPoint - current) veya minStock - current.</summary>
    public int SuggestedOrderQuantity { get; set; }
}

/// <summary>
/// Lagerumlagerung zwischen zwei Bestandszeilen (unterschiedliche Produkte möglich).
/// </summary>
public sealed class TransferInventoryRequest
{
    public Guid TargetInventoryId { get; set; }
    public int Quantity { get; set; }
    public string? Notes { get; set; }
}
