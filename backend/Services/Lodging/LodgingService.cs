using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services.Lodging;

public interface ILodgingService
{
    Task<IReadOnlyList<RoomDto>> ListRoomsAsync(Guid tenantId, bool ignoreFilters, CancellationToken cancellationToken);
    Task<RoomWriteResult> CreateRoomAsync(
        Guid tenantId,
        CreateRoomRequest request,
        bool ignoreFilters,
        CancellationToken cancellationToken,
        string? actorUserId = null,
        string? actorRole = null);
    Task<IReadOnlyList<GuestFolioDto>> ListFoliosAsync(
        Guid tenantId,
        Guid? roomId,
        bool openOnly,
        bool ignoreFilters,
        CancellationToken cancellationToken,
        DateTime? closedFromUtc = null,
        DateTime? closedToUtc = null);
    Task<FolioWriteResult> CreateFolioAsync(
        Guid tenantId,
        CreateGuestFolioRequest request,
        bool ignoreFilters,
        CancellationToken cancellationToken,
        string? actorUserId = null,
        string? actorRole = null);
    Task<RoomDto?> GetRoomAsync(
        Guid tenantId,
        Guid roomId,
        bool ignoreFilters,
        CancellationToken cancellationToken);

    Task<RoomWriteResult> UpdateRoomStatusAsync(
        Guid tenantId,
        Guid roomId,
        UpdateRoomStatusRequest request,
        bool ignoreFilters,
        CancellationToken cancellationToken,
        string? actorUserId = null,
        string? actorRole = null);

    Task<RoomWriteResult> UpdateRoomDetailsAsync(
        Guid tenantId,
        Guid roomId,
        UpdateRoomDetailsRequest request,
        bool ignoreFilters,
        CancellationToken cancellationToken,
        string? actorUserId = null,
        string? actorRole = null);

    Task<RoomWriteResult> DeleteUnusedRoomAsync(
        Guid tenantId,
        Guid roomId,
        bool ignoreFilters,
        CancellationToken cancellationToken,
        string? actorUserId = null,
        string? actorRole = null);

    Task<GuestFolioDto?> GetFolioAsync(
        Guid tenantId,
        Guid folioId,
        bool ignoreFilters,
        CancellationToken cancellationToken);
    Task<FolioWriteResult> UpdateFolioAsync(
        Guid tenantId,
        Guid folioId,
        UpdateGuestFolioRequest request,
        bool ignoreFilters,
        CancellationToken cancellationToken,
        string? actorUserId = null,
        string? actorRole = null);
    Task<FolioWriteResult> ChargeFolioAsync(
        Guid tenantId,
        Guid folioId,
        ChargeFolioRequest request,
        bool ignoreFilters,
        CancellationToken cancellationToken,
        string? actorUserId = null,
        string? actorRole = null);

    Task<FolioWriteResult> MoveRoomAsync(
        Guid tenantId,
        Guid folioId,
        Guid newRoomId,
        bool ignoreFilters,
        CancellationToken cancellationToken,
        string? actorUserId = null,
        string? actorRole = null);

    /// <summary>
    /// Replaces the cashier's active cart with one stay line priced at the open folio balance.
    /// Does not create a payment or a TSE receipt.
    /// </summary>
    Task<StageCheckoutResult> StageCheckoutCartAsync(
        Guid tenantId,
        Guid folioId,
        Guid productId,
        string actorUserId,
        bool ignoreFilters,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<GuestFolioItemDto>?> ListFolioItemsAsync(
        Guid tenantId,
        Guid folioId,
        bool ignoreFilters,
        CancellationToken cancellationToken);

    Task<MinibarWriteResult> AddRoomMinibarAsync(
        Guid tenantId,
        Guid roomId,
        AddRoomMinibarRequest request,
        bool ignoreFilters,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RoomMinibarItemDto>?> ListRoomMinibarAsync(
        Guid tenantId,
        Guid roomId,
        bool ignoreFilters,
        CancellationToken cancellationToken);
}

public abstract record RoomWriteResult
{
    public sealed record Ok(RoomDto Room) : RoomWriteResult;
    public sealed record NotFound() : RoomWriteResult;
    public sealed record Conflict(string Code, string Message) : RoomWriteResult;
    public sealed record BadRequest(string Code, string Message) : RoomWriteResult;
}

public abstract record FolioWriteResult
{
    public sealed record Ok(GuestFolioDto Folio) : FolioWriteResult;
    public sealed record NotFound() : FolioWriteResult;
    public sealed record Conflict(string Code, string Message) : FolioWriteResult;
    public sealed record BadRequest(string Code, string Message) : FolioWriteResult;
}

public abstract record MinibarWriteResult
{
    public sealed record Ok(RoomMinibarItemDto Item) : MinibarWriteResult;
    public sealed record NotFound() : MinibarWriteResult;
    public sealed record BadRequest(string Code, string Message) : MinibarWriteResult;
}

public abstract record StageCheckoutResult
{
    public sealed record Ok(StageFolioCheckoutDto Checkout) : StageCheckoutResult;
    public sealed record NotFound() : StageCheckoutResult;
    public sealed record Conflict(string Code, string Message) : StageCheckoutResult;
    public sealed record BadRequest(string Code, string Message) : StageCheckoutResult;
}

public sealed class LodgingService : ILodgingService
{
    private readonly AppDbContext _db;
    private readonly IAuditLogService? _audit;
    private readonly ITenantExtensionRegistry? _extensions;
    private readonly ILogger<LodgingService>? _logger;

    public LodgingService(
        AppDbContext db,
        IAuditLogService? audit = null,
        ITenantExtensionRegistry? extensions = null,
        ILogger<LodgingService>? logger = null)
    {
        _db = db;
        _audit = audit;
        _extensions = extensions;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RoomDto>> ListRoomsAsync(
        Guid tenantId,
        bool ignoreFilters,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var rooms = await RoomsQuery(ignoreFilters)
            .Where(row => row.TenantId == tenantId)
            .OrderBy(row => row.Number)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var occupied = await FoliosQuery(ignoreFilters)
            .Where(row => row.TenantId == tenantId
                && row.Status == GuestFolioStatus.Open
                && (row.CheckOut == null || row.CheckOut > now))
            .Select(row => row.RoomId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var occupiedSet = occupied.ToHashSet();
        return rooms.Select(row => MapRoom(row, occupiedSet.Contains(row.Id))).ToList();
    }

    public async Task<RoomWriteResult> CreateRoomAsync(
        Guid tenantId,
        CreateRoomRequest request,
        bool ignoreFilters,
        CancellationToken cancellationToken,
        string? actorUserId = null,
        string? actorRole = null)
    {
        var number = request.Number.Trim();
        var type = request.Type.Trim();
        var exists = await RoomsQuery(ignoreFilters)
            .AnyAsync(row => row.TenantId == tenantId && row.Number == number, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            return new RoomWriteResult.Conflict(
                LodgingErrorCodes.RoomNumberDuplicate,
                "A room with this number already exists.");
        }

        var room = new Room
        {
            TenantId = tenantId,
            Number = number,
            Type = type,
            Capacity = Math.Clamp(request.Capacity, 1, 20),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        _db.Rooms.Add(room);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await AuditAsync(
            AuditEventType.RoomCreated,
            "ROOM_CREATED",
            "Room",
            room.Id,
            tenantId,
            actorUserId,
            actorRole,
            $"Room {room.Number} created.",
            cancellationToken).ConfigureAwait(false);
        return new RoomWriteResult.Ok(MapRoom(room, occupied: false));
    }

    public async Task<RoomWriteResult> UpdateRoomStatusAsync(
        Guid tenantId,
        Guid roomId,
        UpdateRoomStatusRequest request,
        bool ignoreFilters,
        CancellationToken cancellationToken,
        string? actorUserId = null,
        string? actorRole = null)
    {
        if (!Enum.TryParse<RoomStatus>(request.Status?.Trim(), ignoreCase: true, out var status)
            || !Enum.IsDefined(status))
        {
            return new RoomWriteResult.BadRequest(
                LodgingErrorCodes.InvalidStatus,
                "Room status must be Available, Occupied, Cleaning, or Maintenance.");
        }

        var room = await RoomsQuery(ignoreFilters)
            .FirstOrDefaultAsync(row => row.Id == roomId && row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (room is null)
            return new RoomWriteResult.NotFound();

        var previous = room.Status;
        room.Status = status;
        room.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await AuditAsync(
            AuditEventType.RoomStatusChanged,
            "ROOM_STATUS_CHANGED",
            "Room",
            room.Id,
            tenantId,
            actorUserId,
            actorRole,
            $"Room {room.Number} status {previous} -> {status}.",
            cancellationToken).ConfigureAwait(false);
        return new RoomWriteResult.Ok(MapRoom(room, status == RoomStatus.Occupied));
    }

    public async Task<RoomDto?> GetRoomAsync(
        Guid tenantId,
        Guid roomId,
        bool ignoreFilters,
        CancellationToken cancellationToken)
    {
        var room = await RoomsQuery(ignoreFilters)
            .FirstOrDefaultAsync(row => row.Id == roomId && row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (room is null)
            return null;
        var occupied = await IsRoomOccupiedAsync(tenantId, room.Id, ignoreFilters, cancellationToken)
            .ConfigureAwait(false);
        return MapRoom(room, occupied);
    }

    public async Task<RoomWriteResult> UpdateRoomDetailsAsync(
        Guid tenantId,
        Guid roomId,
        UpdateRoomDetailsRequest request,
        bool ignoreFilters,
        CancellationToken cancellationToken,
        string? actorUserId = null,
        string? actorRole = null)
    {
        var room = await RoomsQuery(ignoreFilters)
            .FirstOrDefaultAsync(row => row.Id == roomId && row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (room is null)
            return new RoomWriteResult.NotFound();

        var number = request.Number.Trim();
        var duplicate = await RoomsQuery(ignoreFilters)
            .AnyAsync(
                row => row.TenantId == tenantId && row.Id != roomId && row.Number == number,
                cancellationToken)
            .ConfigureAwait(false);
        if (duplicate)
        {
            return new RoomWriteResult.Conflict(
                LodgingErrorCodes.RoomNumberDuplicate,
                "A room with this number already exists.");
        }

        room.Number = number;
        room.Type = request.Type.Trim();
        room.Capacity = Math.Clamp(request.Capacity, 1, 20);
        room.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await AuditAsync(
            AuditEventType.RoomStatusChanged,
            "ROOM_DETAILS_CHANGED",
            "Room",
            room.Id,
            tenantId,
            actorUserId,
            actorRole,
            $"Room {room.Number} details updated.",
            cancellationToken).ConfigureAwait(false);
        var occupied = await IsRoomOccupiedAsync(tenantId, room.Id, ignoreFilters, cancellationToken)
            .ConfigureAwait(false);
        return new RoomWriteResult.Ok(MapRoom(room, occupied));
    }

    public async Task<RoomWriteResult> DeleteUnusedRoomAsync(
        Guid tenantId,
        Guid roomId,
        bool ignoreFilters,
        CancellationToken cancellationToken,
        string? actorUserId = null,
        string? actorRole = null)
    {
        var room = await RoomsQuery(ignoreFilters)
            .FirstOrDefaultAsync(row => row.Id == roomId && row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (room is null)
            return new RoomWriteResult.NotFound();

        var used = await FoliosQuery(ignoreFilters)
            .AnyAsync(row => row.TenantId == tenantId && row.RoomId == roomId, cancellationToken)
            .ConfigureAwait(false);
        if (used)
        {
            return new RoomWriteResult.Conflict(
                LodgingErrorCodes.RoomInUse,
                "A room that already has a folio cannot be deleted.");
        }

        var snapshot = MapRoom(room, occupied: false);
        _db.Rooms.Remove(room);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await AuditAsync(
            AuditEventType.RoomStatusChanged,
            "ROOM_DELETED",
            "Room",
            room.Id,
            tenantId,
            actorUserId,
            actorRole,
            $"Unused room {snapshot.Number} deleted.",
            cancellationToken).ConfigureAwait(false);
        return new RoomWriteResult.Ok(snapshot);
    }

    public async Task<GuestFolioDto?> GetFolioAsync(
        Guid tenantId,
        Guid folioId,
        bool ignoreFilters,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var folio = await FoliosQuery(ignoreFilters)
            .FirstOrDefaultAsync(row => row.Id == folioId && row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        return folio is null ? null : MapFolio(folio, now);
    }

    public async Task<IReadOnlyList<GuestFolioDto>> ListFoliosAsync(
        Guid tenantId,
        Guid? roomId,
        bool openOnly,
        bool ignoreFilters,
        CancellationToken cancellationToken,
        DateTime? closedFromUtc = null,
        DateTime? closedToUtc = null)
    {
        var now = DateTime.UtcNow;
        var query = FoliosQuery(ignoreFilters)
            .Where(row => row.TenantId == tenantId);
        if (roomId is Guid id && id != Guid.Empty)
            query = query.Where(row => row.RoomId == id);
        if (openOnly)
            query = query.Where(row =>
                row.Status == GuestFolioStatus.Open && (row.CheckOut == null || row.CheckOut > now));
        else if (closedFromUtc is not null || closedToUtc is not null)
        {
            query = query.Where(row => row.Status == GuestFolioStatus.Closed && row.CheckOut != null);
            if (closedFromUtc is DateTime from)
                query = query.Where(row => row.CheckOut >= from);
            if (closedToUtc is DateTime to)
                query = query.Where(row => row.CheckOut < to);
        }

        var rows = await query
            .OrderByDescending(row => row.CheckIn)
            .Take(200)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(row => MapFolio(row, now)).ToList();
    }

    public async Task<FolioWriteResult> CreateFolioAsync(
        Guid tenantId,
        CreateGuestFolioRequest request,
        bool ignoreFilters,
        CancellationToken cancellationToken,
        string? actorUserId = null,
        string? actorRole = null)
    {
        var checkIn = Utc(request.CheckIn ?? DateTime.UtcNow);
        var checkOut = request.CheckOut is DateTime raw ? Utc(raw) : (DateTime?)null;
        if (checkOut is DateTime end && end <= checkIn)
        {
            return new FolioWriteResult.BadRequest(
                LodgingErrorCodes.InvalidStay,
                "Check-out must be after check-in.");
        }

        var room = await RoomsQuery(ignoreFilters)
            .FirstOrDefaultAsync(row => row.Id == request.RoomId && row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        var customer = await CustomersQuery(ignoreFilters)
            .FirstOrDefaultAsync(row => row.Id == request.CustomerId && row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (room is null || customer is null)
            return new FolioWriteResult.NotFound();

        var now = DateTime.UtcNow;
        var occupied = await FoliosQuery(ignoreFilters)
            .AnyAsync(
                row => row.TenantId == tenantId
                    && row.RoomId == room.Id
                    && row.Status == GuestFolioStatus.Open
                    && (row.CheckOut == null || row.CheckOut > now),
                cancellationToken)
            .ConfigureAwait(false);
        if (occupied)
        {
            return new FolioWriteResult.Conflict(
                LodgingErrorCodes.RoomOccupied,
                "This room already has an open folio.");
        }

        var staysOpen = checkOut is null || checkOut > now;
        var previousRoomStatus = room.Status;
        var folio = new GuestFolio
        {
            TenantId = tenantId,
            CustomerId = customer.Id,
            RoomId = room.Id,
            CheckIn = checkIn,
            CheckOut = checkOut,
            Status = staysOpen ? GuestFolioStatus.Open : GuestFolioStatus.Closed,
            Balance = request.Balance,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Customer = customer,
            Room = room,
        };

        // The folio row must exist before folio.opened. Save and the extension share one
        // transaction. A rejecting extension rolls the row back. The extension may add
        // guest_folio_items and update Balance; this method saves that change before commit.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        if (staysOpen)
            room.Status = RoomStatus.Occupied;
        _db.GuestFolios.Add(folio);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await TenantExtensionRunner.RunAsync(
                _extensions,
                tenantId,
                TenantExtensionHooks.FolioOpened,
                new Dictionary<string, object?>
                {
                    ["folioId"] = folio.Id,
                    ["roomId"] = folio.RoomId,
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (ExtensionFailedException ex)
        {
            _logger?.LogWarning(
                ex,
                "folio.opened rejected folio {FolioId} for tenant {TenantId}",
                folio.Id,
                tenantId);
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            _db.ChangeTracker.Clear();
            await RemoveRejectedFolioAsync(
                folio.Id,
                room.Id,
                staysOpen ? previousRoomStatus : null,
                cancellationToken).ConfigureAwait(false);
            return new FolioWriteResult.BadRequest(ExtensionFailedException.ErrorCode, ex.Message);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        await AuditAsync(
            AuditEventType.GuestFolioOpened,
            "FOLIO_OPENED",
            "GuestFolio",
            folio.Id,
            tenantId,
            actorUserId,
            actorRole,
            $"Folio opened for room {room.Number}.",
            cancellationToken).ConfigureAwait(false);
        return new FolioWriteResult.Ok(MapFolio(folio, DateTime.UtcNow));
    }

    /// <summary>
    /// In-memory tests ignore database transactions. After a real rollback this finds nothing.
    /// </summary>
    private async Task RemoveRejectedFolioAsync(
        Guid folioId,
        Guid roomId,
        RoomStatus? restoreRoomStatus,
        CancellationToken cancellationToken)
    {
        var changed = false;
        var items = await _db.GuestFolioItems
            .Where(row => row.FolioId == folioId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (items.Count > 0)
        {
            _db.GuestFolioItems.RemoveRange(items);
            changed = true;
        }

        var folio = await _db.GuestFolios
            .FirstOrDefaultAsync(row => row.Id == folioId, cancellationToken)
            .ConfigureAwait(false);
        if (folio is not null)
        {
            _db.GuestFolios.Remove(folio);
            changed = true;
        }

        if (restoreRoomStatus is RoomStatus status)
        {
            var room = await _db.Rooms
                .FirstOrDefaultAsync(row => row.Id == roomId, cancellationToken)
                .ConfigureAwait(false);
            if (room is not null && room.Status != status)
            {
                room.Status = status;
                changed = true;
            }
        }

        if (changed)
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<FolioWriteResult> UpdateFolioAsync(
        Guid tenantId,
        Guid folioId,
        UpdateGuestFolioRequest request,
        bool ignoreFilters,
        CancellationToken cancellationToken,
        string? actorUserId = null,
        string? actorRole = null)
    {
        var folio = await TrackingFolios(ignoreFilters)
            .Include(row => row.Customer)
            .Include(row => row.Room)
            .FirstOrDefaultAsync(row => row.Id == folioId && row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (folio is null)
            return new FolioWriteResult.NotFound();

        if (request.Notes is not null)
            folio.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        var closing = false;
        if (request.CheckOut is DateTime raw)
        {
            var checkOut = Utc(raw);
            if (checkOut <= folio.CheckIn)
            {
                return new FolioWriteResult.BadRequest(
                    LodgingErrorCodes.InvalidStay,
                    "Check-out must be after check-in.");
            }

            folio.CheckOut = checkOut;
            closing = true;
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<GuestFolioStatus>(request.Status.Trim(), ignoreCase: true, out var status)
                || !Enum.IsDefined(status))
            {
                return new FolioWriteResult.BadRequest(
                    LodgingErrorCodes.InvalidStatus,
                    "Folio status must be Open, Closed, or Cancelled.");
            }

            folio.Status = status;
            closing = status is GuestFolioStatus.Closed or GuestFolioStatus.Cancelled;
            if (closing && folio.CheckOut is null)
                folio.CheckOut = DateTime.UtcNow;
        }
        else if (closing)
        {
            folio.Status = GuestFolioStatus.Closed;
        }

        folio.UpdatedAtUtc = DateTime.UtcNow;
        if (closing && folio.Room is not null && folio.Status != GuestFolioStatus.Cancelled)
            folio.Room.Status = RoomStatus.Cleaning;
        if (folio.Status == GuestFolioStatus.Cancelled && folio.Room is not null)
            folio.Room.Status = RoomStatus.Available;

        if (closing)
        {
            try
            {
                await TenantExtensionRunner.RunAsync(
                    _extensions,
                    tenantId,
                    TenantExtensionHooks.FolioBeforeClose,
                    new Dictionary<string, object?>
                    {
                        ["folioId"] = folio.Id,
                        ["roomId"] = folio.RoomId,
                    },
                    cancellationToken).ConfigureAwait(false);
            }
            catch (ExtensionFailedException ex)
            {
                _db.ChangeTracker.Clear();
                return new FolioWriteResult.BadRequest(ExtensionFailedException.ErrorCode, ex.Message);
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (closing)
        {
            await AuditAsync(
                AuditEventType.FolioClosed,
                "FOLIO_CLOSED",
                "GuestFolio",
                folio.Id,
                tenantId,
                actorUserId,
                actorRole,
                $"Folio {folio.Status}.",
                cancellationToken).ConfigureAwait(false);
        }

        return new FolioWriteResult.Ok(MapFolio(folio, DateTime.UtcNow));
    }

    public async Task<FolioWriteResult> ChargeFolioAsync(
        Guid tenantId,
        Guid folioId,
        ChargeFolioRequest request,
        bool ignoreFilters,
        CancellationToken cancellationToken,
        string? actorUserId = null,
        string? actorRole = null)
    {
        var folio = await TrackingFolios(ignoreFilters)
            .Include(row => row.Customer)
            .Include(row => row.Room)
            .FirstOrDefaultAsync(row => row.Id == folioId && row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (folio is null)
            return new FolioWriteResult.NotFound();
        if (folio.Status != GuestFolioStatus.Open)
        {
            return new FolioWriteResult.Conflict(
                LodgingErrorCodes.FolioClosed,
                "Charges can be added only to an open folio.");
        }

        var description = request.Description.Trim();
        var amount = decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        _db.GuestFolioItems.Add(new GuestFolioItem
        {
            FolioId = folio.Id,
            PaymentDetailId = null,
            Description = description,
            Amount = amount,
            CreatedAtUtc = DateTime.UtcNow,
        });
        folio.Balance = decimal.Round(folio.Balance + amount, 2, MidpointRounding.AwayFromZero);
        folio.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await AuditAsync(
            AuditEventType.FolioCharged,
            "FOLIO_CHARGED",
            "GuestFolio",
            folio.Id,
            tenantId,
            actorUserId,
            actorRole,
            $"Folio charge {amount:0.00} ({description}). No payment row and no TSE receipt.",
            cancellationToken).ConfigureAwait(false);
        return new FolioWriteResult.Ok(MapFolio(folio, DateTime.UtcNow));
    }

    public async Task<FolioWriteResult> MoveRoomAsync(
        Guid tenantId,
        Guid folioId,
        Guid newRoomId,
        bool ignoreFilters,
        CancellationToken cancellationToken,
        string? actorUserId = null,
        string? actorRole = null)
    {
        var folio = await TrackingFolios(ignoreFilters)
            .Include(row => row.Customer)
            .Include(row => row.Room)
            .FirstOrDefaultAsync(row => row.Id == folioId && row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (folio is null)
            return new FolioWriteResult.NotFound();
        if (folio.Status != GuestFolioStatus.Open)
        {
            return new FolioWriteResult.Conflict(
                LodgingErrorCodes.FolioNotOpen,
                "Only an open folio can change rooms.");
        }

        if (folio.RoomId == newRoomId)
            return new FolioWriteResult.Ok(MapFolio(folio, DateTime.UtcNow));

        var rooms = ignoreFilters ? _db.Rooms.IgnoreQueryFilters() : _db.Rooms;
        var next = await rooms
            .FirstOrDefaultAsync(row => row.Id == newRoomId && row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (next is null)
            return new FolioWriteResult.NotFound();
        if (next.Status != RoomStatus.Available)
        {
            return new FolioWriteResult.Conflict(
                LodgingErrorCodes.RoomNotAvailable,
                "The target room is not available.");
        }

        var previous = folio.Room ?? await rooms
            .FirstOrDefaultAsync(row => row.Id == folio.RoomId && row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (previous is null)
            return new FolioWriteResult.NotFound();

        var now = DateTime.UtcNow;
        var line = $"Zimmerwechsel: {previous.Number} → {next.Number} ({now:yyyy-MM-dd HH:mm} UTC)";
        var oldRoomId = previous.Id;
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        previous.Status = RoomStatus.Cleaning;
        previous.UpdatedAtUtc = now;
        next.Status = RoomStatus.Occupied;
        next.UpdatedAtUtc = now;
        folio.RoomId = next.Id;
        folio.Room = next;
        folio.Notes = string.IsNullOrWhiteSpace(folio.Notes)
            ? line
            : folio.Notes.TrimEnd() + Environment.NewLine + line;
        folio.UpdatedAtUtc = now;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        await AuditAsync(
            AuditEventType.FolioRoomMoved,
            "FOLIO_ROOM_MOVED",
            "GuestFolio",
            folio.Id,
            tenantId,
            actorUserId,
            actorRole,
            line,
            cancellationToken,
            new
            {
                tenantId,
                folioId = folio.Id,
                oldRoomId,
                newRoomId = next.Id,
                actorUserId = string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId,
            }).ConfigureAwait(false);

        return new FolioWriteResult.Ok(MapFolio(folio, now));
    }

    public async Task<StageCheckoutResult> StageCheckoutCartAsync(
        Guid tenantId,
        Guid folioId,
        Guid productId,
        string actorUserId,
        bool ignoreFilters,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            return new StageCheckoutResult.BadRequest(
                LodgingErrorCodes.InvalidStatus,
                "An authenticated cashier is required to stage a folio checkout.");
        }

        var folio = await TrackingFolios(ignoreFilters)
            .Include(row => row.Room)
            .FirstOrDefaultAsync(row => row.Id == folioId && row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (folio is null)
            return new StageCheckoutResult.NotFound();
        if (folio.Status != GuestFolioStatus.Open)
        {
            return new StageCheckoutResult.Conflict(
                LodgingErrorCodes.FolioClosed,
                "Only an open folio can be sent to the register.");
        }

        var balance = decimal.Round(folio.Balance, 2, MidpointRounding.AwayFromZero);
        if (balance <= 0)
        {
            return new StageCheckoutResult.BadRequest(
                LodgingErrorCodes.ZeroBalance,
                "The folio balance is zero. Close the folio without a register sale.");
        }

        var products = ignoreFilters ? _db.Products.IgnoreQueryFilters() : _db.Products;
        var product = await products
            .Include(row => row.CategoryNavigation)
            .FirstOrDefaultAsync(row => row.Id == productId && row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (product is null)
            return new StageCheckoutResult.NotFound();
        if (!product.IsActive || product.ImeiTracked || product.CategoryId == Guid.Empty || product.CategoryNavigation is null)
        {
            return new StageCheckoutResult.BadRequest(
                LodgingErrorCodes.ProductUnsuitable,
                "The stay line needs an active catalog product with a tax category and without IMEI tracking.");
        }

        var lineName = LodgingStayLine.Format(folio.Room?.Number ?? folio.RoomId.ToString());
        var tableNumber = LodgingStayLine.CheckoutTableNumber;
        var cart = await _db.Carts
            .FirstOrDefaultAsync(
                row => row.UserId == actorUserId
                    && row.TableNumber == tableNumber
                    && row.Status == CartStatus.Active,
                cancellationToken)
            .ConfigureAwait(false);
        if (cart is null)
        {
            cart = new Cart
            {
                CartId = Guid.NewGuid().ToString(),
                TableNumber = tableNumber,
                WaiterName = "Kasse",
                UserId = actorUserId,
                CustomerId = folio.CustomerId,
                Notes = lineName,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                Status = CartStatus.Active,
            };
            _db.Carts.Add(cart);
        }
        else
        {
            cart.CustomerId = folio.CustomerId;
            cart.Notes = lineName;
            cart.ExpiresAt = DateTime.UtcNow.AddHours(24);
            cart.UpdatedAt = DateTime.UtcNow;
        }

        var existing = await _db.CartItems
            .Where(row => row.CartId == cart.CartId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (existing.Count > 0)
        {
            var itemIds = existing.Select(row => row.Id).ToList();
            var modifiers = await _db.CartItemModifiers
                .Where(row => itemIds.Contains(row.CartItemId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (modifiers.Count > 0)
                _db.CartItemModifiers.RemoveRange(modifiers);
            _db.CartItems.RemoveRange(existing);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        _db.CartItems.Add(new CartItem
        {
            CartId = cart.CartId,
            ProductId = product.Id,
            Quantity = 1,
            UnitPrice = balance,
            Notes = lineName,
            AppliedPricingRuleId = null,
        });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new StageCheckoutResult.Ok(new StageFolioCheckoutDto
        {
            LineName = lineName,
            Balance = balance,
            ProductId = product.Id,
            TableNumber = tableNumber,
        });
    }

    public async Task<IReadOnlyList<GuestFolioItemDto>?> ListFolioItemsAsync(
        Guid tenantId,
        Guid folioId,
        bool ignoreFilters,
        CancellationToken cancellationToken)
    {
        var folioExists = await FoliosQuery(ignoreFilters)
            .AnyAsync(row => row.Id == folioId && row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!folioExists)
            return null;

        var items = await ItemsQuery(ignoreFilters)
            .Where(row => row.FolioId == folioId)
            .OrderBy(row => row.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return items.Select(row => new GuestFolioItemDto
        {
            Id = row.Id,
            FolioId = row.FolioId,
            PaymentDetailId = row.PaymentDetailId,
            Description = row.Description,
            Amount = row.Amount,
            CreatedAtUtc = row.CreatedAtUtc,
        }).ToList();
    }

    private IQueryable<Room> RoomsQuery(bool ignoreFilters) =>
        ignoreFilters ? _db.Rooms.IgnoreQueryFilters() : _db.Rooms;

    private IQueryable<GuestFolio> FoliosQuery(bool ignoreFilters)
    {
        var query = ignoreFilters ? _db.GuestFolios.IgnoreQueryFilters() : _db.GuestFolios;
        return query.AsNoTracking().Include(row => row.Customer).Include(row => row.Room);
    }

    private IQueryable<GuestFolio> TrackingFolios(bool ignoreFilters) =>
        ignoreFilters ? _db.GuestFolios.IgnoreQueryFilters() : _db.GuestFolios;

    private IQueryable<GuestFolioItem> ItemsQuery(bool ignoreFilters)
    {
        var query = ignoreFilters ? _db.GuestFolioItems.IgnoreQueryFilters() : _db.GuestFolioItems;
        return query.AsNoTracking();
    }

    private IQueryable<Customer> CustomersQuery(bool ignoreFilters) =>
        ignoreFilters ? _db.Customers.IgnoreQueryFilters() : _db.Customers;

    private Task<bool> IsRoomOccupiedAsync(
        Guid tenantId,
        Guid roomId,
        bool ignoreFilters,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return FoliosQuery(ignoreFilters).AnyAsync(
            row => row.TenantId == tenantId
                && row.RoomId == roomId
                && row.Status == GuestFolioStatus.Open
                && (row.CheckOut == null || row.CheckOut > now),
            cancellationToken);
    }

    private static bool IsOpen(GuestFolio row, DateTime now) =>
        row.Status == GuestFolioStatus.Open && (row.CheckOut == null || row.CheckOut > now);

    private static DateTime Utc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static RoomDto MapRoom(Room row, bool occupied) =>
        new()
        {
            Id = row.Id,
            Number = row.Number,
            Type = row.Type,
            Capacity = row.Capacity,
            Status = row.Status == RoomStatus.Available && occupied
                ? RoomStatus.Occupied.ToString()
                : row.Status.ToString(),
            IsActive = row.IsActive,
            Occupied = occupied || row.Status == RoomStatus.Occupied,
        };

    private static GuestFolioDto MapFolio(GuestFolio row, DateTime now) =>
        new()
        {
            Id = row.Id,
            CustomerId = row.CustomerId,
            CustomerName = row.Customer?.Name,
            RoomId = row.RoomId,
            RoomNumber = row.Room?.Number,
            CheckIn = row.CheckIn,
            CheckOut = row.CheckOut,
            Status = row.Status.ToString(),
            Balance = row.Balance,
            Notes = row.Notes,
            IsOpen = IsOpen(row, now),
        };

    public async Task<MinibarWriteResult> AddRoomMinibarAsync(
        Guid tenantId,
        Guid roomId,
        AddRoomMinibarRequest request,
        bool ignoreFilters,
        CancellationToken cancellationToken)
    {
        var rooms = ignoreFilters ? _db.Rooms.IgnoreQueryFilters() : _db.Rooms;
        var room = await rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == roomId && row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (room is null)
            return new MinibarWriteResult.NotFound();

        if (request.ProductId is Guid productId)
        {
            var products = ignoreFilters ? _db.Products.IgnoreQueryFilters() : _db.Products;
            var exists = await products
                .AsNoTracking()
                .AnyAsync(row => row.Id == productId && row.TenantId == tenantId, cancellationToken)
                .ConfigureAwait(false);
            if (!exists)
                return new MinibarWriteResult.NotFound();
        }

        var description = request.Description.Trim();
        if (description.Length == 0 || request.Quantity < 1)
        {
            return new MinibarWriteResult.BadRequest(
                LodgingErrorCodes.InvalidStatus,
                "Minibar description and quantity are required.");
        }

        var now = DateTime.UtcNow;
        var row = new RoomMinibarItem
        {
            TenantId = tenantId,
            RoomId = roomId,
            ProductId = request.ProductId,
            Description = description,
            Quantity = request.Quantity,
            UnitPrice = decimal.Round(request.UnitPrice, 2, MidpointRounding.AwayFromZero),
            Status = RoomMinibarItemStatus.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        _db.RoomMinibarItems.Add(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new MinibarWriteResult.Ok(MapMinibar(row));
    }

    public async Task<IReadOnlyList<RoomMinibarItemDto>?> ListRoomMinibarAsync(
        Guid tenantId,
        Guid roomId,
        bool ignoreFilters,
        CancellationToken cancellationToken)
    {
        var rooms = ignoreFilters ? _db.Rooms.IgnoreQueryFilters() : _db.Rooms;
        var roomExists = await rooms
            .AsNoTracking()
            .AnyAsync(row => row.Id == roomId && row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!roomExists)
            return null;

        var items = ignoreFilters ? _db.RoomMinibarItems.IgnoreQueryFilters() : _db.RoomMinibarItems;
        var rows = await items
            .AsNoTracking()
            .Where(row => row.TenantId == tenantId && row.RoomId == roomId)
            .OrderByDescending(row => row.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(MapMinibar).ToArray();
    }

    private static RoomMinibarItemDto MapMinibar(RoomMinibarItem row) =>
        new()
        {
            Id = row.Id,
            RoomId = row.RoomId,
            ProductId = row.ProductId,
            Description = row.Description,
            Quantity = row.Quantity,
            UnitPrice = row.UnitPrice,
            Status = row.Status.ToString(),
            ChargedFolioId = row.ChargedFolioId,
            ChargedAtUtc = row.ChargedAtUtc,
            CreatedAtUtc = row.CreatedAtUtc,
        };

    private async Task AuditAsync(
        AuditEventType actionType,
        string action,
        string entityType,
        Guid entityId,
        Guid tenantId,
        string? actorUserId,
        string? actorRole,
        string description,
        CancellationToken cancellationToken,
        object? newValues = null)
    {
        if (_audit is null)
            return;
        _ = cancellationToken;
        await _audit.LogSystemOperationAsync(
                action: action,
                entityType: entityType,
                userId: string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId,
                userRole: string.IsNullOrWhiteSpace(actorRole) ? "Cashier" : actorRole,
                description: description,
                status: AuditLogStatus.Success,
                correlationIdOverride: Guid.NewGuid().ToString("N"),
                actionType: actionType,
                entityId: entityId,
                tenantId: tenantId,
                newValues: newValues)
            .ConfigureAwait(false);
    }
}
