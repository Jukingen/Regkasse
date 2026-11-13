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
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var folios = await _lodging
            .ListFoliosAsync(
                tenantId,
                roomId,
                openOnly,
                ignoreFilters: false,
                cancellationToken,
                fromUtc,
                toUtc)
            .ConfigureAwait(false);
        return Ok(folios);
    }

    [HttpGet("rooms/{id:guid}/minibar")]
    [ProducesResponseType(typeof(IReadOnlyList<RoomMinibarItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RoomMinibarItemDto>>> ListRoomMinibar(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var rows = await _lodging
            .ListRoomMinibarAsync(tenantId, id, ignoreFilters: false, cancellationToken)
            .ConfigureAwait(false);
        return rows is null ? NotFound() : Ok(rows);
    }

    [HttpGet("rooms/{id:guid}")]
    [ProducesResponseType(typeof(RoomDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomDto>> GetRoom(Guid id, CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();
        var room = await _lodging.GetRoomAsync(tenantId, id, ignoreFilters: false, cancellationToken)
            .ConfigureAwait(false);
        return room is null ? NotFound() : Ok(room);
    }

    [HttpPatch("rooms/{id:guid}/details")]
    [HasPermission(AppPermissions.ProductManage)]
    [ProducesResponseType(typeof(RoomDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LodgingErrorDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomDto>> UpdateRoomDetails(
        Guid id,
        [FromBody] UpdateRoomDetailsRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var (userId, role) = Actor();
        var result = await _lodging
            .UpdateRoomDetailsAsync(tenantId, id, request, ignoreFilters: false, cancellationToken, userId, role)
            .ConfigureAwait(false);
        return MapRoom(result);
    }

    [HttpDelete("rooms/{id:guid}")]
    [HasPermission(AppPermissions.ProductManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(LodgingErrorDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRoom(Guid id, CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var (userId, role) = Actor();
        var result = await _lodging
            .DeleteUnusedRoomAsync(tenantId, id, ignoreFilters: false, cancellationToken, userId, role)
            .ConfigureAwait(false);
        return result switch
        {
            RoomWriteResult.Ok => NoContent(),
            RoomWriteResult.NotFound => NotFound(),
            RoomWriteResult.Conflict conflict => Conflict(
                new LodgingErrorDto { Code = conflict.Code, Message = conflict.Message }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    [HttpGet("folios/{id:guid}")]
    [ProducesResponseType(typeof(GuestFolioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GuestFolioDto>> GetFolio(Guid id, CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();
        var folio = await _lodging.GetFolioAsync(tenantId, id, ignoreFilters: false, cancellationToken)
            .ConfigureAwait(false);
        return folio is null ? NotFound() : Ok(folio);
    }

    [HttpGet("folios/{id:guid}/items")]
    [ProducesResponseType(typeof(IReadOnlyList<GuestFolioItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<GuestFolioItemDto>>> ListFolioItems(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();
        var items = await _lodging
            .ListFolioItemsAsync(tenantId, id, ignoreFilters: false, cancellationToken)
            .ConfigureAwait(false);
        return items is null ? NotFound() : Ok(items);
    }

    [HttpPost("folios/{id:guid}/charge")]
    [HasPermission(AppPermissions.ProductManage)]
    [ProducesResponseType(typeof(GuestFolioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LodgingErrorDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GuestFolioDto>> ChargeFolio(
        Guid id,
        [FromBody] ChargeFolioRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var (userId, role) = Actor();
        var result = await _lodging
            .ChargeFolioAsync(tenantId, id, request, ignoreFilters: false, cancellationToken, userId, role)
            .ConfigureAwait(false);
        return MapFolio(result);
    }

    [HttpPatch("folios/{id:guid}")]
    [HasPermission(AppPermissions.ProductManage)]
    [ProducesResponseType(typeof(GuestFolioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LodgingErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GuestFolioDto>> UpdateFolio(
        Guid id,
        [FromBody] UpdateGuestFolioRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var (userId, role) = Actor();
        var result = await _lodging
            .UpdateFolioAsync(tenantId, id, request, ignoreFilters: false, cancellationToken, userId, role)
            .ConfigureAwait(false);
        return MapFolio(result);
    }

    [HttpPost("folios/{id:guid}/move-room")]
    [HasPermission(AppPermissions.ProductManage)]
    [ProducesResponseType(typeof(GuestFolioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LodgingErrorDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GuestFolioDto>> MoveRoom(
        Guid id,
        [FromBody] MoveFolioRoomRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var (userId, role) = Actor();
        var result = await _lodging
            .MoveRoomAsync(tenantId, id, request.NewRoomId, ignoreFilters: false, cancellationToken, userId, role)
            .ConfigureAwait(false);
        return MapFolio(result);
    }

    private ActionResult<RoomDto> MapRoom(RoomWriteResult result) =>
        result switch
        {
            RoomWriteResult.Ok ok => Ok(ok.Room),
            RoomWriteResult.NotFound => NotFound(),
            RoomWriteResult.Conflict conflict => Conflict(
                new LodgingErrorDto { Code = conflict.Code, Message = conflict.Message }),
            RoomWriteResult.BadRequest bad => BadRequest(
                new LodgingErrorDto { Code = bad.Code, Message = bad.Message }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };

    private ActionResult<GuestFolioDto> MapFolio(FolioWriteResult result) =>
        result switch
        {
            FolioWriteResult.Ok ok => Ok(ok.Folio),
            FolioWriteResult.NotFound => NotFound(),
            FolioWriteResult.Conflict conflict => Conflict(
                new LodgingErrorDto { Code = conflict.Code, Message = conflict.Message }),
            FolioWriteResult.BadRequest bad => BadRequest(
                new LodgingErrorDto { Code = bad.Code, Message = bad.Message }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };

    private (string UserId, string Role) Actor()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin";
        var role = User.FindFirstValue(ClaimTypes.Role) ?? Roles.Manager;
        return (userId, role);
    }
}
