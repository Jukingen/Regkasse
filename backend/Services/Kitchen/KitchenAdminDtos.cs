using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Kitchen;

public static class KitchenSettingsDefaults
{
    public const int AutoClearMinutes = 30;
    public const int MinAutoClearMinutes = 1;
    public const int MaxAutoClearMinutes = 240;
    public const bool SoundEnabled = true;
}

public sealed class KitchenSettingsDto
{
    public int AutoClearMinutes { get; init; } = KitchenSettingsDefaults.AutoClearMinutes;
    public bool SoundEnabled { get; init; } = KitchenSettingsDefaults.SoundEnabled;
}

public sealed class UpdateKitchenSettingsRequest
{
    [Range(KitchenSettingsDefaults.MinAutoClearMinutes, KitchenSettingsDefaults.MaxAutoClearMinutes)]
    public int AutoClearMinutes { get; set; } = KitchenSettingsDefaults.AutoClearMinutes;

    public bool SoundEnabled { get; set; } = KitchenSettingsDefaults.SoundEnabled;
}

public sealed class KitchenAdminOrderItemDto
{
    public Guid Id { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public string? Notes { get; init; }

    public KitchenOrderItemStatus Status { get; init; }
}

public sealed class KitchenAdminOrderDto
{
    public Guid Id { get; init; }
    public string? TableNumber { get; init; }
    public KitchenOrderStatus Status { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public IReadOnlyList<KitchenAdminOrderItemDto> Items { get; init; } = [];
}

public sealed class KitchenAnalyticsDto
{
    public double? AveragePrepMinutes { get; init; }
    public double OrdersPerHour { get; init; }
    public int CreatedLast24Hours { get; init; }
    public int CreatedLastHour { get; init; }
}
