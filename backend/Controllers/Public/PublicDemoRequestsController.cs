using KasseAPI_Final.Services.DemoRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Anonymous marketing-site demo request. No JWT and no ambient tenant.
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("api/public/demo-requests")]
[Produces("application/json")]
public sealed class PublicDemoRequestsController : ControllerBase
{
    private readonly IDemoRequestService _demoRequests;

    public PublicDemoRequestsController(IDemoRequestService demoRequests)
    {
        _demoRequests = demoRequests;
    }

    [HttpPost]
    [ProducesResponseType(typeof(DemoRequestCreatedDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(DemoRequestErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(DemoRequestErrorDto), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Create(
        [FromBody] CreateDemoRequestDto? body,
        CancellationToken cancellationToken = default)
    {
        var result = await _demoRequests.CreateAsync(body, ClientIp(), cancellationToken).ConfigureAwait(false);
        return result.Status switch
        {
            DemoRequestCreateStatus.Created => StatusCode(StatusCodes.Status201Created, result.Created),
            DemoRequestCreateStatus.RateLimited => StatusCode(StatusCodes.Status429TooManyRequests, result.Error),
            _ => BadRequest(result.Error),
        };
    }

    private string ClientIp() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
