namespace KasseAPI_Final.Configuration;

/// <summary>
/// Optional inventory / product stock behavior for payment and reversal flows.
/// Schema and columns remain; this only gates runtime mutations and availability checks.
/// </summary>
public sealed class InventoryOptions
{
    public const string SectionName = "Inventory";

    /// <summary>
    /// When true: payment checks stock for non–sellable-add-on lines, decrements on success;
    /// storno and refund restore stock. When false (default): no stock check and no stock mutations in those paths
    /// (Product.StockQuantity unchanged; avoids negative edge cases and keeps decorative stock stable).
    /// </summary>
    public bool EnforceStockOnSales { get; set; } = false;
}
