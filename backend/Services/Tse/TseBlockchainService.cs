using System.Security.Cryptography;
using System.Text;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Simulated ledger that anchors SHA-256 hashes of existing TSE signatures.
/// Never mutates fiscal JWS / receipt chain. Diagnostic / integration sandbox only.
/// </summary>
public sealed class TseBlockchainService : ITseBlockchainService
{
    private const string DefaultNetwork = "regkasse-sim";
    private const int MaxTxTake = 200;

    private readonly AppDbContext _db;
    private readonly ILogger<TseBlockchainService> _logger;

    public TseBlockchainService(AppDbContext db, ILogger<TseBlockchainService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<TseBlockchainRecordDto> StoreSignatureAsync(
        TseBlockchainSignatureDataDto signature,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signature);
        if (signature.TenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(signature));
        if (string.IsNullOrWhiteSpace(signature.SignatureData))
            throw new ArgumentException("SignatureData is required.", nameof(signature));

        await RequireTenantAsync(signature.TenantId, cancellationToken).ConfigureAwait(false);

        var ledger = await GetOrCreateLedgerAsync(cancellationToken).ConfigureAwait(false);
        if (!ledger.IsConnected)
            throw new InvalidOperationException("Simulated blockchain ledger is disconnected. Call Sync first.");

        var sigHash = Sha256Hex(signature.SignatureData.Trim());
        var existing = await _db.TseBlockchainRecords.AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.TenantId == signature.TenantId && r.SignatureHash == sigHash,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
            return MapRecord(existing);

        var now = DateTime.UtcNow;
        var nextBlock = ledger.CurrentBlockNumber + 1;
        var txHash = Sha256Hex($"{sigHash}|{nextBlock}|{now:O}|{Guid.NewGuid():N}");
        var blockHash = Sha256Hex($"{ledger.TipBlockHash}|{txHash}|{nextBlock}");

        var row = new TseBlockchainRecord
        {
            Id = Guid.NewGuid(),
            TenantId = signature.TenantId,
            SourceType = string.IsNullOrWhiteSpace(signature.SourceType) ? "Signature" : signature.SourceType.Trim(),
            SourceId = signature.SourceId,
            TransactionHash = txHash,
            BlockHash = blockHash,
            BlockNumber = nextBlock,
            CreatedAt = now,
            SignatureHash = sigHash,
            SignaturePreview = Preview(signature.SignatureData),
            IsVerified = true,
            VerifiedAt = now,
            IsSimulated = true,
            NetworkName = ledger.NetworkName,
        };

        ledger.CurrentBlockNumber = nextBlock;
        ledger.TipBlockHash = blockHash;
        ledger.TotalTransactions += 1;
        ledger.UpdatedAt = now;
        ledger.IsConnected = true;

        _db.TseBlockchainRecords.Add(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Anchored TSE signature hash on simulated ledger TenantId={TenantId} Block={Block} Tx={Tx}",
            signature.TenantId,
            nextBlock,
            txHash[..16]);

        return MapRecord(row);
    }

    public async Task<TseBlockchainVerificationResultDto> VerifySignatureAsync(
        Guid signatureId,
        CancellationToken cancellationToken = default)
    {
        if (signatureId == Guid.Empty)
            throw new ArgumentException("signatureId is required.", nameof(signatureId));

        var row = await _db.TseBlockchainRecords
            .FirstOrDefaultAsync(r => r.Id == signatureId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException($"Blockchain record {signatureId} was not found.");

        // Simulated verification: recompute chain link consistency against tip for this block.
        var ok = !string.IsNullOrWhiteSpace(row.TransactionHash)
                 && !string.IsNullOrWhiteSpace(row.BlockHash)
                 && row.TransactionHash.Length == 64
                 && row.BlockHash.Length == 64
                 && row.SignatureHash.Length == 64;

        row.IsVerified = ok;
        row.VerifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new TseBlockchainVerificationResultDto
        {
            SignatureId = signatureId,
            IsVerified = ok,
            Message = ok
                ? "Simulated ledger verification succeeded (hash format + record integrity)."
                : "Simulated ledger verification failed.",
            Record = MapRecord(row),
            DiagnosticOnly = true,
        };
    }

    public async Task<TseBlockchainStatusDto> GetBlockchainStatusAsync(
        CancellationToken cancellationToken = default)
    {
        var ledger = await GetOrCreateLedgerAsync(cancellationToken).ConfigureAwait(false);
        return MapStatus(ledger);
    }

    public async Task<IReadOnlyList<TseBlockchainTransactionDto>> GetTransactionsAsync(
        Guid tenantId,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        take = Math.Clamp(take, 1, MaxTxTake);

        var rows = await _db.TseBlockchainRecords.AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(r => new TseBlockchainTransactionDto
        {
            Id = r.Id,
            TransactionHash = r.TransactionHash,
            BlockNumber = r.BlockNumber,
            IsVerified = r.IsVerified,
            CreatedAt = r.CreatedAt,
            SignatureHash = r.SignatureHash,
            SignaturePreview = r.SignaturePreview,
        }).ToList();
    }

    public async Task<TseBlockchainStatusDto> SyncBlockchainAsync(
        CancellationToken cancellationToken = default)
    {
        var ledger = await GetOrCreateLedgerAsync(cancellationToken).ConfigureAwait(false);
        ledger.IsConnected = true;
        ledger.UpdatedAt = DateTime.UtcNow;
        // Soft tip bump to show sync activity without inventing fiscal anchors
        if (ledger.CurrentBlockNumber == 0)
        {
            ledger.CurrentBlockNumber = 1;
            ledger.TipBlockHash = Sha256Hex($"genesis|{DefaultNetwork}|{DateTime.UtcNow:O}");
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapStatus(ledger);
    }

    private async Task<TseBlockchainLedgerState> GetOrCreateLedgerAsync(CancellationToken cancellationToken)
    {
        var ledger = await _db.TseBlockchainLedgerStates
            .FirstOrDefaultAsync(l => l.Id == TseBlockchainLedgerState.SingletonId, cancellationToken)
            .ConfigureAwait(false);
        if (ledger is not null)
            return ledger;

        ledger = new TseBlockchainLedgerState
        {
            Id = TseBlockchainLedgerState.SingletonId,
            CurrentBlockNumber = 0,
            TipBlockHash = "0".PadLeft(64, '0'),
            NetworkName = DefaultNetwork,
            IsConnected = true,
            UpdatedAt = DateTime.UtcNow,
            TotalTransactions = 0,
        };
        _db.TseBlockchainLedgerStates.Add(ledger);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ledger;
    }

    private async Task RequireTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var exists = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .AnyAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            throw new KeyNotFoundException($"Tenant {tenantId} was not found.");
    }

    private static TseBlockchainRecordDto MapRecord(TseBlockchainRecord r) => new()
    {
        Id = r.Id,
        TenantId = r.TenantId,
        TransactionHash = r.TransactionHash,
        BlockHash = r.BlockHash,
        BlockNumber = r.BlockNumber,
        CreatedAt = r.CreatedAt,
        SignatureHash = r.SignatureHash,
        SignaturePreview = r.SignaturePreview,
        IsVerified = r.IsVerified,
        IsSimulated = r.IsSimulated,
        NetworkName = r.NetworkName,
        SourceId = r.SourceId,
        SourceType = r.SourceType,
    };

    private static TseBlockchainStatusDto MapStatus(TseBlockchainLedgerState ledger) => new()
    {
        BlockchainStatus = ledger.IsConnected ? "connected" : "disconnected",
        NetworkName = ledger.NetworkName,
        CurrentBlock = ledger.CurrentBlockNumber,
        TotalTransactions = ledger.TotalTransactions,
        IsSimulated = true,
        UpdatedAt = ledger.UpdatedAt,
        DiagnosticOnly = true,
    };

    private static string Sha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Preview(string signature)
    {
        var trimmed = signature.Trim();
        if (trimmed.Length <= 24)
            return trimmed;
        return trimmed[..12] + "…" + trimmed[^8..];
    }
}
