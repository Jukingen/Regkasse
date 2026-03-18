using System;
using System.Threading.Tasks;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace KasseAPI_Final.Services
{
    /// <summary>
    /// Centralized allocation of sequential BelegNr per (KassenId, date). Uses row-level locking for concurrency.
    /// </summary>
    public class ReceiptSequenceService : IReceiptSequenceService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ReceiptSequenceService> _logger;

        public ReceiptSequenceService(AppDbContext context, ILogger<ReceiptSequenceService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<string> AllocateNextBelegNrAsync(string kassenId, DateTime sequenceDate)
        {
            if (string.IsNullOrWhiteSpace(kassenId))
                throw new ArgumentException("KassenId is required for receipt sequence allocation.", nameof(kassenId));

            var dateOnly = sequenceDate.Date;
            const int maxRetries = 3;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Lock existing row if present so only one allocator proceeds per (kassen_id, date).
                    var row = await _context.ReceiptSequences
                        .FromSqlRaw(
                            "SELECT * FROM receipt_sequences WHERE kassen_id = {0} AND sequence_date = {1} FOR UPDATE",
                            kassenId,
                            dateOnly)
                        .FirstOrDefaultAsync();

                    int allocated;
                    if (row != null)
                    {
                        allocated = row.NextSequence;
                        row.NextSequence++;
                        row.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                        var belegNr = $"AT-{kassenId}-{dateOnly:yyyyMMdd}-{allocated}";
                        return belegNr;
                    }

                    // No row: insert first allocation (next_sequence = 2 so we hand out 1 and next caller gets 2).
                    var newRow = new ReceiptSequence
                    {
                        Id = Guid.NewGuid(),
                        KassenId = kassenId,
                        SequenceDate = dateOnly,
                        NextSequence = 2,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.ReceiptSequences.Add(newRow);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return $"AT-{kassenId}-{dateOnly:yyyyMMdd}-1";
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    // Concurrent insert: another request created the row. Rollback and retry; next attempt will SELECT FOR UPDATE and increment.
                    await transaction.RollbackAsync();
                    if (attempt == maxRetries - 1)
                    {
                        _logger.LogError(ex, "Receipt sequence allocation failed after {Attempts} attempts for KassenId={KassenId} Date={Date}", maxRetries, kassenId, dateOnly);
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Receipt sequence allocation failed for KassenId={KassenId} Date={Date}", kassenId, dateOnly);
                    throw;
                }
            }

            throw new InvalidOperationException($"Receipt sequence allocation failed for KassenId={kassenId} Date={dateOnly:yyyyMMdd} after {maxRetries} attempts.");
        }

        private static bool IsUniqueViolation(DbUpdateException ex)
        {
            return ex.InnerException is PostgresException pg && pg.SqlState == "23505";
        }
    }
}
