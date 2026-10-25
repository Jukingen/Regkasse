using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services.Tickets;

public static class TicketSeatLabel
{
    public static string Format(TicketSeat seat)
    {
        var place = string.IsNullOrWhiteSpace(seat.Section)
            ? $"{seat.Row}-{seat.Number}"
            : $"{seat.Section} {seat.Row}-{seat.Number}";
        return place.Trim();
    }
}

public static class TicketSeatErrorCodes
{
    public const string SeatTaken = "SEAT_TAKEN";
    public const string SeatSold = "SEAT_SOLD";
    public const string CapacityReached = "EVENT_CAPACITY_REACHED";
    public const string CategoryCapacityReached = "CATEGORY_CAPACITY_REACHED";
    public const string InvalidEvent = "EVENT_INVALID";
    public const string InvalidSeat = "SEAT_INVALID";
    public const string CategoryPriceMissing = "CATEGORY_PRICE_MISSING";
    public const string DuplicateCategoryCode = "CATEGORY_CODE_DUPLICATE";
    public const string InvalidCategory = "CATEGORY_INVALID";
    public const string CompanionTargetInvalid = "COMPANION_TARGET_INVALID";
    public const string CompanionTargetNotSold = "COMPANION_TARGET_NOT_SOLD";
}

public sealed class TicketEventDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTime StartAtUtc { get; init; }
    public DateTime EndAtUtc { get; init; }
    public string Venue { get; init; } = string.Empty;
    public int Capacity { get; init; }
    public bool IsActive { get; init; }
    public string StageEdge { get; init; } = "top";
}

public sealed class TicketSeatDto
{
    public Guid Id { get; init; }
    public Guid EventId { get; init; }
    public string Row { get; init; } = string.Empty;
    public string Number { get; init; } = string.Empty;
    public string? Section { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? ReservedUntilUtc { get; init; }
    public string? ReservedByUserId { get; init; }
    public Guid? CategoryId { get; init; }
    public string? CategoryCode { get; init; }
    public string? CategoryName { get; init; }
    public int? GridColumn { get; init; }
    public string SeatRole { get; init; } = nameof(TicketSeatRole.Standard);
    public Guid? CompanionOfSeatId { get; init; }
    public decimal? Price { get; init; }
}

public sealed class UpsertTicketEventRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(255)]
    public string Name { get; init; } = string.Empty;

    public DateTime StartAtUtc { get; init; }
    public DateTime EndAtUtc { get; init; }

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(255)]
    public string Venue { get; init; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Range(0, 100000)]
    public int Capacity { get; init; }

    public bool IsActive { get; init; } = true;

    /// <summary><c>top</c> or <c>bottom</c>. Null keeps the stored value on update and defaults to <c>top</c> on create.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(16)]
    public string? StageEdge { get; init; }
}

public sealed class CreateTicketSeatRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(16)]
    public string Row { get; init; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(16)]
    public string Number { get; init; } = string.Empty;

    [System.ComponentModel.DataAnnotations.MaxLength(64)]
    public string? Section { get; init; }

    public Guid? CategoryId { get; init; }

    public int? GridColumn { get; init; }

    [System.ComponentModel.DataAnnotations.Range(0, 2)]
    public int SeatRole { get; init; }

    /// <summary>Accessible seat this companion seat accompanies. Null leaves the seat unpaired.</summary>
    public Guid? CompanionOfSeatId { get; init; }
}

public sealed class SeatActionRequest
{
    public Guid SeatId { get; init; }
}

public sealed class TicketPriceCategoryDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public bool IsActive { get; init; }
}

public sealed class UpsertTicketPriceCategoryRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(32)]
    public string Code { get; init; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(64)]
    public string Name { get; init; } = string.Empty;

    public int SortOrder { get; init; }

    public bool IsActive { get; init; } = true;
}

public sealed class TicketEventCategoryPriceDto
{
    public Guid Id { get; init; }
    public Guid EventId { get; init; }
    public Guid CategoryId { get; init; }
    public string? CategoryCode { get; init; }
    public string? CategoryName { get; init; }
    public decimal Price { get; init; }
    public int Capacity { get; init; }
}

public sealed class SetTicketEventCategoryPriceRequest
{
    public Guid CategoryId { get; init; }

    public decimal Price { get; init; }

    public int Capacity { get; init; }
}

public abstract record TicketEventUpdateResult
{
    public sealed record Ok(TicketEventDto Event) : TicketEventUpdateResult;
    public sealed record NotFound : TicketEventUpdateResult;
    public sealed record Invalid(string Message) : TicketEventUpdateResult;
}

public abstract record TicketCatalogResult
{
    public sealed record CategoryOk(TicketPriceCategoryDto Category) : TicketCatalogResult;
    public sealed record PriceOk(TicketEventCategoryPriceDto Price) : TicketCatalogResult;
    public sealed record NotFound : TicketCatalogResult;
    public sealed record Error(int StatusCode, string Code, string Message) : TicketCatalogResult;
}

public sealed class SoldTicketDto
{
    public Guid RedemptionId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string DisplayCode { get; init; } = string.Empty;
    public DateTime? ValidUntilUtc { get; init; }
    public string EventName { get; init; } = string.Empty;
    public string SeatLabel { get; init; } = string.Empty;
    public Guid? CategoryId { get; init; }
    public decimal? Price { get; init; }
}

public abstract record TicketSeatActionResult
{
    public sealed record OkSeat(TicketSeatDto Seat) : TicketSeatActionResult;
    public sealed record OkSale(SoldTicketDto Ticket) : TicketSeatActionResult;
    public sealed record NotFound : TicketSeatActionResult;
    public sealed record Error(int StatusCode, string Code, string Message) : TicketSeatActionResult;
}

public interface ITicketEventService
{
    Task<IReadOnlyList<TicketEventDto>> ListAsync(bool activeOnly, CancellationToken cancellationToken);
    Task<TicketEventDto?> GetAsync(Guid eventId, CancellationToken cancellationToken);
    Task<TicketEventDto?> CreateAsync(Guid tenantId, UpsertTicketEventRequest request, CancellationToken cancellationToken);
    Task<TicketEventUpdateResult> UpdateAsync(Guid eventId, UpsertTicketEventRequest request, CancellationToken cancellationToken);
    Task<bool> DeactivateAsync(Guid eventId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TicketSeatDto>?> ListSeatsAsync(Guid eventId, CancellationToken cancellationToken);
    Task<TicketSeatActionResult> AddSeatAsync(Guid eventId, CreateTicketSeatRequest request, CancellationToken cancellationToken);
    Task<TicketSeatActionResult> ReserveAsync(Guid eventId, Guid seatId, string actorUserId, CancellationToken cancellationToken);
    Task<TicketSeatActionResult> SellAsync(Guid tenantId, Guid eventId, Guid seatId, string actorUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TicketPriceCategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken);
    Task<TicketPriceCategoryDto?> GetCategoryAsync(Guid categoryId, CancellationToken cancellationToken);
    Task<TicketCatalogResult> CreateCategoryAsync(Guid tenantId, UpsertTicketPriceCategoryRequest request, string actorUserId, CancellationToken cancellationToken);
    Task<TicketCatalogResult> UpdateCategoryAsync(Guid categoryId, UpsertTicketPriceCategoryRequest request, string actorUserId, CancellationToken cancellationToken);
    Task<TicketCatalogResult> DeactivateCategoryAsync(Guid categoryId, string actorUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TicketEventCategoryPriceDto>?> ListCategoryPricesAsync(Guid eventId, CancellationToken cancellationToken);
    Task<TicketCatalogResult> SetCategoryPriceAsync(Guid eventId, SetTicketEventCategoryPriceRequest request, string actorUserId, CancellationToken cancellationToken);
}

public sealed class TicketEventService : ITicketEventService
{
    public static readonly TimeSpan ReservationHold = TimeSpan.FromMinutes(15);

    private readonly AppDbContext _db;
    private readonly IAuditLogService _audit;
    private readonly ILogger<TicketEventService> _logger;

    public TicketEventService(AppDbContext db, IAuditLogService audit, ILogger<TicketEventService> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TicketEventDto>> ListAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        var query = _db.TicketEvents.AsNoTracking().AsQueryable();
        if (activeOnly)
            query = query.Where(row => row.IsActive);
        var rows = await query
            .OrderBy(row => row.StartAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDto).ToList();
    }

    public async Task<TicketEventDto?> GetAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var row = await _db.TicketEvents.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == eventId, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : ToDto(row);
    }

    public async Task<TicketEventDto?> CreateAsync(
        Guid tenantId,
        UpsertTicketEventRequest request,
        CancellationToken cancellationToken)
    {
        if (!ValidWindow(request) || !TryResolveStageEdge(request.StageEdge, keepWhenMissing: false, current: "top", out var stageEdge))
            return null;
        var now = DateTime.UtcNow;
        var row = new TicketEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name.Trim(),
            StartAtUtc = request.StartAtUtc,
            EndAtUtc = request.EndAtUtc,
            Venue = request.Venue.Trim(),
            Capacity = request.Capacity,
            IsActive = request.IsActive,
            StageEdge = stageEdge,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        _db.TicketEvents.Add(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(row);
    }

    public async Task<TicketEventUpdateResult> UpdateAsync(
        Guid eventId,
        UpsertTicketEventRequest request,
        CancellationToken cancellationToken)
    {
        if (!ValidWindow(request))
            return new TicketEventUpdateResult.Invalid("Event window is invalid.");
        var row = await _db.TicketEvents.FirstOrDefaultAsync(item => item.Id == eventId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return new TicketEventUpdateResult.NotFound();
        if (!TryResolveStageEdge(request.StageEdge, keepWhenMissing: true, row.StageEdge, out var stageEdge))
            return new TicketEventUpdateResult.Invalid("Stage edge must be top or bottom.");
        row.Name = request.Name.Trim();
        row.StartAtUtc = request.StartAtUtc;
        row.EndAtUtc = request.EndAtUtc;
        row.Venue = request.Venue.Trim();
        row.Capacity = request.Capacity;
        row.IsActive = request.IsActive;
        row.StageEdge = stageEdge;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new TicketEventUpdateResult.Ok(ToDto(row));
    }

    public async Task<bool> DeactivateAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var row = await _db.TicketEvents.FirstOrDefaultAsync(item => item.Id == eventId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return false;
        row.IsActive = false;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<IReadOnlyList<TicketSeatDto>?> ListSeatsAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var exists = await _db.TicketEvents.AsNoTracking().AnyAsync(item => item.Id == eventId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            return null;
        var seats = await _db.TicketSeats.AsNoTracking()
            .Where(seat => seat.EventId == eventId)
            .OrderBy(seat => seat.Section)
            .ThenBy(seat => seat.Row)
            .ThenBy(seat => seat.GridColumn)
            .ThenBy(seat => seat.Number)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var labels = await LoadSeatLabelsAsync(eventId, seats, cancellationToken).ConfigureAwait(false);
        var now = DateTime.UtcNow;
        return seats.Select(seat => ToSeatDto(seat, now, labels)).ToList();
    }

    public async Task<TicketSeatActionResult> AddSeatAsync(
        Guid eventId,
        CreateTicketSeatRequest request,
        CancellationToken cancellationToken)
    {
        var ticketEvent = await _db.TicketEvents.FirstOrDefaultAsync(item => item.Id == eventId, cancellationToken)
            .ConfigureAwait(false);
        if (ticketEvent is null)
            return new TicketSeatActionResult.NotFound();
        if (!Enum.IsDefined(typeof(TicketSeatRole), request.SeatRole))
        {
            return new TicketSeatActionResult.Error(
                StatusCodes.Status400BadRequest,
                TicketSeatErrorCodes.InvalidSeat,
                "Seat role is invalid.");
        }

        if (request.GridColumn is < 0)
        {
            return new TicketSeatActionResult.Error(
                StatusCodes.Status400BadRequest,
                TicketSeatErrorCodes.InvalidSeat,
                "Grid column must be zero or greater.");
        }

        var role = (TicketSeatRole)request.SeatRole;
        var companionOfSeatId = request.CompanionOfSeatId is Guid companionId && companionId != Guid.Empty
            ? companionId
            : (Guid?)null;
        if (companionOfSeatId is Guid targetSeatId)
        {
            var target = await _db.TicketSeats.AsNoTracking()
                .FirstOrDefaultAsync(
                    seat => seat.Id == targetSeatId && seat.EventId == eventId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (role != TicketSeatRole.Companion || target is null || target.SeatRole != TicketSeatRole.Accessible)
            {
                return new TicketSeatActionResult.Error(
                    StatusCodes.Status400BadRequest,
                    TicketSeatErrorCodes.CompanionTargetInvalid,
                    "Companion seat must reference an accessible seat in the same event.");
            }
        }

        Guid? categoryId = request.CategoryId is Guid requested && requested != Guid.Empty ? requested : null;
        if (categoryId is Guid pricedCategoryId)
        {
            var priceRow = await _db.TicketEventCategoryPrices
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    row => row.EventId == eventId && row.CategoryId == pricedCategoryId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (priceRow is null)
            {
                return new TicketSeatActionResult.Error(
                    StatusCodes.Status400BadRequest,
                    TicketSeatErrorCodes.CategoryPriceMissing,
                    "This event has no price for the selected category.");
            }

            var used = await _db.TicketSeats.CountAsync(
                    seat => seat.EventId == eventId && seat.CategoryId == pricedCategoryId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (used >= priceRow.Capacity)
            {
                return new TicketSeatActionResult.Error(
                    StatusCodes.Status409Conflict,
                    TicketSeatErrorCodes.CategoryCapacityReached,
                    "Category capacity is reached.");
            }
        }

        var count = await _db.TicketSeats.CountAsync(seat => seat.EventId == eventId, cancellationToken)
            .ConfigureAwait(false);
        if (count >= ticketEvent.Capacity)
        {
            return new TicketSeatActionResult.Error(
                StatusCodes.Status409Conflict,
                TicketSeatErrorCodes.CapacityReached,
                "Event capacity is reached.");
        }

        var now = DateTime.UtcNow;
        var seat = new TicketSeat
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Row = request.Row.Trim(),
            Number = request.Number.Trim(),
            Section = string.IsNullOrWhiteSpace(request.Section) ? string.Empty : request.Section.Trim(),
            Status = TicketSeatStatus.Available,
            CategoryId = categoryId,
            GridColumn = request.GridColumn,
            SeatRole = role,
            CompanionOfSeatId = companionOfSeatId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        _db.TicketSeats.Add(seat);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var labels = await LoadSeatLabelsAsync(eventId, [seat], cancellationToken).ConfigureAwait(false);
        return new TicketSeatActionResult.OkSeat(ToSeatDto(seat, now, labels));
    }

    public async Task<TicketSeatActionResult> ReserveAsync(
        Guid eventId,
        Guid seatId,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var seat = await FindSeatAsync(eventId, seatId, cancellationToken).ConfigureAwait(false);
        if (seat is null)
            return new TicketSeatActionResult.NotFound();
        var now = DateTime.UtcNow;
        if (seat.Status == TicketSeatStatus.Sold)
        {
            return new TicketSeatActionResult.Error(
                StatusCodes.Status409Conflict,
                TicketSeatErrorCodes.SeatSold,
                "Seat is already sold.");
        }

        if (HoldIsActive(seat, now))
        {
            return new TicketSeatActionResult.Error(
                StatusCodes.Status409Conflict,
                TicketSeatErrorCodes.SeatTaken,
                "Seat is reserved.");
        }

        seat.Status = TicketSeatStatus.Reserved;
        seat.ReservedUntilUtc = now.Add(ReservationHold);
        seat.ReservedByUserId = string.IsNullOrWhiteSpace(actorUserId) ? null : actorUserId;
        seat.UpdatedAtUtc = now;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var labels = await LoadSeatLabelsAsync(eventId, [seat], cancellationToken).ConfigureAwait(false);
        return new TicketSeatActionResult.OkSeat(ToSeatDto(seat, now, labels));
    }

    public async Task<TicketSeatActionResult> SellAsync(
        Guid tenantId,
        Guid eventId,
        Guid seatId,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var ticketEvent = await _db.TicketEvents.FirstOrDefaultAsync(item => item.Id == eventId, cancellationToken)
            .ConfigureAwait(false);
        if (ticketEvent is null || ticketEvent.TenantId != tenantId)
            return new TicketSeatActionResult.NotFound();
        var seat = await FindSeatAsync(eventId, seatId, cancellationToken).ConfigureAwait(false);
        if (seat is null)
            return new TicketSeatActionResult.NotFound();

        var now = DateTime.UtcNow;
        if (seat.Status == TicketSeatStatus.Sold)
        {
            return new TicketSeatActionResult.Error(
                StatusCodes.Status409Conflict,
                TicketSeatErrorCodes.SeatSold,
                "Seat is already sold.");
        }

        if (seat.Status == TicketSeatStatus.Reserved && !HoldIsActive(seat, now))
        {
            seat.Status = TicketSeatStatus.Available;
            seat.ReservedUntilUtc = null;
            seat.ReservedByUserId = null;
        }

        if (HoldIsActive(seat, now)
            && !string.Equals(seat.ReservedByUserId, actorUserId, StringComparison.Ordinal))
        {
            return new TicketSeatActionResult.Error(
                StatusCodes.Status409Conflict,
                TicketSeatErrorCodes.SeatTaken,
                "Seat is reserved.");
        }

        if (seat.SeatRole == TicketSeatRole.Companion && seat.CompanionOfSeatId is Guid companionTargetId)
        {
            var targetIsSold = await _db.TicketSeats.AsNoTracking().AnyAsync(
                    row => row.Id == companionTargetId
                        && row.EventId == eventId
                        && row.Status == TicketSeatStatus.Sold,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!targetIsSold)
            {
                return new TicketSeatActionResult.Error(
                    StatusCodes.Status409Conflict,
                    TicketSeatErrorCodes.CompanionTargetNotSold,
                    "Companion seat can be sold only after its accessible seat.");
            }
        }

        var days = TicketCodeHasher.DefaultValidityDays;
        var setting = await _db.TenantSettings.AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.TenantId == tenantId && row.Key == TicketCodeHasher.TenantSettingValidityDaysKey,
                cancellationToken)
            .ConfigureAwait(false);
        if (setting is not null)
            days = TicketCodeHasher.ParseValidityDays(setting.Value);

        string plaintext;
        string hash;
        do
        {
            plaintext = TicketCodeHasher.GeneratePlaintext();
            hash = TicketCodeHasher.HashRaw(plaintext);
        }
        while (await _db.TicketRedemptions.AnyAsync(
                   row => row.TenantId == tenantId && row.TicketCodeHash == hash,
                   cancellationToken)
               .ConfigureAwait(false));

        var display = TicketCodeHasher.DisplayPrefix(hash);
        var validUntil = now.AddDays(days);
        var redemption = new TicketRedemption
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventId = ticketEvent.Id,
            SeatId = seat.Id,
            TicketCode = display,
            TicketCodeHash = hash,
            Status = TicketRedemptionStatus.Valid,
            ValidFromUtc = now,
            ValidUntilUtc = validUntil,
            CreatedAtUtc = now,
        };
        seat.Status = TicketSeatStatus.Sold;
        seat.ReservedUntilUtc = null;
        seat.ReservedByUserId = null;
        seat.UpdatedAtUtc = now;
        _db.TicketRedemptions.Add(redemption);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await WriteAuditAsync(
            AuditEventType.TicketSeatSold,
            "TICKET_SEAT_SOLD",
            "TicketSeat",
            seat.Id,
            tenantId,
            actorUserId,
            "Cashier",
            oldValues: null,
            newValues: new
            {
                tenantId,
                eventId,
                seatId = seat.Id,
                categoryId = seat.CategoryId,
                actorUserId,
            }).ConfigureAwait(false);
        var labels = await LoadSeatLabelsAsync(eventId, [seat], cancellationToken).ConfigureAwait(false);
        decimal? price = seat.CategoryId is Guid soldCategory && labels.Prices.TryGetValue(soldCategory, out var amount)
            ? amount
            : null;
        return new TicketSeatActionResult.OkSale(new SoldTicketDto
        {
            RedemptionId = redemption.Id,
            Code = plaintext,
            DisplayCode = display,
            ValidUntilUtc = validUntil,
            EventName = ticketEvent.Name,
            SeatLabel = TicketSeatLabel.Format(seat),
            CategoryId = seat.CategoryId,
            Price = price,
        });
    }

    private async Task<TicketSeat?> FindSeatAsync(Guid eventId, Guid seatId, CancellationToken cancellationToken)
    {
        var ticketEvent = await _db.TicketEvents.AsNoTracking()
            .AnyAsync(item => item.Id == eventId, cancellationToken)
            .ConfigureAwait(false);
        if (!ticketEvent)
            return null;
        return await _db.TicketSeats
            .FirstOrDefaultAsync(seat => seat.Id == seatId && seat.EventId == eventId, cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool ValidWindow(UpsertTicketEventRequest request) =>
        !string.IsNullOrWhiteSpace(request.Name)
        && !string.IsNullOrWhiteSpace(request.Venue)
        && request.Capacity >= 0
        && request.EndAtUtc >= request.StartAtUtc;

    private static TicketEventDto ToDto(TicketEvent row) => new()
    {
        Id = row.Id,
        Name = row.Name,
        StartAtUtc = row.StartAtUtc,
        EndAtUtc = row.EndAtUtc,
        Venue = row.Venue,
        Capacity = row.Capacity,
        IsActive = row.IsActive,
        StageEdge = row.StageEdge,
    };

    private static TicketSeatDto ToSeatDto(TicketSeat seat, DateTime now, SeatLabelLookup labels)
    {
        var status = seat.Status;
        if (status == TicketSeatStatus.Reserved && !HoldIsActive(seat, now))
            status = TicketSeatStatus.Available;

        labels.Categories.TryGetValue(seat.CategoryId ?? Guid.Empty, out var category);
        decimal? price = seat.CategoryId is Guid categoryId && labels.Prices.TryGetValue(categoryId, out var amount)
            ? amount
            : null;
        return new TicketSeatDto
        {
            Id = seat.Id,
            EventId = seat.EventId,
            Row = seat.Row,
            Number = seat.Number,
            Section = seat.Section,
            Status = status.ToString(),
            ReservedUntilUtc = status == TicketSeatStatus.Reserved ? seat.ReservedUntilUtc : null,
            ReservedByUserId = status == TicketSeatStatus.Reserved ? seat.ReservedByUserId : null,
            CategoryId = seat.CategoryId,
            CategoryCode = category?.Code,
            CategoryName = category?.Name,
            GridColumn = seat.GridColumn,
            SeatRole = seat.SeatRole.ToString(),
            CompanionOfSeatId = seat.CompanionOfSeatId,
            Price = price,
        };
    }

    private static bool HoldIsActive(TicketSeat seat, DateTime now) =>
        seat.Status == TicketSeatStatus.Reserved
        && seat.ReservedUntilUtc is DateTime until
        && until > now;

    private static bool TryResolveStageEdge(string? raw, bool keepWhenMissing, string current, out string edge)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            edge = keepWhenMissing ? current : "top";
            return true;
        }

        var normalized = raw.Trim().ToLowerInvariant();
        if (normalized is "top" or "bottom")
        {
            edge = normalized;
            return true;
        }

        edge = current;
        return false;
    }

    private async Task<SeatLabelLookup> LoadSeatLabelsAsync(
        Guid eventId,
        IReadOnlyList<TicketSeat> seats,
        CancellationToken cancellationToken)
    {
        var categoryIds = seats
            .Where(seat => seat.CategoryId is Guid)
            .Select(seat => seat.CategoryId!.Value)
            .Distinct()
            .ToList();
        var categories = categoryIds.Count == 0
            ? new Dictionary<Guid, TicketPriceCategory>()
            : await _db.TicketPriceCategories.AsNoTracking()
                .Where(row => categoryIds.Contains(row.Id))
                .ToDictionaryAsync(row => row.Id, cancellationToken)
                .ConfigureAwait(false);
        var prices = await _db.TicketEventCategoryPrices.AsNoTracking()
            .Where(row => row.EventId == eventId)
            .ToDictionaryAsync(row => row.CategoryId, row => row.Price, cancellationToken)
            .ConfigureAwait(false);
        return new SeatLabelLookup(categories, prices);
    }

    private sealed record SeatLabelLookup(
        IReadOnlyDictionary<Guid, TicketPriceCategory> Categories,
        IReadOnlyDictionary<Guid, decimal> Prices);

    public async Task<IReadOnlyList<TicketPriceCategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.TicketPriceCategories.AsNoTracking()
            .OrderBy(row => row.SortOrder)
            .ThenBy(row => row.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToCategoryDto).ToList();
    }

    public async Task<TicketPriceCategoryDto?> GetCategoryAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var row = await _db.TicketPriceCategories.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == categoryId, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : ToCategoryDto(row);
    }

    public async Task<TicketCatalogResult> CreateCategoryAsync(
        Guid tenantId,
        UpsertTicketPriceCategoryRequest request,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeCategory(request, out var code, out var name, out var error))
            return error;
        if (await CategoryCodeTakenAsync(code, exceptId: null, cancellationToken).ConfigureAwait(false))
        {
            return new TicketCatalogResult.Error(
                StatusCodes.Status409Conflict,
                TicketSeatErrorCodes.DuplicateCategoryCode,
                "Category code already exists.");
        }

        var now = DateTime.UtcNow;
        var row = new TicketPriceCategory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = name,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        _db.TicketPriceCategories.Add(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await WriteCategoryAuditAsync(tenantId, actorUserId, row.Id, oldValues: null, new
        {
            tenantId,
            categoryId = row.Id,
            code = row.Code,
            name = row.Name,
            sortOrder = row.SortOrder,
            isActive = row.IsActive,
        }).ConfigureAwait(false);
        return new TicketCatalogResult.CategoryOk(ToCategoryDto(row));
    }

    public async Task<TicketCatalogResult> UpdateCategoryAsync(
        Guid categoryId,
        UpsertTicketPriceCategoryRequest request,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeCategory(request, out var code, out var name, out var error))
            return error;
        var row = await _db.TicketPriceCategories.FirstOrDefaultAsync(item => item.Id == categoryId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return new TicketCatalogResult.NotFound();
        if (await CategoryCodeTakenAsync(code, row.Id, cancellationToken).ConfigureAwait(false))
        {
            return new TicketCatalogResult.Error(
                StatusCodes.Status409Conflict,
                TicketSeatErrorCodes.DuplicateCategoryCode,
                "Category code already exists.");
        }

        var oldValues = new
        {
            tenantId = row.TenantId,
            categoryId = row.Id,
            code = row.Code,
            name = row.Name,
            sortOrder = row.SortOrder,
            isActive = row.IsActive,
        };
        row.Code = code;
        row.Name = name;
        row.SortOrder = request.SortOrder;
        row.IsActive = request.IsActive;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await WriteCategoryAuditAsync(row.TenantId, actorUserId, row.Id, oldValues, new
        {
            tenantId = row.TenantId,
            categoryId = row.Id,
            code = row.Code,
            name = row.Name,
            sortOrder = row.SortOrder,
            isActive = row.IsActive,
        }).ConfigureAwait(false);
        return new TicketCatalogResult.CategoryOk(ToCategoryDto(row));
    }

    public async Task<TicketCatalogResult> DeactivateCategoryAsync(
        Guid categoryId,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var row = await _db.TicketPriceCategories.FirstOrDefaultAsync(item => item.Id == categoryId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return new TicketCatalogResult.NotFound();
        var oldActive = row.IsActive;
        row.IsActive = false;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await WriteCategoryAuditAsync(row.TenantId, actorUserId, row.Id,
            new { tenantId = row.TenantId, categoryId = row.Id, isActive = oldActive },
            new { tenantId = row.TenantId, categoryId = row.Id, isActive = false }).ConfigureAwait(false);
        return new TicketCatalogResult.CategoryOk(ToCategoryDto(row));
    }

    public async Task<IReadOnlyList<TicketEventCategoryPriceDto>?> ListCategoryPricesAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var exists = await _db.TicketEvents.AsNoTracking().AnyAsync(item => item.Id == eventId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            return null;
        var rows = await _db.TicketEventCategoryPrices.AsNoTracking()
            .Where(row => row.EventId == eventId)
            .OrderBy(row => row.CategoryId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var categoryIds = rows.Select(price => price.CategoryId).ToList();
        var categories = categoryIds.Count == 0
            ? new Dictionary<Guid, TicketPriceCategory>()
            : await _db.TicketPriceCategories.AsNoTracking()
                .Where(row => categoryIds.Contains(row.Id))
                .ToDictionaryAsync(row => row.Id, cancellationToken)
                .ConfigureAwait(false);
        return rows.Select(row => ToPriceDto(row, categories.GetValueOrDefault(row.CategoryId))).ToList();
    }

    public async Task<TicketCatalogResult> SetCategoryPriceAsync(
        Guid eventId,
        SetTicketEventCategoryPriceRequest request,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        if (request.Price < 0 || request.Price != decimal.Round(request.Price, 2, MidpointRounding.AwayFromZero))
        {
            return new TicketCatalogResult.Error(
                StatusCodes.Status400BadRequest,
                TicketSeatErrorCodes.InvalidCategory,
                "Price must be zero or greater with at most two decimal places.");
        }

        if (request.Capacity < 0)
        {
            return new TicketCatalogResult.Error(
                StatusCodes.Status400BadRequest,
                TicketSeatErrorCodes.InvalidCategory,
                "Capacity must be zero or greater.");
        }

        var ticketEvent = await _db.TicketEvents.FirstOrDefaultAsync(item => item.Id == eventId, cancellationToken)
            .ConfigureAwait(false);
        if (ticketEvent is null)
            return new TicketCatalogResult.NotFound();
        var category = await _db.TicketPriceCategories.FirstOrDefaultAsync(item => item.Id == request.CategoryId, cancellationToken)
            .ConfigureAwait(false);
        if (category is null)
        {
            return new TicketCatalogResult.Error(
                StatusCodes.Status400BadRequest,
                TicketSeatErrorCodes.InvalidCategory,
                "Category was not found.");
        }

        var row = await _db.TicketEventCategoryPrices
            .FirstOrDefaultAsync(item => item.EventId == eventId && item.CategoryId == category.Id, cancellationToken)
            .ConfigureAwait(false);
        decimal? oldPrice = row?.Price;
        int? oldCapacity = row?.Capacity;
        var now = DateTime.UtcNow;
        if (row is null)
        {
            row = new TicketEventCategoryPrice
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                CategoryId = category.Id,
                Price = request.Price,
                Capacity = request.Capacity,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            _db.TicketEventCategoryPrices.Add(row);
        }
        else
        {
            row.Price = request.Price;
            row.Capacity = request.Capacity;
            row.UpdatedAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await WriteAuditAsync(
            AuditEventType.TicketPriceCategoryChanged,
            "TICKET_PRICE_CATEGORY_CHANGED",
            "TicketEventCategoryPrice",
            row.Id,
            ticketEvent.TenantId,
            actorUserId,
            "Manager",
            oldValues: new
            {
                tenantId = ticketEvent.TenantId,
                eventId,
                categoryId = category.Id,
                oldPrice,
                oldCapacity,
            },
            newValues: new
            {
                tenantId = ticketEvent.TenantId,
                eventId,
                categoryId = category.Id,
                oldPrice,
                newPrice = request.Price,
                oldCapacity,
                newCapacity = request.Capacity,
            }).ConfigureAwait(false);
        return new TicketCatalogResult.PriceOk(ToPriceDto(row, category));
    }

    private static TicketPriceCategoryDto ToCategoryDto(TicketPriceCategory row) => new()
    {
        Id = row.Id,
        Code = row.Code,
        Name = row.Name,
        SortOrder = row.SortOrder,
        IsActive = row.IsActive,
    };

    private static TicketEventCategoryPriceDto ToPriceDto(TicketEventCategoryPrice row, TicketPriceCategory? category) => new()
    {
        Id = row.Id,
        EventId = row.EventId,
        CategoryId = row.CategoryId,
        CategoryCode = category?.Code,
        CategoryName = category?.Name,
        Price = row.Price,
        Capacity = row.Capacity,
    };

    private static bool TryNormalizeCategory(
        UpsertTicketPriceCategoryRequest request,
        out string code,
        out string name,
        out TicketCatalogResult.Error error)
    {
        code = (request.Code ?? string.Empty).Trim().ToLowerInvariant();
        name = (request.Name ?? string.Empty).Trim();
        var codeOk = code.Length is >= 1 and <= 32 && code.All(ch => ch is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '-');
        if (!codeOk || name.Length is < 1 or > 64)
        {
            error = new TicketCatalogResult.Error(
                StatusCodes.Status400BadRequest,
                TicketSeatErrorCodes.InvalidCategory,
                "Category code or name is invalid.");
            return false;
        }

        error = null!;
        return true;
    }

    private async Task<bool> CategoryCodeTakenAsync(string code, Guid? exceptId, CancellationToken cancellationToken) =>
        await _db.TicketPriceCategories.AnyAsync(
            row => row.Code == code && (exceptId == null || row.Id != exceptId),
            cancellationToken).ConfigureAwait(false);

    private Task WriteCategoryAuditAsync(Guid tenantId, string actorUserId, Guid categoryId, object? oldValues, object newValues) =>
        WriteAuditAsync(
            AuditEventType.TicketPriceCategoryChanged,
            "TICKET_PRICE_CATEGORY_CHANGED",
            "TicketPriceCategory",
            categoryId,
            tenantId,
            actorUserId,
            "Manager",
            oldValues,
            newValues);

    private async Task WriteAuditAsync(
        AuditEventType actionType,
        string action,
        string entityType,
        Guid? entityId,
        Guid tenantId,
        string actorUserId,
        string actorRole,
        object? oldValues,
        object newValues)
    {
        try
        {
            await _audit.LogSystemOperationAsync(
                action: action,
                entityType: entityType,
                userId: string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId,
                userRole: actorRole,
                description: action,
                actionType: actionType,
                entityId: entityId,
                tenantId: tenantId,
                oldValues: oldValues,
                newValues: newValues).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ticket audit write failed for {Action} {EntityId}", action, entityId);
        }
    }
}
