using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Appointments;

public sealed class AppointmentService : IAppointmentService
{
    public const int DefaultDurationMinutes = 30;

    private static readonly AppointmentStatus[] OccupyingStatuses =
    [
        AppointmentStatus.Booked,
        AppointmentStatus.Confirmed,
    ];

    private readonly AppDbContext _db;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<AppointmentService> _logger;

    public AppointmentService(
        AppDbContext db,
        ICurrentTenantAccessor tenantAccessor,
        IAuditLogService auditLog,
        ILogger<AppointmentService> logger)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
        _auditLog = auditLog;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AppointmentDto>> ListAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? staffId,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return Array.Empty<AppointmentDto>();

        var query = _db.Appointments.AsNoTracking().Where(row => row.TenantId == tenantId);
        if (fromUtc.HasValue)
            query = query.Where(row => row.EndUtc > Utc(fromUtc.Value));
        if (toUtc.HasValue)
            query = query.Where(row => row.StartUtc < Utc(toUtc.Value));
        var staff = NormalizeStaff(staffId);
        if (staff is not null)
            query = query.Where(row => row.StaffId == staff);

        var rows = await query
            .OrderBy(row => row.StartUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(Map).ToList();
    }

    public async Task<AppointmentWriteResult> CreateAsync(
        CreatePosAppointmentRequest request,
        string? actorUserId,
        string? actorRole,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var start = Utc(request.StartUtc);
        var end = ResolveEnd(start, request.EndUtc, await LookupDurationAsync(request.ServiceProductId, cancellationToken).ConfigureAwait(false));
        if (end <= start)
            return BadRequest("END_BEFORE_START");

        var staffId = NormalizeStaff(request.StaffId);
        var occupying = OccupyingStatus(request.Status);
        if (occupying)
        {
            var conflict = await FindConflictAsync(tenantId, id: null, staffId, start, end, cancellationToken).ConfigureAwait(false);
            if (conflict is not null)
                return Conflict(conflict, request.ExpectedVersion);
        }

        var notes = ComposeNotes(request.Notes, request.CustomerName);
        var customerId = await ResolveCustomerAsync(request.CustomerId, cancellationToken).ConfigureAwait(false);
        var productId = await ResolveProductAsync(request.ServiceProductId, cancellationToken).ConfigureAwait(false);
        var now = DateTime.UtcNow;
        var row = new Appointment
        {
            TenantId = tenantId,
            CustomerId = customerId,
            ServiceProductId = productId,
            StaffId = staffId,
            StartUtc = start,
            EndUtc = end,
            Status = request.Status ?? AppointmentStatus.Booked,
            Notes = notes,
            Version = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedByUserId = actorUserId,
        };

        _db.Appointments.Add(row);
        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            var existing = await FindConflictAsync(tenantId, id: null, staffId, start, end, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
                return Conflict(existing, request.ExpectedVersion);
            throw;
        }

        await AuditAsync(
                "APPOINTMENT_CREATED",
                AuditEventType.AppointmentCreated,
                row,
                actorUserId,
                actorRole,
                oldValues: null,
                newValues: Map(row),
                cancellationToken)
            .ConfigureAwait(false);

        return new AppointmentWriteResult(Map(row), null, null, StatusCodes.Status201Created);
    }

    public async Task<AppointmentWriteResult> UpdateAsync(
        Guid id,
        UpdatePosAppointmentRequest request,
        string? actorUserId,
        string? actorRole,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var row = await _db.Appointments.FirstOrDefaultAsync(item => item.Id == id, cancellationToken).ConfigureAwait(false);
        if (row is null || row.TenantId != tenantId)
            return NotFound();

        if (row.Version != request.ExpectedVersion)
            return Conflict(row, request.ExpectedVersion);

        var previous = Map(row);
        var start = request.StartUtc.HasValue ? Utc(request.StartUtc.Value) : row.StartUtc;
        var durationHint = await LookupDurationAsync(request.ServiceProductId ?? row.ServiceProductId, cancellationToken).ConfigureAwait(false);
        var end = request.EndUtc.HasValue
            ? Utc(request.EndUtc.Value)
            : request.StartUtc.HasValue
                ? ResolveEnd(start, null, durationHint)
                : row.EndUtc;
        if (end <= start)
            return BadRequest("END_BEFORE_START");

        var staffId = request.StaffId is null ? row.StaffId : NormalizeStaff(request.StaffId);
        var status = request.Status ?? row.Status;
        if (OccupyingStatus(status))
        {
            var conflict = await FindConflictAsync(tenantId, row.Id, staffId, start, end, cancellationToken).ConfigureAwait(false);
            if (conflict is not null)
                return Conflict(conflict, request.ExpectedVersion);
        }

        row.CustomerId = request.CustomerId.HasValue
            ? await ResolveCustomerAsync(request.CustomerId, cancellationToken).ConfigureAwait(false)
            : row.CustomerId;
        row.ServiceProductId = request.ServiceProductId.HasValue
            ? await ResolveProductAsync(request.ServiceProductId, cancellationToken).ConfigureAwait(false)
            : row.ServiceProductId;
        row.StaffId = staffId;
        row.StartUtc = start;
        row.EndUtc = end;
        row.Notes = request.Notes ?? row.Notes;
        row.Status = status;
        row.Version += 1;
        row.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            var current = await ReloadAsync(id, cancellationToken).ConfigureAwait(false);
            return current is null ? NotFound() : Conflict(current, request.ExpectedVersion);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            var existing = await FindConflictAsync(tenantId, row.Id, staffId, start, end, cancellationToken).ConfigureAwait(false);
            return existing is null ? NotFound() : Conflict(existing, request.ExpectedVersion);
        }

        await AuditAsync(
                "APPOINTMENT_UPDATED",
                AuditEventType.AppointmentUpdated,
                row,
                actorUserId,
                actorRole,
                oldValues: previous,
                newValues: Map(row),
                cancellationToken)
            .ConfigureAwait(false);

        return new AppointmentWriteResult(Map(row), null, null, StatusCodes.Status200OK);
    }

    public async Task<AppointmentWriteResult> CancelAsync(
        Guid id,
        string? actorUserId,
        string? actorRole,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var row = await _db.Appointments.FirstOrDefaultAsync(item => item.Id == id, cancellationToken).ConfigureAwait(false);
        if (row is null || row.TenantId != tenantId)
            return NotFound();

        if (row.Status == AppointmentStatus.Cancelled)
            return new AppointmentWriteResult(Map(row), null, null, StatusCodes.Status200OK);

        var previous = Map(row);
        row.Status = AppointmentStatus.Cancelled;
        row.Version += 1;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await AuditAsync(
                "APPOINTMENT_CANCELLED",
                AuditEventType.AppointmentCancelled,
                row,
                actorUserId,
                actorRole,
                oldValues: previous,
                newValues: Map(row),
                cancellationToken)
            .ConfigureAwait(false);

        return new AppointmentWriteResult(Map(row), null, null, StatusCodes.Status200OK);
    }

    private async Task<Appointment?> FindConflictAsync(
        Guid tenantId,
        Guid? id,
        string? staffId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken)
    {
        var query = _db.Appointments.AsNoTracking()
            .Where(row =>
                row.TenantId == tenantId
                && (row.Status == AppointmentStatus.Booked || row.Status == AppointmentStatus.Confirmed)
                && row.StartUtc < endUtc
                && row.EndUtc > startUtc);
        if (id.HasValue)
            query = query.Where(row => row.Id != id.Value);
        query = staffId is null
            ? query.Where(row => row.StaffId == null)
            : query.Where(row => row.StaffId == staffId);

        return await query
            .OrderBy(row => row.StartUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Appointment?> ReloadAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.Appointments.AsNoTracking().FirstOrDefaultAsync(row => row.Id == id, cancellationToken).ConfigureAwait(false);

    private async Task<int?> LookupDurationAsync(Guid? productId, CancellationToken cancellationToken)
    {
        if (productId is null)
            return null;
        return await _db.Products
            .AsNoTracking()
            .Where(product => product.Id == productId.Value)
            .Select(product => product.DurationMinutes)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Guid?> ResolveCustomerAsync(Guid? customerId, CancellationToken cancellationToken)
    {
        if (customerId is null)
            return null;
        var exists = await _db.Customers.AnyAsync(customer => customer.Id == customerId.Value, cancellationToken).ConfigureAwait(false);
        return exists ? customerId : null;
    }

    private async Task<Guid?> ResolveProductAsync(Guid? productId, CancellationToken cancellationToken)
    {
        if (productId is null)
            return null;
        var exists = await _db.Products.AnyAsync(product => product.Id == productId.Value, cancellationToken).ConfigureAwait(false);
        return exists ? productId : null;
    }

    private async Task AuditAsync(
        string action,
        AuditEventType actionType,
        Appointment row,
        string? actorUserId,
        string? actorRole,
        object? oldValues,
        object? newValues,
        CancellationToken cancellationToken)
    {
        try
        {
            await _auditLog.LogSystemOperationAsync(
                    action: action,
                    entityType: nameof(Appointment),
                    userId: actorUserId ?? "system",
                    userRole: actorRole ?? "Cashier",
                    description: action,
                    status: AuditLogStatus.Success,
                    correlationIdOverride: Guid.NewGuid().ToString("N"),
                    actionType: actionType,
                    entityId: row.Id,
                    tenantId: row.TenantId,
                    oldValues: oldValues,
                    newValues: newValues)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Appointment audit failed for {Action} {AppointmentId}", action, row.Id);
        }
    }

    private static AppointmentWriteResult Conflict(Appointment current, int? expectedVersion)
    {
        if (expectedVersion is int expected && expected == current.Version)
        {
            // Caller already observed this row; still a create/update slot conflict.
        }

        return new AppointmentWriteResult(
            null,
            Map(current),
            AppointmentConflictCodes.Conflict,
            StatusCodes.Status409Conflict);
    }

    private static AppointmentWriteResult NotFound() =>
        new(null, null, "NOT_FOUND", StatusCodes.Status404NotFound);

    private static AppointmentWriteResult BadRequest(string code) =>
        new(null, null, code, StatusCodes.Status400BadRequest);

    private static bool OccupyingStatus(AppointmentStatus? status) =>
        status is null || OccupyingStatuses.Contains(status.Value);

    private static DateTime Utc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();

    private static DateTime ResolveEnd(DateTime start, DateTime? end, int? durationMinutes)
    {
        if (end.HasValue)
            return Utc(end.Value);
        var minutes = durationMinutes is > 0 ? durationMinutes.Value : DefaultDurationMinutes;
        return start.AddMinutes(minutes);
    }

    private static string? NormalizeStaff(string? staffId)
    {
        var trimmed = staffId?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static string? ComposeNotes(string? notes, string? customerName)
    {
        var trimmedNotes = notes?.Trim();
        var trimmedName = customerName?.Trim();
        if (string.IsNullOrEmpty(trimmedNotes))
            return string.IsNullOrEmpty(trimmedName) ? null : trimmedName;
        return trimmedNotes;
    }

    private static AppointmentDto Map(Appointment row) => new()
    {
        Id = row.Id,
        TenantId = row.TenantId,
        CustomerId = row.CustomerId,
        ServiceProductId = row.ServiceProductId,
        StaffId = row.StaffId,
        StartUtc = row.StartUtc,
        EndUtc = row.EndUtc,
        Status = row.Status,
        Notes = row.Notes,
        Version = row.Version,
        CreatedAtUtc = row.CreatedAtUtc,
        UpdatedAtUtc = row.UpdatedAtUtc,
        CreatedByUserId = row.CreatedByUserId,
    };

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("23505", StringComparison.Ordinal)
               || message.Contains("ux_appointments_tenant_staff_start", StringComparison.OrdinalIgnoreCase)
               || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
               || message.Contains("unique", StringComparison.OrdinalIgnoreCase);
    }
}
