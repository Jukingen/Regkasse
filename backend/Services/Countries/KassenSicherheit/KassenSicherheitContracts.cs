using KasseAPI_Final.Services.Countries.Strategies.Germany;

namespace KasseAPI_Final.Services.Countries.KassenSicherheit;

public sealed record KassenSicherheitCreateTssRequest(
    Guid TenantId,
    string TssId,
    string? Description = null);

public sealed record KassenSicherheitCreateClientRequest(
    Guid TenantId,
    string TssId,
    string ClientId,
    string SerialNumber);

public sealed record KassenSicherheitStartTransactionRequest(
    Guid TenantId,
    string TssId,
    string ClientId,
    string TransactionId,
    int TxRevision = 1);

public sealed record KassenSicherheitFinishTransactionRequest(
    Guid TenantId,
    string TssId,
    string ClientId,
    string TransactionId,
    int TxRevision,
    string? ProcessData = null,
    DeReceiptPayload? Receipt = null,
    string? Belegnummer = null);

public sealed record KassenSicherheitExportRequest(
    Guid TenantId,
    string ExportId,
    long StartDateUnix,
    long EndDateUnix,
    string? ClientId = null,
    string Format = "tar");

public sealed record KassenSicherheitAuthResult(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc);

public sealed record KassenSicherheitTssResult(
    string TssId,
    string? State);

public sealed record KassenSicherheitClientResult(
    string ClientId,
    string? State);

public sealed record KassenSicherheitTransactionResult(
    bool Completed,
    string? TransactionId,
    string? State,
    int? TxRevision,
    string? Signature,
    string? Provider);

public sealed record KassenSicherheitExportResult(
    bool Exported,
    string? ExportId,
    string? Provider);
