using KasseAPI_Final.Authorization;
using KasseAPI_Final.Tse.Fiskaly;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Development-only fiskaly SIGN AT credential probe (auth → SCU → cash register).
/// Hidden from OpenAPI; does not change the Production signing DI path.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/fiskaly-dev-test")]
[Produces("application/json")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class FiskalyDevTestController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly IFiskalyConnectionProbe _probe;
    private readonly ILogger<FiskalyDevTestController> _logger;

    public FiskalyDevTestController(
        IWebHostEnvironment env,
        IFiskalyConnectionProbe probe,
        ILogger<FiskalyDevTestController> logger)
    {
        _env = env;
        _probe = probe;
        _logger = logger;
    }

    /// <summary>
    /// POST: Authenticate against fiskaly SIGN AT, then optionally create an SCU and cash register
    /// in CREATED state (not FON-initialized). Host must be Development; otherwise 404.
    /// </summary>
    [HttpPost("connection-probe")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(FiskalyConnectionProbeResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FiskalyConnectionProbeResult>> ProbeConnectionAsync(
        [FromBody] FiskalyConnectionProbeRequest? body,
        CancellationToken cancellationToken)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        var request = body ?? new FiskalyConnectionProbeRequest();
        var result = await _probe.ProbeAsync(request, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Fiskaly connection probe finished: success={Success} auth={Auth} scu={Scu} cashRegister={CashRegister}",
            result.Success,
            result.Authentication.Status,
            result.ScuCreation.Status,
            result.CashRegisterCreation.Status);

        return Ok(result);
    }
}
