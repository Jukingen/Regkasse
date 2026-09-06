using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class FiskalyOperationHistoryRecorder : IFiskalyOperationHistoryRecorder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AppDbContext _db;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly FiskalyOperationHistoryWriteScope _writeScope;
    private readonly IFiskalyOperationStatusBroadcaster? _statusBroadcaster;
    private readonly ILogger<FiskalyOperationHistoryRecorder> _logger;

    public FiskalyOperationHistoryRecorder(
        AppDbContext db,
        ICurrentTenantAccessor tenantAccessor,
        FiskalyOperationHistoryWriteScope writeScope,
        ILogger<FiskalyOperationHistoryRecorder> logger,
        IFiskalyOperationStatusBroadcaster? statusBroadcaster = null)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
        _writeScope = writeScope;
        _logger = logger;
        _statusBroadcaster = statusBroadcaster;
    }

    public async Task<Guid?> StartAsync(
        FiskalyOperationHistoryStartRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return null;

        if (!FiskalyOperationTypes.IsKnown(request.OperationType))
            return null;

        try
        {
            var cashRegisterName = await ResolveCashRegisterNameAsync(request.CashRegisterId, cancellationToken)
                .ConfigureAwait(false);
            var (userDisplay, _) = await ResolveUserAsync(request.ActorUserId, cancellationToken)
                .ConfigureAwait(false);
            var tenantName = await _db.Tenants.AsNoTracking()
                .Where(t => t.Id == tenantId.Value)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            var row = new FiskalyOperationHistory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                TenantName = tenantName,
                OperationType = request.OperationType.Trim().ToLowerInvariant(),
                Status = nameof(FiskalyOperationHistoryStatus.Pending),
                CashRegisterId = request.CashRegisterId,
                CashRegisterName = cashRegisterName,
                UserId = string.IsNullOrWhiteSpace(request.ActorUserId) ? "unknown" : request.ActorUserId.Trim(),
                UserDisplayName = userDisplay,
                RequestPayloadJson = Serialize(request.RequestPayload),
                RetriedFromId = _writeScope.RetriedFromId,
                RetryCount = 0,
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.FiskalyOperationHistories.Add(row);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _writeScope.CurrentHistoryId = row.Id;
            await PublishAsync(row, cancellationToken).ConfigureAwait(false);
            return row.Id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start Fiskaly operation history for {Operation}", request.OperationType);
            return null;
        }
    }

    public async Task MarkProcessingAsync(Guid historyId, CancellationToken cancellationToken = default)
    {
        if (historyId == Guid.Empty)
            return;

        try
        {
            var row = await _db.FiskalyOperationHistories
                .FirstOrDefaultAsync(x => x.Id == historyId, cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
                return;
            if (FiskalyOperationHistoryStatuses.IsTerminal(row.Status))
                return;

            row.Status = FiskalyOperationHistoryStatuses.Processing;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await PublishAsync(row, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to mark Fiskaly operation history {HistoryId} as processing", historyId);
        }
    }

    public async Task CompleteAsync(
        Guid historyId,
        FiskalyOperationHistoryCompleteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (historyId == Guid.Empty)
            return;

        try
        {
            var row = await _db.FiskalyOperationHistories
                .FirstOrDefaultAsync(x => x.Id == historyId, cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
                return;

            row.Status = request.Success
                ? nameof(FiskalyOperationHistoryStatus.Success)
                : nameof(FiskalyOperationHistoryStatus.Failed);
            row.ReceiptNumber = Truncate(request.ReceiptNumber, 64);
            row.ReceiptId = Truncate(request.ReceiptId, 80);
            row.ResponsePayloadJson = Serialize(request.ResponsePayload);
            row.ErrorCode = Truncate(request.ErrorCode, 64);
            row.ErrorMessage = request.ErrorMessage;
            row.CompletedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await PublishAsync(row, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to complete Fiskaly operation history {HistoryId}", historyId);
        }
    }

    private async Task PublishAsync(FiskalyOperationHistory row, CancellationToken cancellationToken)
    {
        if (_statusBroadcaster is null)
            return;
        await _statusBroadcaster
            .PublishAsync(FiskalyOperationStatusMapper.FromRow(row), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<string?> ResolveCashRegisterNameAsync(Guid cashRegisterId, CancellationToken cancellationToken)
    {
        if (cashRegisterId == Guid.Empty)
            return null;

        var register = await _db.CashRegisters.AsNoTracking()
            .Where(r => r.Id == cashRegisterId)
            .Select(r => new { r.RegisterNumber, r.Location })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (register is null)
            return cashRegisterId.ToString("D");

        var number = register.RegisterNumber?.Trim() ?? string.Empty;
        var location = register.Location?.Trim() ?? string.Empty;
        if (number.Length == 0 && location.Length == 0)
            return cashRegisterId.ToString("D");
        if (location.Length == 0)
            return number;
        if (number.Length == 0)
            return location;
        return $"{number} · {location}";
    }

    private async Task<(string? Display, string? UserName)> ResolveUserAsync(
        string actorUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(actorUserId) || actorUserId is "unknown" or "system")
            return (actorUserId, actorUserId);

        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == actorUserId)
            .Select(u => new { u.UserName, u.FirstName, u.LastName })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
            return (actorUserId, actorUserId);

        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        if (!string.IsNullOrWhiteSpace(user.UserName) && !string.IsNullOrWhiteSpace(fullName))
            return ($"{user.UserName} ({fullName})", user.UserName);
        if (!string.IsNullOrWhiteSpace(user.UserName))
            return (user.UserName, user.UserName);
        if (!string.IsNullOrWhiteSpace(fullName))
            return (fullName, fullName);
        return (actorUserId, actorUserId);
    }

    private static string? Serialize(object? value)
    {
        if (value is null)
            return null;
        try
        {
            return JsonSerializer.Serialize(value, JsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        return value.Length <= max ? value : value[..max];
    }
}
