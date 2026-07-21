using System.Data;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KasseAPI_Final.Services
{
    /// <summary>
    /// Centralized BelegNr allocation per cash register (UUID) per day; human-readable segment from RegisterNumber.
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
        public async Task<string> AllocateNextBelegNrAsync(Guid cashRegisterId, string registerNumber, DateTime sequenceDate)
        {
            if (cashRegisterId == Guid.Empty)
                throw new ArgumentException("CashRegisterId must not be empty.", nameof(cashRegisterId));
            if (string.IsNullOrWhiteSpace(registerNumber))
                throw new ArgumentException("RegisterNumber is required for BelegNr.", nameof(registerNumber));

            var dateOnly = sequenceDate.Date;
            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            SetAllocateCommand(cmd, cashRegisterId, dateOnly);

            var result = await cmd.ExecuteScalarAsync();
            return ParseAllocatedResult(result, registerNumber, dateOnly, cashRegisterId);
        }

        /// <inheritdoc />
        public async Task<string> AllocateNextBelegNrInTransactionAsync(IDbContextTransaction transaction, Guid cashRegisterId, string registerNumber, DateTime sequenceDate)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));
            if (cashRegisterId == Guid.Empty)
                throw new ArgumentException("CashRegisterId must not be empty.", nameof(cashRegisterId));
            if (string.IsNullOrWhiteSpace(registerNumber))
                throw new ArgumentException("RegisterNumber is required for BelegNr.", nameof(registerNumber));

            var dateOnly = sequenceDate.Date;
            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction.GetDbTransaction();
            SetAllocateCommand(cmd, cashRegisterId, dateOnly);

            var result = await cmd.ExecuteScalarAsync();
            return ParseAllocatedResult(result, registerNumber, dateOnly, cashRegisterId);
        }

        private static void SetAllocateCommand(System.Data.Common.DbCommand cmd, Guid cashRegisterId, DateTime dateOnly)
        {
            cmd.CommandText = """
                INSERT INTO receipt_sequences (id, cash_register_id, sequence_date, next_sequence, updated_at)
                VALUES (gen_random_uuid(), @p0, @p1, 2, NOW())
                ON CONFLICT (cash_register_id, sequence_date) DO UPDATE SET
                    next_sequence = receipt_sequences.next_sequence + 1,
                    updated_at = NOW()
                RETURNING (next_sequence - 1)
                """;
            var p0 = cmd.CreateParameter();
            p0.ParameterName = "@p0";
            p0.Value = cashRegisterId;
            cmd.Parameters.Add(p0);
            var p1 = cmd.CreateParameter();
            p1.ParameterName = "@p1";
            p1.Value = dateOnly;
            p1.DbType = DbType.Date;
            cmd.Parameters.Add(p1);
        }

        private string ParseAllocatedResult(object? result, string registerNumber, DateTime dateOnly, Guid cashRegisterId)
        {
            var allocated = result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
            if (allocated < 1)
            {
                _logger.LogError("Receipt sequence allocation returned invalid value for CashRegisterId={Id} Date={Date}", cashRegisterId, dateOnly);
                throw new InvalidOperationException($"Receipt sequence allocation failed for CashRegisterId={cashRegisterId} Date={dateOnly:yyyyMMdd}.");
            }
            var belegNr = $"AT-{registerNumber}-{dateOnly:yyyyMMdd}-{allocated}";
            _logger.LogDebug("Allocated BelegNr {BelegNr} for CashRegisterId={Id} Date={Date}", belegNr, cashRegisterId, dateOnly);
            return belegNr;
        }
    }
}
