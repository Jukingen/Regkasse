using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Registrierkasse_API.Data;
using Registrierkasse_API.Models;

namespace Registrierkasse_API.Services
{
    /// <summary>
    /// DEP (DatenErfassungsProtokoll) yönetimi: Gerçek zamanlı kayıt ve dışa aktarım
    /// </summary>
    public class DepService
    {
        private readonly AppDbContext _context;
        public DepService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Her işlemde DEP kaydını gerçek zamanlı olarak saklar
        /// </summary>
        public async Task StoreDepEntryAsync(DepEntry entry)
        {
            entry.Id = Guid.NewGuid();
            entry.Timestamp = DateTime.UtcNow;
            _context.DepEntries.Add(entry);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Belirli tarih aralığındaki DEP kayıtlarını dışa aktarır (JSON/BMF formatı)
        /// </summary>
        public async Task<List<DepEntry>> ExportDepEntriesAsync(DateTime from, DateTime to)
        {
            return await _context.DepEntries
                .Where(e => e.Timestamp >= from && e.Timestamp <= to)
                .OrderBy(e => e.Timestamp)
                .ToListAsync();
        }

        /// <summary>
        /// Yılsonunda fiş, rapor ve audit kayıtlarını BMF (FinanzOnline) formatında toplu export eder
        /// </summary>
        public async Task<BmfExportResult> ExportYearEndDataAsync(int year)
        {
            // Türkçe açıklama: Yıl içindeki tüm fiş, rapor ve audit kayıtlarını BMF formatında dışa aktarır
            // Receipt modelinde Date yoksa CreatedAt kullanılır
            var receipts = await _context.Receipts.Where(r => r.CreatedAt.Year == year).ToListAsync();
            var depEntries = await _context.DepEntries.Where(e => e.Timestamp.Year == year).ToListAsync();
            var audits = await _context.AuditLogs.Where(a => a.CreatedAt.Year == year).ToListAsync();

            var export = new BmfExportResult
            {
                Year = year,
                Receipts = receipts,
                DepEntries = depEntries,
                AuditLogs = audits
            };
            return export;
        }
    }

    // BMF export sonucu modeli (örnek)
    public class BmfExportResult
    {
        public int Year { get; set; }
        public List<Receipt> Receipts { get; set; } = new();
        public List<DepEntry> DepEntries { get; set; } = new();
        public List<AuditLog> AuditLogs { get; set; } = new();
    }
} 