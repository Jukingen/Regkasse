using System.Data;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Offline;

public sealed class SequenceReservationService : ISequenceReservationService
{
    private readonly AppDbContext _context;
    private readonly ILogger<SequenceReservationService> _logger;

    public SequenceReservationService(
        AppDbContext context,
        ILogger<SequenceReservationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<int>> ReserveSequencesAsync(
        int count,
        Guid cashRegisterId,
        CancellationToken ct = default)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be at least 1.");
        if (cashRegisterId == Guid.Empty)
            throw new ArgumentException("Cash register id is required.", nameof(cashRegisterId));

        await EnsureActiveRegisterAsync(cashRegisterId, ct).ConfigureAwait(false);

        var dateOnly = DateTime.UtcNow.Date;
        var start = await AllocateSequenceBlockAsync(cashRegisterId, dateOnly, count, ct).ConfigureAwait(false);
        var sequences = Enumerable.Range(start, count).ToList();

        _logger.LogInformation(
            "Reserved {Count} sequences for register {RegisterId}: {First}-{Last}",
            count, cashRegisterId, sequences.First(), sequences.Last());

        return sequences;
    }

    public async Task ReleaseSequencesAsync(
        List<int> sequences,
        Guid cashRegisterId,
        CancellationToken ct = default)
    {
        if (sequences == null || sequences.Count == 0)
            return;
        if (cashRegisterId == Guid.Empty)
            throw new ArgumentException("Cash register id is required.", nameof(cashRegisterId));

        var dateOnly = DateTime.UtcNow.Date;
        var row = await _context.ReceiptSequences
            .FirstOrDefaultAsync(
                r => r.CashRegisterId == cashRegisterId && r.SequenceDate == dateOnly,
                ct)
            .ConfigureAwait(false);

        if (row == null)
        {
            _logger.LogWarning(
                "ReleaseSequences skipped — no receipt_sequences row for register {RegisterId} on {Date}.",
                cashRegisterId, dateOnly);
            return;
        }

        var released = 0;
        foreach (var sequence in sequences.Where(s => s >= 1).Distinct().OrderByDescending(s => s))
        {
            if (row.NextSequence != sequence + 1)
            {
                _logger.LogDebug(
                    "Skipping release of sequence {Sequence} for register {RegisterId}; next_sequence is {Next}.",
                    sequence, cashRegisterId, row.NextSequence);
                continue;
            }

            row.NextSequence = sequence;
            row.UpdatedAt = DateTime.UtcNow;
            released++;
        }

        if (released == 0)
            return;

        await _context.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Released {Count} tail sequence(s) for register {RegisterId} on {Date}.",
            released, cashRegisterId, dateOnly);
    }

    public async Task<bool> IsSequenceAvailableAsync(
        int sequenceNumber,
        Guid cashRegisterId,
        CancellationToken ct = default)
    {
        if (sequenceNumber < 1 || cashRegisterId == Guid.Empty)
            return false;

        var register = await _context.CashRegisters
            .AsNoTracking()
            .Where(r => r.Id == cashRegisterId && r.IsActive)
            .Select(r => new { r.RegisterNumber })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (register == null || string.IsNullOrWhiteSpace(register.RegisterNumber))
            return false;

        var dateOnly = DateTime.UtcNow.Date;
        var belegNr = FormatBelegNr(register.RegisterNumber, dateOnly, sequenceNumber);

        var taken = await _context.PaymentDetails
            .AsNoTracking()
            .AnyAsync(p => p.ReceiptNumber == belegNr && p.IsActive, ct)
            .ConfigureAwait(false);

        return !taken;
    }

    public async Task<string> ToBelegNrAsync(
        Guid cashRegisterId,
        int sequenceNumber,
        CancellationToken ct = default)
    {
        if (sequenceNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(sequenceNumber), "Sequence must be at least 1.");

        var registerNumber = await GetRegisterNumberAsync(cashRegisterId, ct).ConfigureAwait(false);
        return FormatBelegNr(registerNumber, DateTime.UtcNow.Date, sequenceNumber);
    }

    public async Task<string> ReserveNextReceiptNumberAsync(
        Guid cashRegisterId,
        CancellationToken ct = default,
        int maxAttempts = 3)
    {
        var attempts = Math.Clamp(maxAttempts, 1, 10);
        Exception? lastError = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            List<int>? reserved = null;

            try
            {
                reserved = await ReserveSequencesAsync(1, cashRegisterId, ct).ConfigureAwait(false);
                var belegNr = await ToBelegNrAsync(cashRegisterId, reserved[0], ct).ConfigureAwait(false);

                if (await IsSequenceAvailableAsync(reserved[0], cashRegisterId, ct).ConfigureAwait(false))
                    return belegNr;

                _logger.LogWarning(
                    "Reserved BelegNr {BelegNr} already exists; releasing and retrying (attempt {Attempt}/{Max}).",
                    belegNr, attempt, attempts);

                await ReleaseSequencesAsync(reserved, cashRegisterId, ct).ConfigureAwait(false);
                reserved = null;
            }
            catch (Exception ex)
            {
                lastError = ex;
                if (reserved is { Count: > 0 })
                {
                    try
                    {
                        await ReleaseSequencesAsync(reserved, cashRegisterId, ct).ConfigureAwait(false);
                    }
                    catch (Exception releaseEx)
                    {
                        _logger.LogWarning(
                            releaseEx,
                            "Failed to release sequence after reservation error for register {RegisterId}.",
                            cashRegisterId);
                    }
                }

                _logger.LogWarning(
                    ex,
                    "Receipt sequence reservation failed for CashRegisterId={CashRegisterId} (attempt {Attempt}/{Max}).",
                    cashRegisterId, attempt, attempts);
            }
        }

        throw new InvalidOperationException(
            $"Could not reserve receipt number for cash register '{cashRegisterId}' after {attempts} attempt(s).",
            lastError);
    }

    internal static string FormatBelegNr(string registerNumber, DateTime sequenceDate, int sequenceNumber) =>
        $"AT-{registerNumber}-{sequenceDate:yyyyMMdd}-{sequenceNumber}";

    private async Task<int> AllocateSequenceBlockAsync(
        Guid cashRegisterId,
        DateTime dateOnly,
        int count,
        CancellationToken ct)
    {
        var conn = _context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct).ConfigureAwait(false);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO receipt_sequences (id, cash_register_id, sequence_date, next_sequence, updated_at)
            VALUES (gen_random_uuid(), @p0, @p1, @p2 + 1, NOW())
            ON CONFLICT (cash_register_id, sequence_date) DO UPDATE SET
                next_sequence = receipt_sequences.next_sequence + @p2,
                updated_at = NOW()
            RETURNING (next_sequence - @p2)
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

        var p2 = cmd.CreateParameter();
        p2.ParameterName = "@p2";
        p2.Value = count;
        cmd.Parameters.Add(p2);

        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        var start = result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
        if (start < 1)
        {
            _logger.LogError(
                "Batch sequence reservation returned invalid start for CashRegisterId={Id} Date={Date} Count={Count}",
                cashRegisterId, dateOnly, count);
            throw new InvalidOperationException(
                $"Batch sequence reservation failed for CashRegisterId={cashRegisterId} Date={dateOnly:yyyyMMdd}.");
        }

        return start;
    }

    private async Task EnsureActiveRegisterAsync(Guid cashRegisterId, CancellationToken ct)
    {
        var exists = await _context.CashRegisters
            .AsNoTracking()
            .AnyAsync(r => r.Id == cashRegisterId && r.IsActive, ct)
            .ConfigureAwait(false);

        if (!exists)
            throw new KeyNotFoundException($"Active cash register '{cashRegisterId}' was not found.");
    }

    private async Task<string> GetRegisterNumberAsync(Guid cashRegisterId, CancellationToken ct)
    {
        var register = await _context.CashRegisters
            .AsNoTracking()
            .Where(r => r.Id == cashRegisterId && r.IsActive)
            .Select(r => r.RegisterNumber)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(register))
            throw new KeyNotFoundException($"Active cash register '{cashRegisterId}' was not found.");

        return register;
    }
}
