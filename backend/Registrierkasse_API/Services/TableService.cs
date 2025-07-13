using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Registrierkasse.Data;
using Registrierkasse.Models;

namespace Registrierkasse.Services
{
    public interface ITableService
    {
        Task<List<Table>> GetAllTablesAsync();
        Task<Table?> GetTableByNumberAsync(int tableNumber);
        Task<Table> UpdateTableStatusAsync(int tableNumber, string status, string? customerName = null);
        Task<Table> UpdateTableOrderAsync(int tableNumber, Guid orderId, decimal total);
        Task<Table> CompleteTableOrderAsync(int tableNumber, decimal paidAmount);
        Task<Table> ClearTableAsync(int tableNumber);
        Task<Table> ReserveTableAsync(int tableNumber, string customerName);
        Task<List<Order>> GetTableOrderHistoryAsync(int tableNumber);
        Task<Table> UpdateTableCustomerAsync(int tableNumber, string customerName);
    }

    public class TableService : ITableService
    {
        private readonly AppDbContext _context;

        public TableService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Table>> GetAllTablesAsync()
        {
            return await _context.Tables
                .Include(t => t.CurrentOrder)
                .Include(t => t.OrderHistory)
                .Include(t => t.Reservations)
                .Where(t => t.IsActive)
                .OrderBy(t => t.Number)
                .ToListAsync();
        }

        public async Task<Table?> GetTableByNumberAsync(int tableNumber)
        {
            return await _context.Tables
                .Include(t => t.CurrentOrder)
                .Include(t => t.OrderHistory)
                .Include(t => t.Reservations)
                .FirstOrDefaultAsync(t => t.Number == tableNumber && t.IsActive);
        }

        public async Task<Table> UpdateTableStatusAsync(int tableNumber, string status, string? customerName = null)
        {
            var table = await GetTableByNumberAsync(tableNumber);
            if (table == null)
            {
                throw new ArgumentException($"Table {tableNumber} not found");
            }

            table.Status = status;
            
            if (status == "occupied" && customerName != null)
            {
                table.CustomerName = customerName;
                table.StartTime = DateTime.UtcNow;
            }
            else if (status == "empty")
            {
                table.CustomerName = string.Empty;
                table.StartTime = null;
            }

            await _context.SaveChangesAsync();
            return table;
        }

        public async Task<Table> UpdateTableOrderAsync(int tableNumber, Guid orderId, decimal total)
        {
            var table = await GetTableByNumberAsync(tableNumber);
            if (table == null)
            {
                throw new ArgumentException($"Table {tableNumber} not found");
            }

            table.CurrentOrderId = orderId;
            table.CurrentTotal = total;
            table.LastOrderTime = DateTime.UtcNow;
            table.Status = "occupied";

            await _context.SaveChangesAsync();
            return table;
        }

        public async Task<Table> CompleteTableOrderAsync(int tableNumber, decimal paidAmount)
        {
            var table = await GetTableByNumberAsync(tableNumber);
            if (table == null)
            {
                throw new ArgumentException($"Table {tableNumber} not found");
            }

            // Toplam ödenen miktarı güncelle
            table.TotalPaid += paidAmount;
            
            // Mevcut siparişi geçmişe taşı
            if (table.CurrentOrder != null)
            {
                table.OrderHistory.Add(table.CurrentOrder);
                table.CurrentOrderId = null;
                table.CurrentTotal = 0;
            }

            // Masayı ödenmiş olarak işaretle
            table.Status = "paid";

            await _context.SaveChangesAsync();
            return table;
        }

        public async Task<Table> ClearTableAsync(int tableNumber)
        {
            var table = await GetTableByNumberAsync(tableNumber);
            if (table == null)
            {
                throw new ArgumentException($"Table {tableNumber} not found");
            }

            // Masayı temizle ama geçmişi koru
            table.Status = "empty";
            table.CustomerName = string.Empty;
            table.StartTime = null;
            table.CurrentOrderId = null;
            table.CurrentTotal = 0;
            // TotalPaid ve OrderHistory korunur

            await _context.SaveChangesAsync();
            return table;
        }

        public async Task<Table> ReserveTableAsync(int tableNumber, string customerName)
        {
            var table = await GetTableByNumberAsync(tableNumber);
            if (table == null)
            {
                throw new ArgumentException($"Table {tableNumber} not found");
            }

            table.Status = "reserved";
            table.CustomerName = customerName;

            await _context.SaveChangesAsync();
            return table;
        }

        public async Task<List<Order>> GetTableOrderHistoryAsync(int tableNumber)
        {
            return await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.Customer)
                .Where(o => o.TableNumber == tableNumber && !o.IsActive)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task<Table> UpdateTableCustomerAsync(int tableNumber, string customerName)
        {
            var table = await GetTableByNumberAsync(tableNumber);
            if (table == null)
            {
                throw new ArgumentException($"Table {tableNumber} not found");
            }

            table.CustomerName = customerName;
            await _context.SaveChangesAsync();
            return table;
        }
    }
} 