using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;

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
        CancellationToken cancellationToken);
    Task<FolioWriteResult> CreateFolioAsync(
        Guid tenantId,
        CreateGuestFolioRequest request,
        bool ignoreFilters,
        CancellationToken cancellationToken,
        string? actorUserId = null,
        string? actorRole = null);
    Task<RoomWriteResult> UpdateRoomStatusAsync(
        Guid tenantId,
        Guid roomId,
        UpdateRoomStatusRequest request,
        bool ignoreFilters,
        CancellationToken cancellationToken,
        string? actorUserId = null,
        string? actorRole = null);
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
    Task<IReadOnlyList<GuestFolioItemDto>?> ListFolioItemsAsync(
        Guid tenantId,
        Guid folioId,
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

public sealed class LodgingService : ILodgingService
{
    private readonly AppDbContext _db;
    private readonly IAuditLogService? _audit;

    public LodgingService(AppDbContext db, IAuditLogService? audit = null)
    {
        _db = db;
        _audit = audit;
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

    public async Task<IReadOnlyList<GuestFolioDto>> ListFoliosAsync(
        Guid tenantId,
        Guid? roomId,
        bool openOnly,
        bool ignoreFilters,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var query = FoliosQuery(ignoreFilters)
            .Where(row => row.TenantId == tenantId);
        if (roomId is Guid id && id != Guid.Empty)
            query = query.Where(row => row.RoomId == id);
        if (openOnly)
            query = query.Where(row =>
                row.Status == GuestFolioStatus.Open && (row.CheckOut == null || row.CheckOut > now));

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
        var folio = new GuestFolio
        {
            TenantId = tenantId,
            CustomerId = customer.Id,
            RoomId = room.Id,
            CheckIn = checkIn,
            CheckOut = checkOut,
            Status = staysOpen ? GuestFolioStatus.Open : GuestFolioStatus.Closed,
            Balance = request.Balance,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Customer = customer,
            Room = room,
        };
        if (staysOpen)
            room.Status = RoomStatus.Occupied;
        _db.GuestFolios.Add(folio);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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

    private async Task AuditAsync(
        AuditEventType actionType,
        string action,
        string entityType,
        Guid entityId,
        Guid tenantId,
        string? actorUserId,
        string? actorRole,
        string description,
        CancellationToken cancellationToken)
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
                tenantId: tenantId)
            .ConfigureAwait(false);
    }
}
