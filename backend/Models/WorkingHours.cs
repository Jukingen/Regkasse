using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace KasseAPI_Final.Models;

/// <summary>Open/close times for a single weekday (local restaurant clock, HH:mm).</summary>
public sealed class WorkingHoursDay
{
    /// <summary>Local open time as <c>HH:mm</c> (24h). Ignored when <see cref="IsClosed"/>.</summary>
    [MaxLength(5)]
    public string OpenTime { get; set; } = "09:00";

    /// <summary>Local close time as <c>HH:mm</c> (24h). When earlier than open, closing is next calendar day.</summary>
    [MaxLength(5)]
    public string CloseTime { get; set; } = "22:00";

    /// <summary>When true, the restaurant is closed this weekday (no hours-based Tagesabschluss reminder).</summary>
    public bool IsClosed { get; set; }
}

/// <summary>
/// One-off override for a calendar date (holidays, early close). Stored inside
/// <see cref="WorkingHoursSettings.SpecialDays"/> JSON on company settings.
/// </summary>
public sealed class WorkingHoursSpecialDay
{
    /// <summary>Local calendar date as <c>yyyy-MM-dd</c>.</summary>
    [Required]
    [MaxLength(10)]
    public string Date { get; set; } = string.Empty;

    /// <summary>When true, the restaurant is closed for the whole day.</summary>
    public bool IsClosed { get; set; }

    /// <summary>Optional open time (<c>HH:mm</c>) when not fully closed.</summary>
    [MaxLength(5)]
    public string? OpenTime { get; set; }

    /// <summary>Optional close time (<c>HH:mm</c>) when not fully closed.</summary>
    [MaxLength(5)]
    public string? CloseTime { get; set; }
}

/// <summary>
/// Per-tenant restaurant working hours stored as JSON on <see cref="CompanySettings"/>.
/// Used by POS smart Tagesabschluss reminders, public closed-day messaging,
/// and online-order intake cutoffs.
/// </summary>
/// <remarks>
/// Kept as a JSON value object (not a separate table) so tenant company settings remain
/// the single source of truth. Day schedules use typed <see cref="WorkingHoursDay"/> objects
/// rather than raw JSON strings.
/// </remarks>
public sealed class WorkingHoursSettings
{
    public const int DefaultReminderHoursBeforeClosing = 1;
    public const int MinReminderHoursBeforeClosing = 0;
    public const int MaxReminderHoursBeforeClosing = 12;

    public const int DefaultStopOnlineOrdersMinutesBeforeClose = 30;
    public const int MinStopOnlineOrdersMinutesBeforeClose = 0;
    public const int MaxStopOnlineOrdersMinutesBeforeClose = 180;

    public const string DefaultClosedDayMessage = "Heute geschlossen";
    public const int MaxClosedDayMessageLength = 200;
    public const int MaxSpecialDays = 366;

    /// <summary>Show POS Tagesabschluss reminder this many hours before today's closing time.</summary>
    [Range(MinReminderHoursBeforeClosing, MaxReminderHoursBeforeClosing)]
    public int ReminderHoursBeforeClosing { get; set; } = DefaultReminderHoursBeforeClosing;

    /// <summary>
    /// Reject new online orders this many minutes before today's closing time
    /// (and all day when closed / special closed day).
    /// </summary>
    [Range(MinStopOnlineOrdersMinutesBeforeClose, MaxStopOnlineOrdersMinutesBeforeClose)]
    public int StopOnlineOrdersMinutesBeforeClose { get; set; } = DefaultStopOnlineOrdersMinutesBeforeClose;

    /// <summary>
    /// FA preference: surface a strong POS closing prompt at closing time.
    /// Does <strong>not</strong> authorize automatic fiscal Tagesabschluss / RKSV close,
    /// and must never block POS orders or payments — cashiers always work.
    /// </summary>
    public bool AutoClosePOSAtClosing { get; set; }

    /// <summary>Customer-facing message when the restaurant is closed today.</summary>
    [MaxLength(MaxClosedDayMessageLength)]
    public string ClosedDayMessage { get; set; } = DefaultClosedDayMessage;

    /// <summary>Holiday / one-off date overrides (sorted by date after <see cref="Normalize"/>).</summary>
    public List<WorkingHoursSpecialDay> SpecialDays { get; set; } = new();

    public WorkingHoursDay? Monday { get; set; } = CreateDefaultOpenDay();
    public WorkingHoursDay? Tuesday { get; set; } = CreateDefaultOpenDay();
    public WorkingHoursDay? Wednesday { get; set; } = CreateDefaultOpenDay();
    public WorkingHoursDay? Thursday { get; set; } = CreateDefaultOpenDay();
    public WorkingHoursDay? Friday { get; set; } = CreateDefaultOpenDay();
    public WorkingHoursDay? Saturday { get; set; } = CreateDefaultOpenDay();
    public WorkingHoursDay? Sunday { get; set; } = CreateDefaultOpenDay();

    public static WorkingHoursSettings CreateDefault() => new();

    public static WorkingHoursDay CreateDefaultOpenDay() => new()
    {
        OpenTime = "09:00",
        CloseTime = "22:00",
        IsClosed = false,
    };

    /// <summary>Resolve the weekday config for a <see cref="DayOfWeek"/> (ignores special days).</summary>
    public WorkingHoursDay GetDay(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Monday => EnsureDay(Monday),
        DayOfWeek.Tuesday => EnsureDay(Tuesday),
        DayOfWeek.Wednesday => EnsureDay(Wednesday),
        DayOfWeek.Thursday => EnsureDay(Thursday),
        DayOfWeek.Friday => EnsureDay(Friday),
        DayOfWeek.Saturday => EnsureDay(Saturday),
        DayOfWeek.Sunday => EnsureDay(Sunday),
        _ => EnsureDay(Monday),
    };

    /// <summary>
    /// Resolve effective hours for a local calendar date, applying special-day overrides first.
    /// </summary>
    public WorkingHoursDay ResolveDayForDate(DateOnly localDate)
    {
        var key = localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var special = SpecialDays?
            .FirstOrDefault(s => string.Equals(s.Date, key, StringComparison.Ordinal));
        if (special is not null)
        {
            if (special.IsClosed)
            {
                return new WorkingHoursDay
                {
                    IsClosed = true,
                    OpenTime = "00:00",
                    CloseTime = "00:00",
                };
            }

            return new WorkingHoursDay
            {
                IsClosed = false,
                OpenTime = NormalizeTimeOrDefault(special.OpenTime, "09:00"),
                CloseTime = NormalizeTimeOrDefault(special.CloseTime, "22:00"),
            };
        }

        return CloneDay(GetDay(localDate.DayOfWeek));
    }

    /// <summary>
    /// Whether <strong>online</strong> (Web/App) order intake should be accepted at
    /// <paramref name="utcNow"/> in the given restaurant time zone (defaults to Europe/Vienna).
    /// Requires the restaurant open window and the stop-before-close cutoff.
    /// Never apply this gate to POS cart / payment flows — cashiers always work.
    /// </summary>
    public bool IsAcceptingOnlineOrders(DateTimeOffset utcNow, string? timeZoneId = null) =>
        EvaluateWebsiteStatus(utcNow, timeZoneId).CanOrder;

    /// <summary>
    /// Whether the restaurant is within today's local open window (display / isOpen).
    /// Ignores online-order stop-before-close. Never use to block POS.
    /// </summary>
    public bool IsRestaurantOpen(DateTimeOffset utcNow, string? timeZoneId = null) =>
        EvaluateWebsiteStatus(utcNow, timeZoneId).IsOpen;

    /// <summary>True when the restaurant is closed for the local calendar date of <paramref name="utcNow"/>.</summary>
    public bool IsClosedOn(DateTimeOffset utcNow, string? timeZoneId = null)
    {
        var tz = ResolveTimeZone(timeZoneId);
        var local = TimeZoneInfo.ConvertTime(utcNow, tz);
        var localDate = DateOnly.FromDateTime(local.DateTime);
        return ResolveDayForDate(localDate).IsClosed;
    }

    /// <summary>
    /// Resolve special-day override for a local calendar date, if any
    /// (from <see cref="SpecialDays"/> JSON — not a separate table).
    /// </summary>
    public WorkingHoursSpecialDay? FindSpecialDay(DateOnly localDate)
    {
        var key = localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return SpecialDays?
            .FirstOrDefault(s => string.Equals(s.Date, key, StringComparison.Ordinal));
    }

    /// <summary>
    /// Customer-facing website/app status (open window + online-order intake).
    /// POS and FA must never be gated by this result.
    /// </summary>
    public WorkingHoursWebsiteStatus EvaluateWebsiteStatus(
        DateTimeOffset utcNow,
        string? timeZoneId = null)
    {
        var tz = ResolveTimeZone(timeZoneId);
        var local = TimeZoneInfo.ConvertTime(utcNow, tz);
        var localDate = DateOnly.FromDateTime(local.DateTime);
        var special = FindSpecialDay(localDate);
        var isSpecial = special is not null;
        var day = ResolveDayForDate(localDate);
        var closedMessage = string.IsNullOrWhiteSpace(ClosedDayMessage)
            ? DefaultClosedDayMessage
            : ClosedDayMessage.Trim();

        if (day.IsClosed)
        {
            return new WorkingHoursWebsiteStatus
            {
                IsOpen = false,
                CanOrder = false,
                Message = closedMessage,
                OpenTime = null,
                CloseTime = null,
                IsSpecial = isSpecial,
            };
        }

        if (!TryComputeOpenCloseInstants(localDate, day, tz, out var openAt, out var closeAt))
        {
            return new WorkingHoursWebsiteStatus
            {
                IsOpen = false,
                CanOrder = false,
                Message = closedMessage,
                OpenTime = null,
                CloseTime = null,
                IsSpecial = isSpecial,
            };
        }

        var isOpen = utcNow >= openAt && utcNow < closeAt;
        var cutoff = closeAt.AddMinutes(-StopOnlineOrdersMinutesBeforeClose);
        // Intake only while open and before the configured stop-before-close window.
        var canOrder = isOpen && utcNow < cutoff;

        string message;
        if (canOrder)
            message = isSpecial
                ? "Sondertag — Online-Bestellung möglich"
                : "Online-Bestellung möglich";
        else if (isOpen)
            message = "Online-Bestellungen vor Schließung gestoppt";
        else if (utcNow < openAt)
            message = $"Öffnet um {day.OpenTime}";
        else
            message = closedMessage;

        return new WorkingHoursWebsiteStatus
        {
            IsOpen = isOpen,
            CanOrder = canOrder,
            Message = message,
            OpenTime = day.OpenTime,
            CloseTime = day.CloseTime,
            IsSpecial = isSpecial,
        };
    }

    /// <summary>
    /// Today's special-day snapshot for website display (from JSON special days).
    /// </summary>
    public WebsiteSpecialDayInfo EvaluateSpecialDay(
        DateTimeOffset utcNow,
        string? timeZoneId = null)
    {
        var tz = ResolveTimeZone(timeZoneId);
        var local = TimeZoneInfo.ConvertTime(utcNow, tz);
        var localDate = DateOnly.FromDateTime(local.DateTime);
        var special = FindSpecialDay(localDate);
        if (special is null)
        {
            return new WebsiteSpecialDayInfo
            {
                IsSpecial = false,
                Date = localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            };
        }

        var closedMessage = string.IsNullOrWhiteSpace(ClosedDayMessage)
            ? DefaultClosedDayMessage
            : ClosedDayMessage.Trim();

        if (special.IsClosed)
        {
            return new WebsiteSpecialDayInfo
            {
                IsSpecial = true,
                IsClosed = true,
                Message = closedMessage,
                Date = special.Date,
                OpenTime = null,
                CloseTime = null,
            };
        }

        return new WebsiteSpecialDayInfo
        {
            IsSpecial = true,
            IsClosed = false,
            Message = "Sondertag — geänderte Öffnungszeiten",
            Date = special.Date,
            OpenTime = NormalizeTimeOrDefault(special.OpenTime, "09:00"),
            CloseTime = NormalizeTimeOrDefault(special.CloseTime, "22:00"),
        };
    }

    /// <summary>Clamp reminder / cutoff ranges, fill missing days, and sanitize special days.</summary>
    public void Normalize()
    {
        ReminderHoursBeforeClosing = Math.Clamp(
            ReminderHoursBeforeClosing,
            MinReminderHoursBeforeClosing,
            MaxReminderHoursBeforeClosing);
        StopOnlineOrdersMinutesBeforeClose = Math.Clamp(
            StopOnlineOrdersMinutesBeforeClose,
            MinStopOnlineOrdersMinutesBeforeClose,
            MaxStopOnlineOrdersMinutesBeforeClose);

        if (string.IsNullOrWhiteSpace(ClosedDayMessage))
            ClosedDayMessage = DefaultClosedDayMessage;
        else
        {
            ClosedDayMessage = ClosedDayMessage.Trim();
            if (ClosedDayMessage.Length > MaxClosedDayMessageLength)
                ClosedDayMessage = ClosedDayMessage[..MaxClosedDayMessageLength];
        }

        Monday = EnsureDay(Monday);
        Tuesday = EnsureDay(Tuesday);
        Wednesday = EnsureDay(Wednesday);
        Thursday = EnsureDay(Thursday);
        Friday = EnsureDay(Friday);
        Saturday = EnsureDay(Saturday);
        Sunday = EnsureDay(Sunday);
        NormalizeDay(Monday);
        NormalizeDay(Tuesday);
        NormalizeDay(Wednesday);
        NormalizeDay(Thursday);
        NormalizeDay(Friday);
        NormalizeDay(Saturday);
        NormalizeDay(Sunday);

        SpecialDays = NormalizeSpecialDays(SpecialDays);
    }

    private static List<WorkingHoursSpecialDay> NormalizeSpecialDays(List<WorkingHoursSpecialDay>? source)
    {
        if (source is null || source.Count == 0)
            return new List<WorkingHoursSpecialDay>();

        var normalized = new List<WorkingHoursSpecialDay>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in source
                     .Where(s => s is not null)
                     .OrderBy(s => s.Date, StringComparer.Ordinal))
        {
            if (!DateOnly.TryParseExact(
                    item.Date?.Trim(),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var date))
            {
                continue;
            }

            var key = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (!seen.Add(key))
                continue;

            var entry = new WorkingHoursSpecialDay
            {
                Date = key,
                IsClosed = item.IsClosed,
                OpenTime = item.IsClosed ? null : NormalizeTimeOrDefault(item.OpenTime, "09:00"),
                CloseTime = item.IsClosed ? null : NormalizeTimeOrDefault(item.CloseTime, "22:00"),
            };
            normalized.Add(entry);
            if (normalized.Count >= MaxSpecialDays)
                break;
        }

        return normalized;
    }

    private static WorkingHoursDay EnsureDay(WorkingHoursDay? day) =>
        day ?? CreateDefaultOpenDay();

    private static WorkingHoursDay CloneDay(WorkingHoursDay day) => new()
    {
        OpenTime = day.OpenTime,
        CloseTime = day.CloseTime,
        IsClosed = day.IsClosed,
    };

    private static void NormalizeDay(WorkingHoursDay day)
    {
        day.OpenTime = NormalizeTimeOrDefault(day.OpenTime, "09:00");
        day.CloseTime = NormalizeTimeOrDefault(day.CloseTime, "22:00");
    }

    private static string NormalizeTimeOrDefault(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;
        var trimmed = value.Trim();
        if (TimeSpan.TryParseExact(trimmed, @"hh\:mm", null, out _)
            || TimeSpan.TryParseExact(trimmed, @"h\:mm", null, out _))
            return trimmed.Length == 4 ? "0" + trimmed : trimmed;
        return fallback;
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        var id = string.IsNullOrWhiteSpace(timeZoneId) ? "Europe/Vienna" : timeZoneId.Trim();
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
        }
    }

    private static bool TryComputeOpenCloseInstants(
        DateOnly localDate,
        WorkingHoursDay day,
        TimeZoneInfo tz,
        out DateTimeOffset openAt,
        out DateTimeOffset closeAt)
    {
        openAt = default;
        closeAt = default;
        if (day.IsClosed)
            return false;

        if (!TryParseHhMm(day.OpenTime, out var open)
            || !TryParseHhMm(day.CloseTime, out var close))
            return false;

        var openLocal = localDate.ToDateTime(open, DateTimeKind.Unspecified);
        var closeLocal = localDate.ToDateTime(close, DateTimeKind.Unspecified);
        if (closeLocal <= openLocal)
            closeLocal = closeLocal.AddDays(1);

        openAt = new DateTimeOffset(openLocal, tz.GetUtcOffset(openLocal));
        closeAt = new DateTimeOffset(closeLocal, tz.GetUtcOffset(closeLocal));
        return true;
    }

    private static bool TryParseHhMm(string? value, out TimeOnly time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        return TimeOnly.TryParseExact(
            value.Trim(),
            new[] { "HH:mm", "H:mm" },
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out time);
    }
}

/// <summary>
/// Snapshot for tenant website / mobile-app online-order UI.
/// Never apply to POS sales or FA management APIs.
/// </summary>
public sealed class WorkingHoursWebsiteStatus
{
    public bool IsOpen { get; init; }
    public bool CanOrder { get; init; }
    public string Message { get; init; } = WorkingHoursSettings.DefaultClosedDayMessage;
    public string? OpenTime { get; init; }
    public string? CloseTime { get; init; }

    /// <summary>True when today's local date has a <see cref="WorkingHoursSpecialDay"/> override.</summary>
    public bool IsSpecial { get; init; }
}

/// <summary>Today's special-day override for website display (from JSON, not a DB table).</summary>
public sealed class WebsiteSpecialDayInfo
{
    public bool IsSpecial { get; init; }
    public bool IsClosed { get; init; }
    public string? Message { get; init; }
    public string? OpenTime { get; init; }
    public string? CloseTime { get; init; }
    public string? Date { get; init; }
}

/// <summary>API DTO returned by working-hours endpoints (same shape as persisted JSON).</summary>
public sealed class WorkingHoursDto
{
    public int ReminderHoursBeforeClosing { get; set; } = WorkingHoursSettings.DefaultReminderHoursBeforeClosing;

    public int StopOnlineOrdersMinutesBeforeClose { get; set; } =
        WorkingHoursSettings.DefaultStopOnlineOrdersMinutesBeforeClose;

    /// <summary>POS preference only — never triggers automatic fiscal Tagesabschluss or blocks POS sales.</summary>
    public bool AutoClosePOSAtClosing { get; set; }

    public string ClosedDayMessage { get; set; } = WorkingHoursSettings.DefaultClosedDayMessage;

    public List<WorkingHoursSpecialDay> SpecialDays { get; set; } = new();

    public WorkingHoursDay Monday { get; set; } = WorkingHoursSettings.CreateDefaultOpenDay();
    public WorkingHoursDay Tuesday { get; set; } = WorkingHoursSettings.CreateDefaultOpenDay();
    public WorkingHoursDay Wednesday { get; set; } = WorkingHoursSettings.CreateDefaultOpenDay();
    public WorkingHoursDay Thursday { get; set; } = WorkingHoursSettings.CreateDefaultOpenDay();
    public WorkingHoursDay Friday { get; set; } = WorkingHoursSettings.CreateDefaultOpenDay();
    public WorkingHoursDay Saturday { get; set; } = WorkingHoursSettings.CreateDefaultOpenDay();
    public WorkingHoursDay Sunday { get; set; } = WorkingHoursSettings.CreateDefaultOpenDay();

    public static WorkingHoursDto From(WorkingHoursSettings? settings)
    {
        var source = settings ?? WorkingHoursSettings.CreateDefault();
        source.Normalize();
        return new WorkingHoursDto
        {
            ReminderHoursBeforeClosing = source.ReminderHoursBeforeClosing,
            StopOnlineOrdersMinutesBeforeClose = source.StopOnlineOrdersMinutesBeforeClose,
            AutoClosePOSAtClosing = source.AutoClosePOSAtClosing,
            ClosedDayMessage = source.ClosedDayMessage,
            SpecialDays = source.SpecialDays
                .Select(CloneSpecialDay)
                .ToList(),
            Monday = CloneDay(source.GetDay(DayOfWeek.Monday)),
            Tuesday = CloneDay(source.GetDay(DayOfWeek.Tuesday)),
            Wednesday = CloneDay(source.GetDay(DayOfWeek.Wednesday)),
            Thursday = CloneDay(source.GetDay(DayOfWeek.Thursday)),
            Friday = CloneDay(source.GetDay(DayOfWeek.Friday)),
            Saturday = CloneDay(source.GetDay(DayOfWeek.Saturday)),
            Sunday = CloneDay(source.GetDay(DayOfWeek.Sunday)),
        };
    }

    public WorkingHoursSettings ToSettings()
    {
        var settings = new WorkingHoursSettings
        {
            ReminderHoursBeforeClosing = ReminderHoursBeforeClosing,
            StopOnlineOrdersMinutesBeforeClose = StopOnlineOrdersMinutesBeforeClose,
            AutoClosePOSAtClosing = AutoClosePOSAtClosing,
            ClosedDayMessage = ClosedDayMessage,
            SpecialDays = (SpecialDays ?? new List<WorkingHoursSpecialDay>())
                .Select(CloneSpecialDay)
                .ToList(),
            Monday = CloneDay(Monday),
            Tuesday = CloneDay(Tuesday),
            Wednesday = CloneDay(Wednesday),
            Thursday = CloneDay(Thursday),
            Friday = CloneDay(Friday),
            Saturday = CloneDay(Saturday),
            Sunday = CloneDay(Sunday),
        };
        settings.Normalize();
        return settings;
    }

    private static WorkingHoursDay CloneDay(WorkingHoursDay? day)
    {
        var source = day ?? WorkingHoursSettings.CreateDefaultOpenDay();
        return new WorkingHoursDay
        {
            OpenTime = source.OpenTime,
            CloseTime = source.CloseTime,
            IsClosed = source.IsClosed,
        };
    }

    private static WorkingHoursSpecialDay CloneSpecialDay(WorkingHoursSpecialDay? day)
    {
        var source = day ?? new WorkingHoursSpecialDay();
        return new WorkingHoursSpecialDay
        {
            Date = source.Date,
            IsClosed = source.IsClosed,
            OpenTime = source.OpenTime,
            CloseTime = source.CloseTime,
        };
    }
}
