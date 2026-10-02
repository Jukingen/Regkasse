using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services.Lodging;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/admin")]
[Produces("application/json")]
[HasPermission(AppPermissions.ProductView)]
public sealed class AdminLodgingController : ControllerBase
{
    private readonly ILodgingService _lodging;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminLodgingController(ILodgingService lodging, ICurrentTenantAccessor tenantAccessor)
    {
        _lodging = lodging;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet("rooms")]
    [ProducesResponseType(typeof(IReadOnlyList<RoomDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RoomDto>>> ListRooms(CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var rooms = await _lodging.ListRoomsAsync(tenantId, ignoreFilters: false, cancellationToken)
            .ConfigureAwait(false);
        return Ok(rooms);
    }

    [HttpPost("rooms")]
    [HasPermission(AppPermissions.ProductManage)]
    [ProducesResponseType(typeof(RoomDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(LodgingErrorDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomDto>> CreateRoom(
        [FromBody] CreateRoomRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var (userId, role) = Actor();
        var result = await _lodging
            .CreateRoomAsync(tenantId, request, ignoreFilters: false, cancellationToken, userId, role)
            .ConfigureAwait(false);
        return result switch
        {
            RoomWriteResult.Ok ok => StatusCode(StatusCodes.Status201Created, ok.Room),
            RoomWriteResult.Conflict conflict => Conflict(
                new LodgingErrorDto { Code = conflict.Code, Message = conflict.Message }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPatch("rooms/{id:guid}")]
    [HasPermission(AppPermissions.ProductManage)]
    [ProducesResponseType(typeof(RoomDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LodgingErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomDto>> UpdateRoomStatus(
        Guid id,
        [FromBody] UpdateRoomStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var (userId, role) = Actor();
        var result = await _lodging
            .UpdateRoomStatusAsync(tenantId, id, request, ignoreFilters: false, cancellationToken, userId, role)
            .ConfigureAwait(false);
        return result switch
        {
            RoomWriteResult.Ok ok => Ok(ok.Room),
            RoomWriteResult.NotFound => NotFound(),
            RoomWriteResult.BadRequest bad => BadRequest(
                new LodgingErrorDto { Code = bad.Code, Message = bad.Message }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    [HttpGet("folios")]
    [ProducesResponseType(typeof(IReadOnlyList<GuestFolioDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<GuestFolioDto>>> ListFolios(
        [FromQuery] Guid? roomId,
        [FromQuery] bool openOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var folios = await _lodging
            .ListFoliosAsync(tenantId, roomId, openOnly, ignoreFilters: false, cancellationToken)
            .ConfigureAwait(false);
        return Ok(folios);
    }

    private (string UserId, string Role) Actor()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin";
        var role = User.FindFirstValue(ClaimTypes.Role) ?? Roles.Manager;
        return (userId, role);
    }
}
