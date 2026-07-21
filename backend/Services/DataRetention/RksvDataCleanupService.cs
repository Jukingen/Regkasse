using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.DataRetention;

/// <summary>
/// Cold-archives past-retention fiscal payments to disk and tracks them in
/// <c>rksv_cold_archive_*</c>. Live payment rows are retained for TSE/RKSV chain integrity.
/// </summary>
public sealed class RksvDataCleanupService : IRksvDataCleanupService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IOptionsMonitor<RksvDataCleanupOptions> _options;
    private readonly IHostEnvironment _env;
    private readonly ILogger<RksvDataCleanupService> _logger;

    public RksvDataCleanupService(
        IDbContextFactory<AppDbContext> dbFactory,
        IOptionsMonitor<RksvDataCleanupOptions> options,
        IHostEnvironment env,
        ILogger<RksvDataCleanupService> logger)
    {
        _dbFactory = dbFactory;
        _options = options;
        _env = env;
        _logger = logger;
    }

    public async Task<RksvColdArchiveResult> CleanupRksvDataAsync(CancellationToken ct = default)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled)
        {
            return new RksvColdArchiveResult
            {
                Message = "RksvDataCleanup is disabled (Enabled=false).",
            };
        }

        var retentionYears = Math.Max(1, opts.RetentionYears);
        var batchSize = Math.Clamp(opts.MaxBatchSize, 1, 5000);
        var cutoff = DateTime.UtcNow.AddYears(-retentionYears);

        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var alreadyArchived = db.RksvColdArchiveItems.AsNoTracking()
            .Select(i => i.PaymentDetailId);

        var eligibleQuery =
            from p in db.PaymentDetails.AsNoTracking()
            join cr in db.CashRegisters.AsNoTracking().IgnoreQueryFilters()
                on p.CashRegisterId equals cr.Id
            where p.CreatedAt < cutoff && !alreadyArchived.Contains(p.Id)
            orderby p.CreatedAt
            select new
            {
                Payment = p,
                cr.TenantId,
            };

        var eligibleCount = await eligibleQuery.CountAsync(ct).ConfigureAwait(false);
        if (eligibleCount == 0)
        {
            _logger.LogInformation(
                "RKSV cold-archive: no eligible payments (cutoff={Cutoff:o}).",
                cutoff);

            string? emptyMsg = "No payments past retention window.";
            var refuse = opts.HardDeleteEnabled;
            if (refuse)
            {
                emptyMsg +=
                    " HardDeleteEnabled is set but live RKSV payment deletion is refused " +
                    "(signature chain / DEP integrity).";
            }

            return new RksvColdArchiveResult
            {
                EligibleCount = 0,
                ArchivedCount = 0,
                Message = emptyMsg,
                HardDeleteRefused = refuse,
            };
        }

        var batch = await eligibleQuery.Take(batchSize).ToListAsync(ct).ConfigureAwait(false);
        var archiveRoot = ResolveArchiveRoot(opts.ArchiveRootRelativeDirectory);
        Directory.CreateDirectory(archiveRoot);

        var runId = Guid.NewGuid();
        var fileName = $"rksv_cold_{cutoff:yyyyMMdd}_{runId:N}.zip";
        var archivePath = Path.Combine(archiveRoot, fileName);
        var now = DateTime.UtcNow;

        try
        {
            await WriteArchiveZipAsync(archivePath, batch.Select(b => b.Payment).ToList(), cutoff, ct)
                .ConfigureAwait(false);

            string? sha256;
            await using (var fs = File.OpenRead(archivePath))
            {
                var hash = await SHA256.HashDataAsync(fs, ct).ConfigureAwait(false);
                sha256 = Convert.ToHexString(hash).ToLowerInvariant();
            }

            var run = new RksvColdArchiveRun
            {
                Id = runId,
                CreatedAtUtc = now,
                CutoffUtc = cutoff,
                ArchivePath = archivePath,
                Sha256 = sha256,
                PaymentCount = batch.Count,
                Status = RksvColdArchiveRunStatuses.Succeeded,
            };

            db.RksvColdArchiveRuns.Add(run);
            foreach (var row in batch)
            {
                db.RksvColdArchiveItems.Add(new RksvColdArchiveItem
                {
                    ArchiveRunId = runId,
                    PaymentDetailId = row.Payment.Id,
                    TenantId = row.TenantId,
                    CashRegisterId = row.Payment.CashRegisterId,
                    PaymentCreatedAtUtc = row.Payment.CreatedAt,
                    ArchivedAtUtc = now,
                });
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            _logger.LogInformation(
                "RKSV cold-archive wrote {Count} payment(s) to {Path} (sha256={Sha256}, eligible={Eligible}).",
                batch.Count,
                archivePath,
                sha256,
                eligibleCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RKSV cold-archive failed for run {RunId}.", runId);
            try
            {
                if (File.Exists(archivePath))
                    File.Delete(archivePath);
            }
            catch
            {
                // best-effort
            }

            db.RksvColdArchiveRuns.Add(new RksvColdArchiveRun
            {
                Id = runId,
                CreatedAtUtc = now,
                CutoffUtc = cutoff,
                ArchivePath = archivePath,
                PaymentCount = 0,
                Status = RksvColdArchiveRunStatuses.Failed,
                ErrorMessage = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message,
            });
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            throw;
        }

        // Optional filesystem prune of very old archive ZIP packages (not live DB rows).
        await PruneExpiredArchiveFilesAsync(db, opts, ct).ConfigureAwait(false);

        var hardDeleteRefused = false;
        string? message = null;
        if (opts.HardDeleteEnabled)
        {
            // Refuse: deleting live fiscal payments breaks TSE signature-chain continuity.
            hardDeleteRefused = true;
            message =
                "HardDeleteEnabled is set but live RKSV payment deletion is refused " +
                "(signature chain / DEP integrity). Cold-archive copies only.";
            _logger.LogWarning("{Message}", message);
        }

        return new RksvColdArchiveResult
        {
            EligibleCount = eligibleCount,
            ArchivedCount = batch.Count,
            ArchiveRunId = runId,
            ArchivePath = archivePath,
            HardDeleteRefused = hardDeleteRefused,
            Message = message,
        };
    }

    private async Task PruneExpiredArchiveFilesAsync(
        AppDbContext db,
        RksvDataCleanupOptions opts,
        CancellationToken ct)
    {
        var extraYears = Math.Max(0, opts.ExtraArchiveYears);
        var fileCutoff = DateTime.UtcNow.AddYears(-(opts.RetentionYears + extraYears));

        var oldRuns = await db.RksvColdArchiveRuns
            .Where(r => r.Status == RksvColdArchiveRunStatuses.Succeeded
                        && r.CreatedAtUtc < fileCutoff
                        && r.ArchivePath != "")
            .OrderBy(r => r.CreatedAtUtc)
            .Take(50)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var run in oldRuns)
        {
            try
            {
                if (File.Exists(run.ArchivePath))
                {
                    File.Delete(run.ArchivePath);
                    _logger.LogInformation(
                        "Pruned expired RKSV cold-archive file {Path} (run {RunId}).",
                        run.ArchivePath,
                        run.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to prune cold-archive file {Path}.", run.ArchivePath);
            }
        }
    }

    private string ResolveArchiveRoot(string configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
            configured = "App_Data/rksv-cold-archives";

        return Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(_env.ContentRootPath, configured));
    }

    private static async Task WriteArchiveZipAsync(
        string archivePath,
        IReadOnlyList<PaymentDetails> payments,
        DateTime cutoffUtc,
        CancellationToken ct)
    {
        await using var zipStream = new FileStream(
            archivePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            64 * 1024,
            FileOptions.Asynchronous);

        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true);

        var manifest = new
        {
            format = "regkasse.rksv-cold-archive.v1",
            cutoffUtc,
            archivedAtUtc = DateTime.UtcNow,
            paymentCount = payments.Count,
            note =
                "Cold copy of fiscal payments past RKSV retention. Live DB rows are retained for signature-chain integrity.",
        };

        await WriteJsonEntryAsync(zip, "manifest.json", manifest, ct).ConfigureAwait(false);
        await WriteJsonEntryAsync(zip, "payment_details.json", payments, ct).ConfigureAwait(false);
        await zipStream.FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task WriteJsonEntryAsync(
        ZipArchive zip,
        string entryName,
        object payload,
        CancellationToken ct)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, payload, JsonOptions, ct).ConfigureAwait(false);
    }
}
