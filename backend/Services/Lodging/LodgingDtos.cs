using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Services.Lodging;

public sealed class RoomDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public int Capacity { get; init; }
    public string Status { get; init; } = "Available";
    public bool IsActive { get; init; }
    public bool Occupied { get; init; }
}

public sealed class UpdateRoomStatusRequest
{
    [Required]
    [MaxLength(32)]
    public string Status { get; set; } = string.Empty;
}

public sealed class CreateRoomRequest
{
    [Required]
    [MaxLength(32)]
    public string Number { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string Type { get; set; } = string.Empty;

    [Range(1, 20)]
    public int Capacity { get; set; } = 1;
}

public sealed class GuestFolioDto
{
    public Guid Id { get; init; }
    public Guid CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public Guid RoomId { get; init; }
    public string? RoomNumber { get; init; }
    public DateTime CheckIn { get; init; }
    public DateTime? CheckOut { get; init; }
    public string Status { get; init; } = "Open";
    public decimal Balance { get; init; }
    public string? Notes { get; init; }
    public bool IsOpen { get; init; }
}

public sealed class UpdateGuestFolioRequest
{
    public DateTime? CheckOut { get; set; }

    public string? Notes { get; set; }

    [MaxLength(32)]
    public string? Status { get; set; }
}

public sealed class ChargeFolioRequest
{
    [Required]
    [MaxLength(255)]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "99999999")]
    public decimal Amount { get; set; }
}

public sealed class GuestFolioItemDto
{
    public Guid Id { get; init; }
    public Guid FolioId { get; init; }
    public Guid? PaymentDetailId { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class CreateGuestFolioRequest
{
    [Required]
    public Guid CustomerId { get; set; }

    [Required]
    public Guid RoomId { get; set; }

    public DateTime? CheckIn { get; set; }

    public DateTime? CheckOut { get; set; }

    public decimal Balance { get; set; }
}

public static class LodgingErrorCodes
{
    public const string RoomNumberDuplicate = "ROOM_NUMBER_DUPLICATE";
    public const string RoomOccupied = "ROOM_OCCUPIED";
    public const string InvalidStay = "INVALID_STAY";
    public const string FolioClosed = "FOLIO_CLOSED";
    public const string InvalidStatus = "INVALID_STATUS";
}

public sealed class LodgingErrorDto
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
