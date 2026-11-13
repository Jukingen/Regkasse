using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tse.Fiskaly;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Development-only Super Admin endpoints to sign and verify synthetic fiskaly SIGN AT receipts.
/// Hidden from OpenAPI; does not create POS payments. Host must be Development; otherwise 404.
/// </summary>
[Authorize(Roles = Roles.SuperAdmin)]
[ApiController]
[Route("api/admin/fiskaly-dev-test")]
[Produces("application/json")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class FiskalySignTestController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly IFiskalySignTestService _signTest;
    private readonly IFiskalyReceiptService? _receipts;
    private readonly ILogger<FiskalySignTestController>? _logger;

    public FiskalySignTestController(
        IWebHostEnvironment env,
        IFiskalySignTestService signTest,
        IFiskalyReceiptService? receipts = null,
        ILogger<FiskalySignTestController>? logger = null)
    {
        _env = env;
        _signTest = signTest;
        _receipts = receipts;
        _logger = logger;
    }

    [HttpGet("sign-scenarios")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(IReadOnlyList<FiskalySignTestScenarioDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<IReadOnlyList<FiskalySignTestScenarioDto>> GetScenarios()
    {
        if (!_env.IsDevelopment())
            return NotFound();

        return Ok(_signTest.GetScenarios());
    }

    [HttpPost("sign-test")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(FiskalySignTestResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FiskalySignTestResultDto>> SignTest(
        [FromBody] FiskalySignTestRequest? request,
        CancellationToken cancellationToken)
    {
        if (!_env.IsDevelopment())
            return NotFound();
        if (request is null)
            return BadRequest(new
            {
                success = false,
                message = "Request body is required.",
                error = FiskalyReceiptErrorMapper.FromCode(
                    FiskalyReceiptErrorCodes.ValidationError,
                    "Request body is required.")
            });

        if (IsReceiptServiceScenario(request.Scenario))
            return await SignViaReceiptServiceAsync(request, cancellationToken).ConfigureAwait(false);

        var result = await _signTest
            .SignAsync(request, ActorId(), User.IsInRole(Roles.SuperAdmin), cancellationToken)
            .ConfigureAwait(false);
        return Map(result);
    }

    [HttpPost("verify-test")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(FiskalyVerifyTestResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FiskalyVerifyTestResultDto>> VerifyTest(
        [FromBody] FiskalyVerifyTestRequest? request,
        CancellationToken cancellationToken)
    {
        if (!_env.IsDevelopment())
            return NotFound();
        if (request is null)
            return BadRequest(new
            {
                success = false,
                message = "Request body is required.",
                error = FiskalyReceiptErrorMapper.FromCode(
                    FiskalyReceiptErrorCodes.ValidationError,
                    "Request body is required.")
            });

        var result = await _signTest
            .VerifyAsync(request, User.IsInRole(Roles.SuperAdmin), cancellationToken)
            .ConfigureAwait(false);
        return Map(result);
    }

    private static bool IsReceiptServiceScenario(string? scenario) =>
        string.Equals(scenario, FiskalySignTestScenarioIds.Tagesabschluss, StringComparison.OrdinalIgnoreCase)
        || string.Equals(scenario, FiskalySignTestScenarioIds.MonthlyClose, StringComparison.OrdinalIgnoreCase)
        || string.Equals(scenario, FiskalySignTestScenarioIds.YearlyClose, StringComparison.OrdinalIgnoreCase);

    private async Task<ActionResult<FiskalySignTestResultDto>> SignViaReceiptServiceAsync(
        FiskalySignTestRequest request,
        CancellationToken cancellationToken)
    {
        if (_receipts is null)
            return BadRequest(new
            {
                success = false,
                message = "Fiskaly receipt service is not configured.",
                error = FiskalyReceiptErrorMapper.FromCode(
                    FiskalyReceiptErrorCodes.FiskalyNotConfigured,
                    "Fiskaly receipt service is not configured.")
            });

        var actorId = ActorId();
        var isSuperAdmin = User.IsInRole(Roles.SuperAdmin);
        var scenario = request.Scenario?.Trim() ?? string.Empty;

        FiskalyReceiptOperationResult submitted;
        if (string.Equals(scenario, FiskalySignTestScenarioIds.Tagesabschluss, StringComparison.OrdinalIgnoreCase))
        {
            submitted = await _receipts
                .CreateTagesabschlussAsync(
                    request.CashRegisterId,
                    request.ClosingDate,
                    actorId,
                    isSuperAdmin,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (string.Equals(scenario, FiskalySignTestScenarioIds.MonthlyClose, StringComparison.OrdinalIgnoreCase))
        {
            if (!FiskalySignTestPeriod.TryResolve(request, yearly: false, out var year, out var month, out var periodError))
                return PeriodBadRequest(periodError, yearly: false);

            submitted = await _receipts
                .CreateMonatsbelegAsync(
                    request.CashRegisterId,
                    year,
                    month,
                    "Development Fiskaly sign test",
                    actorId,
                    isSuperAdmin,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            if (!FiskalySignTestPeriod.TryResolve(request, yearly: true, out var year, out _, out var periodError))
                return PeriodBadRequest(periodError, yearly: true);

            submitted = await _receipts
                .CreateJahresbelegAsync(
                    request.CashRegisterId,
                    year,
                    "Development Fiskaly sign test",
                    actorId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (submitted.Success && submitted.Data is not null)
            return Ok(FromReceipt(scenario, submitted.Data));

        _logger?.LogWarning(
            "Fiskaly sign-test special receipt failed. Scenario={Scenario} Register={CashRegisterId} Code={Code} Message={Message}",
            scenario,
            request.CashRegisterId,
            submitted.Error?.Code,
            submitted.Error?.Message);

        return Map(FiskalySetupOperationResult<FiskalySignTestResultDto>.Fail(
            submitted.StatusCode,
            submitted.Error?.Message ?? "Special receipt submit failed.",
            submitted.Error?.Code,
            submitted.Error?.Details));
    }

    private ActionResult<FiskalySignTestResultDto> PeriodBadRequest(string? periodError, bool yearly)
    {
        var message = periodError ?? (yearly ? "Invalid year." : "Invalid year or month.");
        var code = FiskalyReceiptErrorMapper.InferCodeFromMessage(message);
        return BadRequest(new
        {
            success = false,
            message,
            error = FiskalyReceiptErrorMapper.FromCode(
                code,
                message,
                yearly
                    ? "Use a completed Vienna calendar year or an ISO-8601 UTC closingDate."
                    : "Use the last completed Vienna calendar month or an ISO-8601 UTC closingDate.")
        });
    }

    private static FiskalySignTestResultDto FromReceipt(string scenario, FiskalyReceiptDataDto data)
    {
        var hasQr = !string.IsNullOrWhiteSpace(data.QrCode);
        var hasNumber = !string.IsNullOrWhiteSpace(data.ReceiptNumber);
        return new FiskalySignTestResultDto
        {
            Success = true,
            Scenario = scenario,
            ReceiptId = data.ReceiptId,
            ReceiptNumber = data.ReceiptNumber,
            QrCodeData = data.QrCode,
            ReceiptType = "NORMAL",
            Signed = true,
            Checks = new FiskalyReceiptChecksDto
            {
                QrFormatValid = hasQr,
                HasReceiptNumber = hasNumber,
                ReceiptNumberLooksSequential = hasNumber,
                Signed = true
            },
            QrValidation = new FiskalyQrValidationDto
            {
                IsValid = hasQr,
                ReceiptNumber = data.ReceiptNumber
            }
        };
    }

    private string ActorId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "unknown";

    private ActionResult<T> Map<T>(FiskalySetupOperationResult<T> result)
    {
        if (result.Success && result.Data is not null)
            return Ok(result.Data);

        var error = FiskalyReceiptErrorMapper.FromCode(
            result.Code ?? FiskalyReceiptErrorMapper.InferCodeFromMessage(result.Message),
            result.Message,
            result.Details);
        var body = new
        {
            success = false,
            message = result.Message,
            error
        };

        if (result.StatusCode == StatusCodes.Status404NotFound)
            return NotFound(body);

        return BadRequest(body);
    }
}
