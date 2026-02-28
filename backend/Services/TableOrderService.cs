using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace KasseAPI_Final.Services
{
    /// <summary>
    /// TableOrder ile Cart arasında sync sağlayan service
    /// Cart'tan TableOrder'a migration yapar
    /// RKSV uyumlu audit trail sağlar
    /// </summary>
    public class TableOrderService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TableOrderService> _logger;

        public TableOrderService(AppDbContext context, ILogger<TableOrderService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Cart'ı TableOrder'a dönüştürür ve kalıcı hale getirir
        /// Bu işlem F5 recovery için kritik
        /// </summary>
        public async Task<TableOrder?> ConvertCartToTableOrderAsync(string cartId, string userId)
        {
            try
            {
                // Cart'ı bul
                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .Include(c => c.Customer)
                    .FirstOrDefaultAsync(c => c.CartId == cartId && c.UserId == userId);

                if (cart == null || cart.Items.Count == 0)
                {
                    _logger.LogWarning("Cart not found or empty for conversion - CartId: {CartId}, UserId: {UserId}", cartId, userId);
                    return null;
                }

                // Aynı masa için TableOrder zaten var mı kontrol et
                var existingTableOrder = await _context.TableOrders
                    .FirstOrDefaultAsync(to => to.TableNumber == cart.TableNumber && 
                                              to.UserId == userId && 
                                              to.Status == TableOrderStatus.Active);

                if (existingTableOrder != null)
                {
                    // Mevcut TableOrder'ı güncelle
                    return await UpdateExistingTableOrderAsync(existingTableOrder, cart);
                }

                // Yeni TableOrder oluştur
                var tableOrder = new TableOrder
                {
                    TableOrderId = $"TO-{cart.TableNumber}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8]}",
                    TableNumber = cart.TableNumber ?? 0,
                    UserId = userId,
                    WaiterName = cart.WaiterName,
                    CustomerName = cart.Customer?.Name,
                    CustomerId = cart.CustomerId,
                    Notes = cart.Notes,
                    Status = TableOrderStatus.Active,
                    OrderStartTime = cart.CreatedAt,
                    LastModifiedTime = DateTime.UtcNow,
                    CartId = cartId,
                    SessionId = Guid.NewGuid().ToString("N")[..16], // Session tracking için
                    StatusHistory = JsonSerializer.Serialize(new List<TableOrderStatusChange>
                    {
                        new TableOrderStatusChange
                        {
                            Timestamp = DateTime.UtcNow,
                            FromStatus = TableOrderStatus.Active,
                            ToStatus = TableOrderStatus.Active,
                            UserId = userId,
                            Reason = "Created from Cart"
                        }
                    })
                };

                // Product bilgilerini ayrı sorgu ile al
                var productIds = cart.Items.Select(ci => ci.ProductId).ToList();
                var products = await _context.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p);

                // Items'ları dönüştür
                var tableOrderItems = cart.Items.Select(cartItem => {
                    var product = products.TryGetValue(cartItem.ProductId, out var p) ? p : null;
                    return new TableOrderItem
                    {
                        TableOrderId = tableOrder.TableOrderId,
                        ProductId = cartItem.ProductId,
                        ProductName = product?.Name ?? "Unknown Product",
                        Quantity = cartItem.Quantity,
                        UnitPrice = cartItem.UnitPrice,
                        TotalPrice = cartItem.UnitPrice * cartItem.Quantity,
                        Notes = cartItem.Notes,
                        TaxType = product?.TaxType ?? 1,
                        TaxRate = GetTaxRate(product?.TaxType ?? 1),
                        Status = ItemStatus.Pending,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                }).ToList();

                // Toplamları hesapla
                tableOrder.Subtotal = tableOrderItems.Sum(item => item.TotalPrice);
                tableOrder.TaxAmount = tableOrderItems.Sum(item => item.TotalPrice * item.TaxRate / 100);
                tableOrder.TotalAmount = tableOrder.Subtotal + tableOrder.TaxAmount;

                tableOrder.Items = tableOrderItems;
                tableOrder.CreatedAt = DateTime.UtcNow;
                tableOrder.UpdatedAt = DateTime.UtcNow;

                // Database'e kaydet
                _context.TableOrders.Add(tableOrder);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Cart converted to TableOrder successfully - TableOrderId: {TableOrderId}, CartId: {CartId}, TableNumber: {TableNumber}, ItemCount: {ItemCount}, TotalAmount: {TotalAmount}", 
                    tableOrder.TableOrderId, cartId, tableOrder.TableNumber, tableOrderItems.Count, tableOrder.TotalAmount);

                return tableOrder;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error converting Cart to TableOrder - CartId: {CartId}, UserId: {UserId}", cartId, userId);
                throw;
            }
        }

        /// <summary>
        /// Mevcut TableOrder'ı Cart verileriyle günceller
        /// </summary>
        private async Task<TableOrder> UpdateExistingTableOrderAsync(TableOrder existingTableOrder, Cart cart)
        {
            try
            {
                // Status history güncelle
                var statusHistory = GetStatusHistory(existingTableOrder.StatusHistory);
                statusHistory.Add(new TableOrderStatusChange
                {
                    Timestamp = DateTime.UtcNow,
                    FromStatus = existingTableOrder.Status,
                    ToStatus = existingTableOrder.Status,
                    UserId = cart.UserId,
                    Reason = "Updated from Cart"
                });

                // Mevcut items'ları temizle ve yenilerini ekle
                _context.TableOrderItems.RemoveRange(existingTableOrder.Items);

                // Product bilgilerini ayrı sorgu ile al
                var productIds = cart.Items.Select(ci => ci.ProductId).ToList();
                var products = await _context.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p);

                var newItems = cart.Items.Select(cartItem => {
                    var product = products.TryGetValue(cartItem.ProductId, out var p) ? p : null;
                    return new TableOrderItem
                    {
                        TableOrderId = existingTableOrder.TableOrderId,
                        ProductId = cartItem.ProductId,
                        ProductName = product?.Name ?? "Unknown Product",
                        Quantity = cartItem.Quantity,
                        UnitPrice = cartItem.UnitPrice,
                        TotalPrice = cartItem.UnitPrice * cartItem.Quantity,
                        Notes = cartItem.Notes,
                        TaxType = product?.TaxType ?? 1,
                        TaxRate = GetTaxRate(product?.TaxType ?? 1),
                        Status = ItemStatus.Pending,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                }).ToList();

                // Toplamları yeniden hesapla
                existingTableOrder.Subtotal = newItems.Sum(item => item.TotalPrice);
                existingTableOrder.TaxAmount = newItems.Sum(item => item.TotalPrice * item.TaxRate / 100);
                existingTableOrder.TotalAmount = existingTableOrder.Subtotal + existingTableOrder.TaxAmount;
                existingTableOrder.LastModifiedTime = DateTime.UtcNow;
                existingTableOrder.UpdatedAt = DateTime.UtcNow;
                existingTableOrder.StatusHistory = JsonSerializer.Serialize(statusHistory);
                existingTableOrder.CartId = cart.CartId;

                existingTableOrder.Items = newItems;

                // await _context.SaveChangesAsync(); // GEÇİCİ OLARAK DEVRE DIŞI

                _logger.LogInformation("🔄 TableOrder updated from Cart - TableOrderId: {TableOrderId}, ItemCount: {ItemCount}, TotalAmount: {TotalAmount}", 
                    existingTableOrder.TableOrderId, newItems.Count, existingTableOrder.TotalAmount);

                return existingTableOrder;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error updating TableOrder from Cart - TableOrderId: {TableOrderId}", existingTableOrder.TableOrderId);
                throw;
            }
        }

        /// <summary>
        /// TableOrder'ı tamamla (ödeme sonrası)
        /// </summary>
        public async Task<bool> CompleteTableOrderAsync(string tableOrderId, string userId, string reason = "Payment completed")
        {
            try
            {
                var tableOrder = await _context.TableOrders
                    .FirstOrDefaultAsync(to => to.TableOrderId == tableOrderId && to.UserId == userId);

                if (tableOrder == null)
                {
                    _logger.LogWarning("TableOrder not found for completion - TableOrderId: {TableOrderId}, UserId: {UserId}", tableOrderId, userId);
                    return false;
                }

                // Status güncelle
                var statusHistory = GetStatusHistory(tableOrder.StatusHistory);
                statusHistory.Add(new TableOrderStatusChange
                {
                    Timestamp = DateTime.UtcNow,
                    FromStatus = tableOrder.Status,
                    ToStatus = TableOrderStatus.Completed,
                    UserId = userId,
                    Reason = reason
                });

                tableOrder.Status = TableOrderStatus.Completed;
                tableOrder.CompletedTime = DateTime.UtcNow;
                tableOrder.LastModifiedTime = DateTime.UtcNow;
                tableOrder.UpdatedAt = DateTime.UtcNow;
                tableOrder.StatusHistory = JsonSerializer.Serialize(statusHistory);

                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ TableOrder completed - TableOrderId: {TableOrderId}, TotalAmount: {TotalAmount}", 
                    tableOrderId, tableOrder.TotalAmount);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error completing TableOrder - TableOrderId: {TableOrderId}", tableOrderId);
                return false;
            }
        }

        /// <summary>
        /// Status history'yi parse et
        /// </summary>
        private List<TableOrderStatusChange> GetStatusHistory(string? statusHistoryJson)
        {
            if (string.IsNullOrEmpty(statusHistoryJson))
                return new List<TableOrderStatusChange>();

            try
            {
                return JsonSerializer.Deserialize<List<TableOrderStatusChange>>(statusHistoryJson) 
                       ?? new List<TableOrderStatusChange>();
            }
            catch
            {
                return new List<TableOrderStatusChange>();
            }
        }

        /// <summary>
        /// Tax rate hesaplama - RKSV uyumlu
        /// </summary>
        private decimal GetTaxRate(int taxType)
        {
            return taxType switch
            {
                1 => 20.0m,   // %20 KDV
                2 => 10.0m,   // %10 KDV
                3 => 13.0m,   // %13 KDV
                4 => 0.0m,    // %0 ZeroRate (Österreich 2026)
                _ => 20.0m
            };
        }

        /// <summary>
        /// Otomatik Cart to TableOrder migration background task için
        /// </summary>
        public async Task<int> MigrateActiveCartsToTableOrdersAsync()
        {
            try
            {
                        var activeCarts = await _context.Carts
            .Include(c => c.Items)
            .Where(c => c.Status == CartStatus.Active && c.TableNumber.HasValue)
            .ToListAsync();

                int migratedCount = 0;

                foreach (var cart in activeCarts)
                {
                    var tableOrder = await ConvertCartToTableOrderAsync(cart.CartId, cart.UserId);
                    if (tableOrder != null)
                    {
                        migratedCount++;
                    }
                }

                _logger.LogInformation("🔄 Background migration completed: {MigratedCount} carts converted to TableOrders", migratedCount);
                return migratedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error during background Cart to TableOrder migration");
                return 0;
            }
        }
    }
}
