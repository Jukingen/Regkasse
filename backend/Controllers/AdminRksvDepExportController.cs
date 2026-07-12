using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Export;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// RKSV §7 DEP export in BMF JSON format (Anlage Z3). Distinct from operational <see cref="FiscalExportController"/>.
/// </summary>
[ApiController]
[Route("api/admin/rksv/dep-export")]
[Authorize]
[HasPermission(AppPermissions.ReportExport)]
[HasPermission(AppPermissions.AuditView)]
public class AdminRksvDepExportController : ControllerBase
{
    private readonly IRksvDepExportService _depExportService;
    private readonly IDepExportHistoryService _historyService;
    private readonly IDepExportScheduler _scheduler;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IAuditLogService _auditLogService;
    private readonly IRksvEnvironmentService _rksvEnv;

    public AdminRksvDepExportController(
        IRksvDepExportService depExportService,
        IDepExportHistoryService historyService,
        IDepExportScheduler scheduler,
        ICurrentTenantAccessor tenantAccessor,
        IAuditLogService auditLogService,
        IRksvEnvironmentService rksvEnv)
    {
        _depExportService = depExportService;
        _historyService = historyService;
        _scheduler = scheduler;
        _tenantAccessor = tenantAccessor;
        _auditLogService = auditLogService;
        _rksvEnv = rksvEnv;
    }

    [HttpGet]
    [ProducesResponseType(typeof(RksvDepExportRootDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RksvDepExportEnvelopeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDepExport(
        [FromQuery] Guid cashRegisterId,
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        [FromQuery] bool includeSpecialReceipts = true,
        [FromQuery] bool includeDailyClosings = true,
        [FromQuery] bool includeEnvelope = false,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        try
        {
            if (includeEnvelope)
            {
                var build = await _depExportService.GenerateDepExportWithValidationAsync(
                        cashRegisterId,
                        fromUtc,
                        toUtc,
                        includeSpecialReceipts,
                        includeDailyClosings,
                        cancellationToken)
                    .ConfigureAwait(false);

                var exportJson = System.Text.Json.JsonSerializer.Serialize(build.Root);
                var validation = await _depExportService
                    .ValidateExportFormatAsync(exportJson, cancellationToken)
                    .ConfigureAwait(false);

                await RecordCompletedExportAsync(
                        tenantId.Value,
                        cashRegisterId,
                        fromUtc,
                        toUtc,
                        includeSpecialReceipts,
                        includeDailyClosings,
                        build.Root,
                        cancellationToken)
                    .ConfigureAwait(false);

                return Ok(new RksvDepExportEnvelopeDto
                {
                    LegalNotice = build.LegalNotice,
                    Dep = build.Root,
                    BelegCount = build.BelegCount,
                    BelegeGruppeCount = build.BelegeGruppeCount,
                    CashRegisterId = build.CashRegisterId,
                    RegisterNumber = build.RegisterNumber,
                    FromUtc = build.FromUtc,
                    ToUtc = build.ToUtc,
                    IsDemo = build.IsDemo,
                    Environment = build.Environment,
                    FormatValidated = build.FormatValidated,
                    FormatValidation = validation,
                });
            }

            var export = await _depExportService.GenerateDepExportAsync(
                    cashRegisterId,
                    fromUtc,
                    toUtc,
                    includeSpecialReceipts,
                    includeDailyClosings,
                    cancellationToken)
                .ConfigureAwait(false);

            await RecordCompletedExportAsync(
                    tenantId.Value,
                    cashRegisterId,
                    fromUtc,
                    toUtc,
                    includeSpecialReceipts,
                    includeDailyClosings,
                    export,
                    cancellationToken)
                .ConfigureAwait(false);

            return Ok(export);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message, code = "RKSV_DEP_EXPORT_INVALID_RANGE" });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message, code = "RKSV_DEP_EXPORT_REGISTER_NOT_FOUND" });
        }
    }

    [HttpPost("validate")]
    [HasPermission(AppPermissions.ReportExport)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidateExport(
        [FromBody] ValidateExportRequest request,
        CancellationToken ct = default)
    {
        if (_tenantAccessor.TenantId is null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.ExportJson))
            return BadRequest(new { message = "exportJson is required.", code = "RKSV_DEP_EXPORT_JSON_REQUIRED" });

        var result = await _depExportService.ValidateExportFormatAsync(request.ExportJson, ct).ConfigureAwait(false);

        return Ok(new
        {
            success = result.IsValid,
            message = result.IsValid ? "Export format is valid" : "Export format is invalid",
            environment = _rksvEnv.GetEnvironmentDisplayName(),
            validation = result,
        });
    }

    [HttpPost("test-prueftool")]
    [HasPermission(AppPermissions.ReportExport)]
    [ProducesResponseType(typeof(RksvDepPrueftoolResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TestPrueftool(
        [FromBody] TestPrueftoolRequest request,
        CancellationToken ct = default)
    {
        if (_tenantAccessor.TenantId is null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.ExportJson))
            return BadRequest(new { message = "exportJson is required.", code = "RKSV_DEP_EXPORT_JSON_REQUIRED" });

        var result = await _depExportService
            .RunPrueftoolAsync(request.ExportJson, ct)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpGet("test-material")]
    [ProducesResponseType(typeof(CryptoMaterialDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTestMaterial(
        [FromQuery] Guid cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        if (_tenantAccessor.TenantId is null)
            return NotFound();

        try
        {
            var material = await _depExportService
                .GenerateCryptoMaterialAsync(cashRegisterId, cancellationToken)
                .ConfigureAwait(false);
            return Ok(material);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message, code = "RKSV_DEP_EXPORT_REGISTER_NOT_FOUND" });
        }
    }

    [HttpGet("history")]
    [ProducesResponseType(typeof(DepExportHistoryListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListHistory(
        [FromQuery] Guid? cashRegisterId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var result = await _historyService
            .ListAsync(tenantId.Value, cashRegisterId, page, pageSize, cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("history/{id:guid}")]
    [ProducesResponseType(typeof(DepExportHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(Guid id, CancellationToken cancellationToken = default)
    {
        if (_tenantAccessor.TenantId is null)
            return NotFound();

        var row = await _historyService.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpGet("history/{id:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadHistory(Guid id, CancellationToken cancellationToken = default)
    {
        if (_tenantAccessor.TenantId is null)
            return NotFound();

        var file = await _historyService.TryOpenDownloadAsync(id, cancellationToken).ConfigureAwait(false);
        if (file is null)
            return NotFound(new { message = "Stored export file not available.", code = "RKSV_DEP_EXPORT_FILE_NOT_FOUND" });

        var (stream, fileName, contentType) = file.Value;
        return File(stream, contentType, fileName);
    }

    [HttpPost("schedule")]
    [ProducesResponseType(typeof(DepExportScheduleResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateSchedule(
        [FromBody] CreateDepExportScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var schedule = await _scheduler.CreateScheduleAsync(
                    tenantId.Value,
                    request.CashRegisterId,
                    request.ScheduleType,
                    request.DayOfMonth,
                    request.TimeOfDay,
                    request.RecipientEmails,
                    cancellationToken)
                .ConfigureAwait(false);

            return Created($"/api/admin/rksv/dep-export/schedules", ToScheduleDto(schedule));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message, code = "RKSV_DEP_SCHEDULE_INVALID" });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message, code = "RKSV_DEP_EXPORT_REGISTER_NOT_FOUND" });
        }
    }

    [HttpGet("schedules")]
    [ProducesResponseType(typeof(IEnumerable<DepExportScheduleResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSchedules(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var schedules = await _scheduler.GetSchedulesAsync(tenantId.Value, cancellationToken).ConfigureAwait(false);
        return Ok(schedules.Select(ToScheduleDto));
    }

    [HttpDelete("schedule/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSchedule(Guid id, CancellationToken cancellationToken = default)
    {
        if (_tenantAccessor.TenantId is null)
            return NotFound();

        var schedule = await _scheduler.GetScheduleByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (schedule is null || schedule.TenantId != _tenantAccessor.TenantId)
            return NotFound();

        await _scheduler.DeactivateScheduleAsync(id, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    private async Task RecordCompletedExportAsync(
        Guid tenantId,
        Guid cashRegisterId,
        DateTime fromUtc,
        DateTime toUtc,
        bool includeSpecialReceipts,
        bool includeDailyClosings,
        RksvDepExportRootDto export,
        CancellationToken cancellationToken)
    {
        await _historyService.RecordCompletedAsync(
                new DepExportHistoryRecordRequest
                {
                    TenantId = tenantId,
                    CashRegisterId = cashRegisterId,
                    FromUtc = fromUtc,
                    ToUtc = toUtc,
                    ExportedByUserId = User.GetActorUserId() ?? "unknown",
                    Export = export,
                    IncludeSpecialReceipts = includeSpecialReceipts,
                    IncludeDailyClosings = includeDailyClosings,
                },
                cancellationToken)
            .ConfigureAwait(false);

        await _auditLogService.LogSystemOperationAsync(
                "RksvDepExportJson",
                AuditLogEntityTypes.FISCAL_EXPORT,
                User.GetActorUserId() ?? "unknown",
                User.GetActorRole() ?? "Unknown",
                description: $"DEP export generated for register {cashRegisterId} from {fromUtc} to {toUtc}")
            .ConfigureAwait(false);
    }

    private static DepExportScheduleResponse ToScheduleDto(DepExportSchedule schedule) =>
        new()
        {
            Id = schedule.Id,
            CashRegisterId = schedule.CashRegisterId,
            ScheduleType = schedule.ScheduleType,
            DayOfMonth = schedule.DayOfMonth,
            TimeOfDay = schedule.TimeOfDay,
            IsActive = schedule.IsActive,
            RecipientEmails = schedule.RecipientEmails,
            LastRunAt = schedule.LastRunAt,
            NextRunAt = schedule.NextRunAt,
            CreatedAt = schedule.CreatedAt,
        };
}

public record ValidateExportRequest
{
    public string ExportJson { get; init; } = string.Empty;
}

public record TestPrueftoolRequest
{
    public string ExportJson { get; init; } = string.Empty;
}
