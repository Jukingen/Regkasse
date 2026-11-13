using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KasseAPI_Final.Services.Countries.Strategies.Germany;

public interface IDeReceiptSequenceService
{
    Task<int> AllocateNextAsync(Guid tenantId, Guid cashRegisterId, CancellationToken cancellationToken = default);
}

public sealed class DeReceiptSequenceService : IDeReceiptSequenceService
{
    private readonly AppDbContext _db;

    public DeReceiptSequenceService(AppDbContext db)
    {
        _db = db;
    }

    internal static string FormatDeBelegNr(string tenantSlug, string registerNumber, int sequence) =>
        $"DE-{tenantSlug}-{registerNumber}-{sequence}";

    public async Task<int> AllocateNextAsync(
        Guid tenantId,
        Guid cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        var conn = _db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = conn.CreateCommand();
        var current = _db.Database.CurrentTransaction;
        if (current != null)
            cmd.Transaction = current.GetDbTransaction();

        cmd.CommandText = """
            INSERT INTO de_receipt_sequences
                (id, tenant_id, cash_register_id, next_sequence, created_at, updated_at)
            VALUES (gen_random_uuid(), @tenantId, @cashRegisterId, 2, NOW(), NOW())
            ON CONFLICT (cash_register_id) DO UPDATE SET
                next_sequence = de_receipt_sequences.next_sequence + 1,
                updated_at = NOW()
            RETURNING (next_sequence - 1)
            """;

        var tenant = cmd.CreateParameter();
        tenant.ParameterName = "tenantId";
        tenant.Value = tenantId;
        cmd.Parameters.Add(tenant);

        var register = cmd.CreateParameter();
        register.ParameterName = "cashRegisterId";
        register.Value = cashRegisterId;
        cmd.Parameters.Add(register);

        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        var sequence = result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
        if (sequence < 1)
            throw new InvalidOperationException(
                $"DE receipt sequence reservation failed for cash register '{cashRegisterId}'.");
        return sequence;
    }
}
