using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

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
    public const string InvalidEvent = "EVENT_INVALID";
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
}

public sealed class SeatActionRequest
{
    public Guid SeatId { get; init; }
}

public sealed class SoldTicketDto
{
    public Guid RedemptionId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string DisplayCode { get; init; } = string.Empty;
    public DateTime? ValidUntilUtc { get; init; }
    public string EventName { get; init; } = string.Empty;
    public string SeatLabel { get; init; } = string.Empty;
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
    Task<TicketEventDto?> UpdateAsync(Guid eventId, UpsertTicketEventRequest request, CancellationToken cancellationToken);
    Task<bool> DeactivateAsync(Guid eventId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TicketSeatDto>?> ListSeatsAsync(Guid eventId, CancellationToken cancellationToken);
    Task<TicketSeatActionResult> AddSeatAsync(Guid eventId, CreateTicketSeatRequest request, CancellationToken cancellationToken);
    Task<TicketSeatActionResult> ReserveAsync(Guid eventId, Guid seatId, CancellationToken cancellationToken);
    Task<TicketSeatActionResult> SellAsync(Guid tenantId, Guid eventId, Guid seatId, CancellationToken cancellationToken);
}

public sealed class TicketEventService : ITicketEventService
{
    public static readonly TimeSpan ReservationHold = TimeSpan.FromMinutes(15);

    private readonly AppDbContext _db;

    public TicketEventService(AppDbContext db)
    {
        _db = db;
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
        if (!ValidWindow(request))
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
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        _db.TicketEvents.Add(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(row);
    }

    public async Task<TicketEventDto?> UpdateAsync(
        Guid eventId,
        UpsertTicketEventRequest request,
        CancellationToken cancellationToken)
    {
        if (!ValidWindow(request))
            return null;
        var row = await _db.TicketEvents.FirstOrDefaultAsync(item => item.Id == eventId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return null;
        row.Name = request.Name.Trim();
        row.StartAtUtc = request.StartAtUtc;
        row.EndAtUtc = request.EndAtUtc;
        row.Venue = request.Venue.Trim();
        row.Capacity = request.Capacity;
        row.IsActive = request.IsActive;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(row);
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
            .ThenBy(seat => seat.Number)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var now = DateTime.UtcNow;
        return seats.Select(seat => ToSeatDto(seat, now)).ToList();
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
        var count = await _db.TicketSeats.CountAsync(seat => seat.EventId == eventId, cancellationToken)
            .ConfigureAwait(false);
        if (count >= ticketEvent.Capacity)
        {
            return new TicketSeatActionResult.Error(
                StatusCodes.Status409Conflict,
                TicketSeatErrorCodes.CapacityReached,
                "Event capacity is reached.");
        }

        var seat = new TicketSeat
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Row = request.Row.Trim(),
            Number = request.Number.Trim(),
            Section = string.IsNullOrWhiteSpace(request.Section) ? string.Empty : request.Section.Trim(),
            Status = TicketSeatStatus.Available,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.TicketSeats.Add(seat);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new TicketSeatActionResult.OkSeat(ToSeatDto(seat, DateTime.UtcNow));
    }

    public async Task<TicketSeatActionResult> ReserveAsync(
        Guid eventId,
        Guid seatId,
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

        if (seat.Status == TicketSeatStatus.Reserved
            && seat.ReservedUntilUtc is DateTime until
            && until > now)
        {
            return new TicketSeatActionResult.Error(
                StatusCodes.Status409Conflict,
                TicketSeatErrorCodes.SeatTaken,
                "Seat is reserved.");
        }

        seat.Status = TicketSeatStatus.Reserved;
        seat.ReservedUntilUtc = now.Add(ReservationHold);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new TicketSeatActionResult.OkSeat(ToSeatDto(seat, now));
    }

    public async Task<TicketSeatActionResult> SellAsync(
        Guid tenantId,
        Guid eventId,
        Guid seatId,
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

        if (seat.Status == TicketSeatStatus.Reserved
            && seat.ReservedUntilUtc is DateTime until
            && until <= now)
        {
            seat.Status = TicketSeatStatus.Available;
            seat.ReservedUntilUtc = null;
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
        _db.TicketRedemptions.Add(redemption);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new TicketSeatActionResult.OkSale(new SoldTicketDto
        {
            RedemptionId = redemption.Id,
            Code = plaintext,
            DisplayCode = display,
            ValidUntilUtc = validUntil,
            EventName = ticketEvent.Name,
            SeatLabel = TicketSeatLabel.Format(seat),
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
    };

    private static TicketSeatDto ToSeatDto(TicketSeat seat, DateTime now)
    {
        var status = seat.Status;
        if (status == TicketSeatStatus.Reserved
            && seat.ReservedUntilUtc is DateTime until
            && until <= now)
        {
            status = TicketSeatStatus.Available;
        }

        return new TicketSeatDto
        {
            Id = seat.Id,
            EventId = seat.EventId,
            Row = seat.Row,
            Number = seat.Number,
            Section = seat.Section,
            Status = status.ToString(),
            ReservedUntilUtc = status == TicketSeatStatus.Reserved ? seat.ReservedUntilUtc : null,
        };
    }
}
