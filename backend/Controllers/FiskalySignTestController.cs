using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Tse.Fiskaly;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public FiskalySignTestController(IWebHostEnvironment env, IFiskalySignTestService signTest)
    {
        _env = env;
        _signTest = signTest;
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
            return BadRequest(new { message = "Request body is required." });

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
            return BadRequest(new { message = "Request body is required." });

        var result = await _signTest
            .VerifyAsync(request, User.IsInRole(Roles.SuperAdmin), cancellationToken)
            .ConfigureAwait(false);
        return Map(result);
    }

    private string ActorId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "unknown";

    private ActionResult<T> Map<T>(FiskalySetupOperationResult<T> result)
    {
        if (result.Success && result.Data is not null)
            return Ok(result.Data);

        if (result.StatusCode == StatusCodes.Status404NotFound)
            return NotFound(new { message = result.Message });

        return BadRequest(new { message = result.Message });
    }
}
