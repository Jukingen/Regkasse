using System.Security.Cryptography;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Regkasse.LicenseTools;

namespace KasseAPI_Final.Services;

/// <summary>Inputs for issuing a new license from the admin panel.</summary>
public sealed record GenerateLicenseRequest(
    string CustomerName,
    DateTime ExpiryDateUtc,
    bool RequireFingerprint,
    string? MachineHashHex);

/// <summary>Result returned to the admin caller.</summary>
public sealed record GenerateLicenseResult(
    bool Success,
    string? LicenseKey,
    string? SignedJwt,
    DateTime? ExpiryAtUtc,
    string? Message);

/// <summary>Renew an existing issuance row or key: new JWT + REGK row; prior row revoked for audit.</summary>
public sealed record RenewLicenseCommand(
    string? LicenseKey,
    Guid? IssuedLicenseId,
    DateTime NewExpiryDateUtc);

/// <summary>
/// Supersede: new row + JWT; previous row stays non-revoked and points to the new id. Exactly one of
/// <see cref="UpgradeLicenseCommand.LicenseKey"/> or <see cref="UpgradeLicenseCommand.IssuedLicenseId"/>.
/// </summary>
public sealed record UpgradeLicenseCommand(
    string? LicenseKey,
    Guid? IssuedLicenseId,
    DateTime NewExpiryDateUtc,
    string? Reason);

/// <summary>Transfer machine binding to a new server hash; new REGK row + JWT, same expiry and customer.</summary>
public sealed record TransferLicenseCommand(
    string LicenseKey,
    string NewMachineHashHex,
    string? Reason);

/// <summary>POS-facing transfer eligibility (no secrets).</summary>
public sealed record LicenseTransferRequestInfoResult(
    bool Eligible,
    string Message,
    string? CustomerNameMasked,
    DateTime? ExpiryAtUtc,
    bool NewServerRequiresMachineFingerprint,
    string LicenseKeyMasked);

/// <summary>Suggestion payload for GET renewal-info.</summary>
public sealed record LicenseRenewalInfoResult(
    DateTime OriginalExpiryAtUtc,
    string SuggestedNewExpiryDate);

public interface ILicenseIssuanceService
{
    /// <summary>
    /// Issues a new license. Returns <c>Success=false</c> with an English message on validation failures
    /// (UI translates). Throws <see cref="LicenseIssuanceUnavailableException"/> when no signing key is configured.
    /// </summary>
    Task<GenerateLicenseResult> IssueAsync(
        GenerateLicenseRequest request,
        string? issuedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a replacement license (new key + JWT), revokes the previous row. Exactly one of
    /// <see cref="RenewLicenseCommand.LicenseKey"/> or <see cref="RenewLicenseCommand.IssuedLicenseId"/> must be set.
    /// </summary>
    Task<GenerateLicenseResult> RenewAsync(
        RenewLicenseCommand command,
        string? renewedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upgrade / supersede chain: rejects if requested expiry is before the stored expiry instant, or row is revoked/superseded.
    /// </summary>
    Task<GenerateLicenseResult> UpgradeAsync(
        UpgradeLicenseCommand command,
        string? upgradedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns null when no row matches <paramref name="licenseKey"/>.</summary>
    Task<LicenseRenewalInfoResult?> GetRenewalInfoAsync(
        string licenseKey,
        CancellationToken cancellationToken = default);

    /// <summary>Ensures the key is transferable and returns masking hints for POS.</summary>
    Task<LicenseTransferRequestInfoResult?> GetTransferRequestInfoAsync(
        string licenseKey,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a replacement row bound to <paramref name="command"/>.NewMachineHashHex.</summary>
    Task<GenerateLicenseResult> TransferAsync(
        TransferLicenseCommand command,
        string? transferredByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Thrown when the deployment is not configured to issue licenses (no <c>License:SigningPrivateKeyPem</c>).
/// Controller maps this to HTTP 503 — the feature is intentionally disabled on customer POS hosts.
/// </summary>
public sealed class LicenseIssuanceUnavailableException : Exception
{
    public LicenseIssuanceUnavailableException(string message) : base(message) { }
}

public sealed class LicenseIssuanceService : ILicenseIssuanceService
{
    private const int MaxCustomerNameLength = 256;

    private readonly IOptions<LicenseOptions> _options;
    private readonly AppDbContext _db;
    private readonly ILogger<LicenseIssuanceService> _logger;

    public LicenseIssuanceService(
        IOptions<LicenseOptions> options,
        AppDbContext db,
        ILogger<LicenseIssuanceService> logger)
    {
        _options = options;
        _db = db;
        _logger = logger;
    }

    public async Task<GenerateLicenseResult> IssueAsync(
        GenerateLicenseRequest request,
        string? issuedByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1) Validate inputs (server-side, mirroring frontend validation but authoritative).
        var validationError = ValidateRequest(request);
        if (validationError is not null)
            return new GenerateLicenseResult(false, null, null, null, validationError);

        // 2) Load signing key from configuration. Absent → feature disabled on this host.
        var pem = _options.Value.SigningPrivateKeyPem?.Trim();
        if (string.IsNullOrWhiteSpace(pem))
        {
            throw new LicenseIssuanceUnavailableException(
                "License issuance is disabled on this host (License:SigningPrivateKeyPem is not configured).");
        }

        using var rsa = LoadRsaPrivateKey(pem);

        // 3) Sign + encode the license via the existing LicenseIssuer (REGK key + RS256 JWT).
        var customer = request.CustomerName.Trim();
        var machineHash = request.RequireFingerprint
            ? request.MachineHashHex?.Trim().ToLowerInvariant()
            : null; // floating
        var expiresAt = new DateTimeOffset(DateTime.SpecifyKind(request.ExpiryDateUtc, DateTimeKind.Utc), TimeSpan.Zero);

        LicenseIssueResult issued;
        try
        {
            issued = LicenseIssuer.Issue(
                customerName: customer,
                machineHashHex: machineHash,
                expiresAtUtc: expiresAt,
                privateKey: rsa);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "License issuance failed during signing for customer={Customer} expires={Expires:o}",
                customer, expiresAt);
            return new GenerateLicenseResult(false, null, null, null, "Failed to sign the license. See server logs.");
        }

        // 4) Persist audit row.
        var entity = new IssuedLicense
        {
            Id = Guid.NewGuid(),
            LicenseKey = issued.LicenseKey,
            CustomerName = customer,
            ExpiryAtUtc = issued.ExpiresAtUtc.UtcDateTime,
            RequireFingerprint = request.RequireFingerprint,
            MachineHashHex = string.IsNullOrEmpty(machineHash) ? null : machineHash,
            SignedJwt = issued.SignedPayload,
            IssuedAtUtc = DateTime.UtcNow,
            IssuedByUserId = string.IsNullOrWhiteSpace(issuedByUserId) ? null : issuedByUserId,
            IsRevoked = false,
        };

        _db.IssuedLicenses.Add(entity);
        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            // Extremely unlikely (REGK collision needs SHA-256 prefix collision), but recoverable per-call.
            _logger.LogError(ex, "License issuance: DB persistence failed for licenseKey={LicenseKey}", issued.LicenseKey);
            return new GenerateLicenseResult(false, null, null, null, "Failed to persist the issued license.");
        }

        _logger.LogInformation(
            "License issued: customer=\"{Customer}\" expires={Expires:o} requireFingerprint={ReqFp} keyPrefix={Prefix} issuedBy={UserId}",
            customer,
            issued.ExpiresAtUtc,
            request.RequireFingerprint,
            SafePrefix(issued.LicenseKey),
            issuedByUserId ?? "(system)");

        return new GenerateLicenseResult(
            Success: true,
            LicenseKey: issued.LicenseKey,
            SignedJwt: issued.SignedPayload,
            ExpiryAtUtc: issued.ExpiresAtUtc.UtcDateTime,
            Message: null);
    }

    public async Task<GenerateLicenseResult> RenewAsync(
        RenewLicenseCommand command,
        string? renewedByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var hasId = command.IssuedLicenseId.HasValue && command.IssuedLicenseId.Value != Guid.Empty;
        var hasKey = !string.IsNullOrWhiteSpace(command.LicenseKey);
        if (hasId == hasKey)
        {
            return new GenerateLicenseResult(
                false, null, null, null,
                "Provide exactly one of licenseKey or issuedLicenseId.");
        }

        await using var tx = await _db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            IssuedLicense? old;
            if (hasId)
            {
                var rowId = command.IssuedLicenseId!.Value;
                old = await _db.IssuedLicenses
                    .FirstOrDefaultAsync(il => il.Id == rowId, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                var nk = command.LicenseKey!.Trim();
                old = await _db.IssuedLicenses
                    .FirstOrDefaultAsync(il => EF.Functions.ILike(il.LicenseKey, nk), cancellationToken)
                    .ConfigureAwait(false);
            }

            if (old is null)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(false, null, null, null, "No issued license matches this key or id.");
            }

            if (old.IsRevoked)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(false, null, null, null, "Cannot renew a revoked license.");
            }

            if (old.SupersededByLicenseId is not null)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(false, null, null, null, "Cannot renew a superseded license.");
            }

            if (old.TransferredToLicenseId is not null)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(false, null, null, null, "Cannot renew a transferred license.");
            }

            var newExpiryUtc = DateTime.SpecifyKind(command.NewExpiryDateUtc, DateTimeKind.Utc);
            if (newExpiryUtc <= DateTime.UtcNow)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(false, null, null, null, "newExpiryDate must be in the future (UTC).");
            }

            if (old.RequireFingerprint && string.IsNullOrWhiteSpace(old.MachineHashHex))
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(
                    false, null, null, null,
                    "Bound license row is missing machineHashHex; cannot renew.");
            }

            var genReq = new GenerateLicenseRequest(
                CustomerName: old.CustomerName,
                ExpiryDateUtc: newExpiryUtc,
                RequireFingerprint: old.RequireFingerprint,
                MachineHashHex: old.MachineHashHex);

            var validationError = ValidateRequest(genReq);
            if (validationError is not null)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(false, null, null, null, validationError);
            }

            var pem = _options.Value.SigningPrivateKeyPem?.Trim();
            if (string.IsNullOrWhiteSpace(pem))
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw new LicenseIssuanceUnavailableException(
                    "License issuance is disabled on this host (License:SigningPrivateKeyPem is not configured).");
            }

            using var rsa = LoadRsaPrivateKey(pem);

            var customer = genReq.CustomerName.Trim();
            var machineHash = genReq.RequireFingerprint
                ? genReq.MachineHashHex?.Trim().ToLowerInvariant()
                : null;
            var expiresAt = new DateTimeOffset(newExpiryUtc, TimeSpan.Zero);

            LicenseIssueResult issued;
            try
            {
                issued = LicenseIssuer.Issue(
                    customerName: customer,
                    machineHashHex: machineHash,
                    expiresAtUtc: expiresAt,
                    privateKey: rsa);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "License renewal failed during signing for customer={Customer} expires={Expires:o}",
                    customer, expiresAt);
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(false, null, null, null, "Failed to sign the renewed license. See server logs.");
            }

            var newEntity = new IssuedLicense
            {
                Id = Guid.NewGuid(),
                LicenseKey = issued.LicenseKey,
                CustomerName = customer,
                ExpiryAtUtc = issued.ExpiresAtUtc.UtcDateTime,
                RequireFingerprint = old.RequireFingerprint,
                MachineHashHex = string.IsNullOrEmpty(machineHash) ? null : machineHash,
                SignedJwt = issued.SignedPayload,
                IssuedAtUtc = DateTime.UtcNow,
                IssuedByUserId = string.IsNullOrWhiteSpace(renewedByUserId) ? null : renewedByUserId,
                IsRevoked = false,
            };

            _db.IssuedLicenses.Add(newEntity);

            old.IsRevoked = true;
            old.RevokedAtUtc = DateTime.UtcNow;
            old.RevokedByUserId = string.IsNullOrWhiteSpace(renewedByUserId) ? null : renewedByUserId;
            old.RevocationReason = "Superseded by renewal.";

            try
            {
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "License renewal: DB persistence failed for newKey={LicenseKey}", issued.LicenseKey);
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(false, null, null, null, "Failed to persist the renewed license.");
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "License renewed: customer=\"{Customer}\" oldKeyPrefix={OldPrefix} newKeyPrefix={NewPrefix} renewedBy={UserId}",
                customer,
                SafePrefix(old.LicenseKey),
                SafePrefix(issued.LicenseKey),
                renewedByUserId ?? "(system)");

            return new GenerateLicenseResult(
                Success: true,
                LicenseKey: issued.LicenseKey,
                SignedJwt: issued.SignedPayload,
                ExpiryAtUtc: issued.ExpiresAtUtc.UtcDateTime,
                Message: null);
        }
        catch (LicenseIssuanceUnavailableException)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<GenerateLicenseResult> UpgradeAsync(
        UpgradeLicenseCommand command,
        string? upgradedByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var hasId = command.IssuedLicenseId.HasValue && command.IssuedLicenseId.Value != Guid.Empty;
        var hasKey = !string.IsNullOrWhiteSpace(command.LicenseKey);
        if (hasId == hasKey)
        {
            return new GenerateLicenseResult(
                false, null, null, null,
                "Provide exactly one of licenseKey or issuedLicenseId.");
        }

        await using var tx = await _db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            IssuedLicense? old;
            if (hasId)
            {
                var rid = command.IssuedLicenseId!.Value;
                old = await _db.IssuedLicenses
                    .FirstOrDefaultAsync(il => il.Id == rid, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                var nk = command.LicenseKey!.Trim();
                old = await _db.IssuedLicenses
                    .FirstOrDefaultAsync(il => EF.Functions.ILike(il.LicenseKey, nk), cancellationToken)
                    .ConfigureAwait(false);
            }

            if (old is null)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(false, null, null, null, "No issued license matches this key or id.");
            }

            if (old.IsRevoked)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(false, null, null, null, "Cannot upgrade a revoked license.");
            }

            if (old.SupersededByLicenseId is not null)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(false, null, null, null, "This license was already superseded.");
            }

            if (old.TransferredToLicenseId is not null)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(false, null, null, null, "This license was transferred to another machine binding.");
            }

            var newExpiryUtc = DateTime.SpecifyKind(command.NewExpiryDateUtc, DateTimeKind.Utc);
            if (newExpiryUtc <= DateTime.UtcNow)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(false, null, null, null, "newExpiryDate must be in the future (UTC).");
            }

            if (old.ExpiryAtUtc > newExpiryUtc)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(
                    false, null, null, null,
                    "newExpiryDate must be on or after the current license expiry.");
            }

            if (old.RequireFingerprint && string.IsNullOrWhiteSpace(old.MachineHashHex))
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(
                    false, null, null, null,
                    "Bound license row is missing machineHashHex; cannot upgrade.");
            }

            var genReq = new GenerateLicenseRequest(
                CustomerName: old.CustomerName,
                ExpiryDateUtc: newExpiryUtc,
                RequireFingerprint: old.RequireFingerprint,
                MachineHashHex: old.MachineHashHex);

            var validationError = ValidateRequest(genReq);
            if (validationError is not null)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(false, null, null, null, validationError);
            }

            var pem = _options.Value.SigningPrivateKeyPem?.Trim();
            if (string.IsNullOrWhiteSpace(pem))
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw new LicenseIssuanceUnavailableException(
                    "License issuance is disabled on this host (License:SigningPrivateKeyPem is not configured).");
            }

            using var rsa = LoadRsaPrivateKey(pem);

            var customer = genReq.CustomerName.Trim();
            var machineHash = genReq.RequireFingerprint
                ? genReq.MachineHashHex?.Trim().ToLowerInvariant()
                : null;
            var expiresAt = new DateTimeOffset(newExpiryUtc, TimeSpan.Zero);

            LicenseIssueResult issued;
            try
            {
                issued = LicenseIssuer.Issue(
                    customerName: customer,
                    machineHashHex: machineHash,
                    expiresAtUtc: expiresAt,
                    privateKey: rsa);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "License upgrade failed during signing for customer={Customer} expires={Expires:o}",
                    customer, expiresAt);
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(false, null, null, null, "Failed to sign the upgraded license. See server logs.");
            }

            var newEntity = new IssuedLicense
            {
                Id = Guid.NewGuid(),
                LicenseKey = issued.LicenseKey,
                CustomerName = customer,
                ExpiryAtUtc = issued.ExpiresAtUtc.UtcDateTime,
                RequireFingerprint = old.RequireFingerprint,
                MachineHashHex = string.IsNullOrEmpty(machineHash) ? null : machineHash,
                SignedJwt = issued.SignedPayload,
                IssuedAtUtc = DateTime.UtcNow,
                IssuedByUserId = string.IsNullOrWhiteSpace(upgradedByUserId) ? null : upgradedByUserId,
                IsRevoked = false,
            };

            _db.IssuedLicenses.Add(newEntity);
            old.SupersededByLicenseId = newEntity.Id;

            try
            {
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "License upgrade: DB persistence failed for newKey={LicenseKey}", issued.LicenseKey);
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(false, null, null, null, "Failed to persist the upgraded license.");
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

            var reasonLog = string.IsNullOrWhiteSpace(command.Reason) ? "(none)" : command.Reason.Trim();
            _logger.LogInformation(
                "License upgraded: customer=\"{Customer}\" oldKeyPrefix={OldPrefix} newKeyPrefix={NewPrefix} upgradedBy={UserId} reason={Reason}",
                customer,
                SafePrefix(old.LicenseKey),
                SafePrefix(issued.LicenseKey),
                upgradedByUserId ?? "(system)",
                reasonLog);

            return new GenerateLicenseResult(
                Success: true,
                LicenseKey: issued.LicenseKey,
                SignedJwt: issued.SignedPayload,
                ExpiryAtUtc: issued.ExpiresAtUtc.UtcDateTime,
                Message: null);
        }
        catch (LicenseIssuanceUnavailableException)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<LicenseTransferRequestInfoResult?> GetTransferRequestInfoAsync(
        string licenseKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return null;

        var nk = licenseKey.Trim();
        var row = await _db.IssuedLicenses
            .AsNoTracking()
            .FirstOrDefaultAsync(il => EF.Functions.ILike(il.LicenseKey, nk), cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
            return null;

        var utcNow = DateTime.UtcNow;
        var expiry = DateTime.SpecifyKind(row.ExpiryAtUtc, DateTimeKind.Utc);
        var maskedKey = MaskIssuedLicenseDisplayKey(row.LicenseKey);
        var maskedCustomer = MaskCustomerName(row.CustomerName);

        if (row.IsRevoked)
        {
            return new LicenseTransferRequestInfoResult(
                false,
                "This license is revoked.",
                maskedCustomer,
                expiry,
                NewServerRequiresMachineFingerprint: true,
                maskedKey);
        }

        if (row.SupersededByLicenseId is not null)
        {
            return new LicenseTransferRequestInfoResult(
                false,
                "This license was superseded.",
                maskedCustomer,
                expiry,
                NewServerRequiresMachineFingerprint: true,
                maskedKey);
        }

        if (row.TransferredToLicenseId is not null)
        {
            return new LicenseTransferRequestInfoResult(
                false,
                "This license was already transferred.",
                maskedCustomer,
                expiry,
                NewServerRequiresMachineFingerprint: true,
                maskedKey);
        }

        if (row.ExpiryAtUtc <= utcNow)
        {
            return new LicenseTransferRequestInfoResult(
                false,
                "This license has expired.",
                maskedCustomer,
                expiry,
                NewServerRequiresMachineFingerprint: true,
                maskedKey);
        }

        return new LicenseTransferRequestInfoResult(
            true,
            "Eligible for transfer. Send support this device's machine fingerprint (SHA-256 hex) together with your license key.",
            maskedCustomer,
            expiry,
            NewServerRequiresMachineFingerprint: true,
            maskedKey);
    }

    public async Task<GenerateLicenseResult> TransferAsync(
        TransferLicenseCommand command,
        string? transferredByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.LicenseKey))
            return new GenerateLicenseResult(false, null, null, null, "licenseKey is required.");

        var rawNewHash = command.NewMachineHashHex?.Trim();
        if (string.IsNullOrEmpty(rawNewHash) || rawNewHash.Length != 64 || !IsHex(rawNewHash))
        {
            return new GenerateLicenseResult(
                false,
                null,
                null,
                null,
                "newMachineHashHex must be a 64-character SHA-256 hex digest.");
        }

        var newHashNorm = rawNewHash.ToLowerInvariant();

        await using var tx = await _db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var nk = command.LicenseKey.Trim();
            var old = await _db.IssuedLicenses
                .FirstOrDefaultAsync(il => EF.Functions.ILike(il.LicenseKey, nk), cancellationToken)
                .ConfigureAwait(false);

            if (old is null)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(false, null, null, null, "No issued license matches this license key.");
            }

            if (old.IsRevoked)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(false, null, null, null, "Cannot transfer a revoked license.");
            }

            if (old.SupersededByLicenseId is not null)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(false, null, null, null, "Cannot transfer a superseded license.");
            }

            if (old.TransferredToLicenseId is not null)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(false, null, null, null, "This license was already transferred.");
            }

            if (old.ExpiryAtUtc <= DateTime.UtcNow)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(false, null, null, null, "Cannot transfer an expired license.");
            }

            if (!string.IsNullOrWhiteSpace(old.MachineHashHex)
                && string.Equals(old.MachineHashHex, newHashNorm, StringComparison.OrdinalIgnoreCase))
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(false, null, null, null, "newMachineHashHex must differ from the current binding.");
            }

            var expiryUtc = DateTime.SpecifyKind(old.ExpiryAtUtc, DateTimeKind.Utc);
            var genReq = new GenerateLicenseRequest(
                CustomerName: old.CustomerName,
                ExpiryDateUtc: expiryUtc,
                RequireFingerprint: true,
                MachineHashHex: newHashNorm);

            var validationError = ValidateRequest(genReq);
            if (validationError is not null)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(false, null, null, null, validationError);
            }

            var pem = _options.Value.SigningPrivateKeyPem?.Trim();
            if (string.IsNullOrWhiteSpace(pem))
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw new LicenseIssuanceUnavailableException(
                    "License issuance is disabled on this host (License:SigningPrivateKeyPem is not configured).");
            }

            using var rsa = LoadRsaPrivateKey(pem);

            var customer = genReq.CustomerName.Trim();
            var expiresAt = new DateTimeOffset(expiryUtc, TimeSpan.Zero);

            LicenseIssueResult issued;
            try
            {
                issued = LicenseIssuer.Issue(
                    customerName: customer,
                    machineHashHex: newHashNorm,
                    expiresAtUtc: expiresAt,
                    privateKey: rsa);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "License transfer failed during signing for customer={Customer} expires={Expires:o}",
                    customer, expiresAt);
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(false, null, null, null, "Failed to sign the transferred license. See server logs.");
            }

            var newEntity = new IssuedLicense
            {
                Id = Guid.NewGuid(),
                LicenseKey = issued.LicenseKey,
                CustomerName = customer,
                ExpiryAtUtc = issued.ExpiresAtUtc.UtcDateTime,
                RequireFingerprint = true,
                MachineHashHex = newHashNorm,
                SignedJwt = issued.SignedPayload,
                IssuedAtUtc = DateTime.UtcNow,
                IssuedByUserId = string.IsNullOrWhiteSpace(transferredByUserId) ? null : transferredByUserId,
                IsRevoked = false,
            };

            _db.IssuedLicenses.Add(newEntity);
            old.TransferredToLicenseId = newEntity.Id;

            try
            {
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "License transfer: DB persistence failed for newKey={LicenseKey}", issued.LicenseKey);
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new GenerateLicenseResult(false, null, null, null, "Failed to persist the transferred license.");
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

            var reasonLog = string.IsNullOrWhiteSpace(command.Reason) ? "(none)" : command.Reason.Trim();
            _logger.LogInformation(
                "License transferred: customer=\"{Customer}\" oldKeyPrefix={OldPrefix} newKeyPrefix={NewPrefix} transferredBy={UserId} reason={Reason}",
                customer,
                SafePrefix(old.LicenseKey),
                SafePrefix(issued.LicenseKey),
                transferredByUserId ?? "(system)",
                reasonLog);

            return new GenerateLicenseResult(
                Success: true,
                LicenseKey: issued.LicenseKey,
                SignedJwt: issued.SignedPayload,
                ExpiryAtUtc: issued.ExpiresAtUtc.UtcDateTime,
                Message: null);
        }
        catch (LicenseIssuanceUnavailableException)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<LicenseRenewalInfoResult?> GetRenewalInfoAsync(
        string licenseKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return null;

        var nk = licenseKey.Trim();
        var row = await _db.IssuedLicenses
            .AsNoTracking()
            .FirstOrDefaultAsync(il => EF.Functions.ILike(il.LicenseKey, nk), cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
            return null;

        var original = DateTime.SpecifyKind(row.ExpiryAtUtc, DateTimeKind.Utc);
        var utcNow = DateTime.UtcNow;
        var anchorDate = original >= utcNow
            ? new DateTime(original.Year, original.Month, original.Day, 0, 0, 0, DateTimeKind.Utc)
            : new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, DateTimeKind.Utc);
        var suggestedDate = anchorDate.AddYears(1);
        var suggestedEndOfDayUtc = new DateTime(
            suggestedDate.Year,
            suggestedDate.Month,
            suggestedDate.Day,
            23,
            59,
            59,
            DateTimeKind.Utc);

        var suggestedIsoDate = $"{suggestedEndOfDayUtc:yyyy-MM-dd}";
        return new LicenseRenewalInfoResult(original, suggestedIsoDate);
    }

    private static string? ValidateRequest(GenerateLicenseRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.CustomerName))
            return "customerName is required.";
        if (req.CustomerName.Trim().Length > MaxCustomerNameLength)
            return $"customerName must be {MaxCustomerNameLength} characters or fewer.";

        // Expiry must be strictly in the future (UTC).
        if (req.ExpiryDateUtc <= DateTime.UtcNow)
            return "expiryDate must be in the future (UTC).";

        if (req.RequireFingerprint)
        {
            var mh = req.MachineHashHex?.Trim();
            if (string.IsNullOrEmpty(mh))
                return "machineHashHex is required when requireFingerprint is true.";

            // SHA-256 hex (64 hex chars). Be lenient on case but strict on shape.
            if (mh.Length != 64 || !IsHex(mh))
                return "machineHashHex must be a 64-character lowercase hex SHA-256 digest.";
        }

        return null;
    }

    private static bool IsHex(string s)
    {
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            var ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if (!ok) return false;
        }
        return true;
    }

    private static RSA LoadRsaPrivateKey(string pem)
    {
        var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(pem);
            return rsa;
        }
        catch
        {
            rsa.Dispose();
            throw new LicenseIssuanceUnavailableException(
                "License signing key is invalid (License:SigningPrivateKeyPem cannot be imported as RSA PEM).");
        }
    }

    private static string SafePrefix(string s) =>
        string.IsNullOrEmpty(s) || s.Length <= 12 ? s : s[..12];

    /// <summary>REGK-****-****- plus last segment; non-standard shapes fully redacted.</summary>
    private static string MaskIssuedLicenseDisplayKey(string licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return "REGK-****-****-*****";

        var parts = licenseKey.Trim().Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 4
            && string.Equals(parts[0], "REGK", StringComparison.OrdinalIgnoreCase)
            && parts[3].Length > 0)
        {
            return "REGK-****-****-" + parts[3].ToUpperInvariant();
        }

        return "REGK-****-****-*****";
    }

    private static string MaskCustomerName(string customerName)
    {
        var t = customerName.Trim();
        return t.Length <= 2 ? "***" : t[..2] + "***";
    }
}
