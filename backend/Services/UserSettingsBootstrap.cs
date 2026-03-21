using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>
/// Ensures a UserSettings row exists (same defaults as UserSettingsController GET bootstrap).
/// </summary>
public static class UserSettingsBootstrap
{
    public static UserSettings CreateDefaultRow(string userId)
    {
        return new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Language = "de-DE",
            Currency = "EUR",
            DateFormat = "DD.MM.YYYY",
            TimeFormat = "24h",
            DefaultTaxRate = 20,
            EnableDiscounts = true,
            EnableCoupons = true,
            AutoPrintReceipts = false,
            ReceiptHeader = "Registrierkasse - Kassenbeleg",
            ReceiptFooter = "Vielen Dank für Ihren Einkauf!",
            FinanzOnlineEnabled = false,
            SessionTimeout = 30,
            RequirePinForRefunds = true,
            MaxDiscountPercentage = 50,
            Theme = "light",
            CompactMode = false,
            ShowProductImages = true,
            EnableNotifications = true,
            LowStockAlert = true,
            DefaultPaymentMethod = "mixed",
            DefaultTableNumber = "1",
            DefaultWaiterName = "Kasiyer",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static async Task<UserSettings> GetOrCreateTrackedUserSettingsAsync(
        AppDbContext context,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var existing = await context.UserSettings
            .FirstOrDefaultAsync(us => us.UserId == userId, cancellationToken);
        if (existing != null)
            return existing;

        var created = CreateDefaultRow(userId);
        context.UserSettings.Add(created);
        await context.SaveChangesAsync(cancellationToken);
        return created;
    }
}
