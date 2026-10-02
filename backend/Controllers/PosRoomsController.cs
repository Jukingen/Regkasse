using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services.Lodging;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/pos")]
[Produces("application/json")]
[HasPermission(AppPermissions.CartView)]
public sealed class PosRoomsController : ControllerBase
{
    private readonly ILodgingService _lodging;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public PosRoomsController(ILodgingService lodging, ICurrentTenantAccessor tenantAccessor)
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
    [ProducesResponseType(typeof(RoomDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(LodgingErrorDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomDto>> CreateRoom(
        [FromBody] CreateRoomRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        if (!IsManager())
            return Forbid();
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
        [FromQuery] bool openOnly = true,
        CancellationToken cancellationToken = default)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var folios = await _lodging
            .ListFoliosAsync(tenantId, roomId, openOnly, ignoreFilters: false, cancellationToken)
            .ConfigureAwait(false);
        return Ok(folios);
    }

    [HttpPost("folios")]
    [ProducesResponseType(typeof(GuestFolioDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(LodgingErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(LodgingErrorDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GuestFolioDto>> CreateFolio(
        [FromBody] CreateGuestFolioRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var (userId, role) = Actor();
        var result = await _lodging
            .CreateFolioAsync(tenantId, request, ignoreFilters: false, cancellationToken, userId, role)
            .ConfigureAwait(false);
        return result switch
        {
            FolioWriteResult.Ok ok => StatusCode(StatusCodes.Status201Created, ok.Folio),
            FolioWriteResult.NotFound => NotFound(),
            FolioWriteResult.Conflict conflict => Conflict(
                new LodgingErrorDto { Code = conflict.Code, Message = conflict.Message }),
            FolioWriteResult.BadRequest bad => BadRequest(
                new LodgingErrorDto { Code = bad.Code, Message = bad.Message }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPatch("folios/{id:guid}")]
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

    [HttpPost("folios/{id:guid}/charge")]
    [ProducesResponseType(typeof(GuestFolioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LodgingErrorDto), StatusCodes.Status400BadRequest)]
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

    private bool IsManager() =>
        User.IsInRole(Roles.Manager) || User.IsInRole(Roles.SuperAdmin);

    private (string UserId, string Role) Actor()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "pos";
        var role = User.FindFirstValue(ClaimTypes.Role) ?? Roles.Cashier;
        return (userId, role);
    }
}
