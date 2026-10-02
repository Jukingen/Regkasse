using System.Security.Cryptography;
using System.Text;

namespace KasseAPI_Final.Services.Tickets;

public static class TicketCodeHasher
{
    public const int DisplayPrefixLength = 12;
    public const string TenantSettingValidityDaysKey = "ticketDefaultValidityDays";
    public const int DefaultValidityDays = 365;

    public static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    public static string HashNormalized(string normalizedCode)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedCode));
        return Convert.ToHexString(bytes);
    }

    public static string HashRaw(string? code) => HashNormalized(Normalize(code));

    public static string DisplayPrefix(string hash)
    {
        if (string.IsNullOrEmpty(hash))
            return string.Empty;
        return hash.Length <= DisplayPrefixLength
            ? hash
            : hash[..DisplayPrefixLength];
    }

    public static string GeneratePlaintext()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<byte> raw = stackalloc byte[12];
        RandomNumberGenerator.Fill(raw);
        var chars = new char[12];
        for (var i = 0; i < raw.Length; i++)
            chars[i] = alphabet[raw[i] % alphabet.Length];
        return "TKT-" + new string(chars);
    }

    public static int ParseValidityDays(string? raw)
    {
        if (int.TryParse(raw, out var days) && days > 0 && days <= 3650)
            return days;
        return DefaultValidityDays;
    }
}

public static class TicketErrorCodes
{
    public const string AlreadyRedeemed = "TICKET_ALREADY_REDEEMED";
    public const string Expired = "TICKET_EXPIRED";
    public const string Invalid = "TICKET_INVALID";
    public const string Cancelled = "TICKET_CANCELLED";
}

public sealed class IssuedTicketDto
{
    public string Code { get; init; } = string.Empty;
    public string DisplayCode { get; init; } = string.Empty;
    public DateTime? ValidUntilUtc { get; init; }
}

public sealed class TicketValidationDto
{
    public string DisplayCode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime? ValidFromUtc { get; init; }
    public DateTime? ValidUntilUtc { get; init; }
    public bool IsValid { get; init; }
    public bool CanRedeem { get; init; }
}

public sealed class TicketRedemptionListRowDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string DisplayCode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime? ValidUntilUtc { get; init; }
    public DateTime? RedeemedAtUtc { get; init; }
    public string? RedeemedByUserId { get; init; }
}

public sealed class TicketRedemptionListResponse
{
    public IReadOnlyList<TicketRedemptionListRowDto> Items { get; init; } = [];
}

public sealed class TicketErrorDto
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
