using KasseAPI_Final.Models;
using KasseAPI_Final.Services.License;

namespace KasseAPI_Final.Services.AdminTenants;

internal static class TenantLicensePreviewHelper
{
    internal static string InferPlanKey(int durationDays) =>
        durationDays switch
        {
            >= 360 and <= 370 => "annual",
            >= 85 and <= 95 => "quarterly",
            >= 28 and <= 31 => "monthly",
            _ => "custom",
        };

    internal static string FormatDurationDisplay(int durationDays) =>
        durationDays switch
        {
            >= 360 and <= 370 => "1 year",
            >= 85 and <= 95 => "90 days",
            >= 28 and <= 31 => "30 days",
            1 => "1 day",
            _ => $"{durationDays} days",
        };

    internal static string FormatPlanName(string planKey) =>
        planKey switch
        {
            "annual" => "Annual license",
            "quarterly" => "Quarterly license",
            "monthly" => "Monthly license",
            _ => "Custom license",
        };

    internal static LicensePreviewResult BuildValidPreview(IssuedLicense issued)
    {
        var validFrom = DateTime.SpecifyKind(issued.IssuedAtUtc, DateTimeKind.Utc);
        var validUntil = DateTime.SpecifyKind(issued.ExpiryAtUtc, DateTimeKind.Utc);
        var durationDays = Math.Max(1, (int)Math.Round((validUntil - validFrom).TotalDays));
        var planKey = InferPlanKey(durationDays);

        return new LicensePreviewResult(
            Valid: true,
            LicenseKey: issued.LicenseKey,
            ValidFromUtc: validFrom,
            ValidUntilUtc: validUntil,
            DurationDays: durationDays,
            DurationDisplay: FormatDurationDisplay(durationDays),
            Status: "valid",
            PlanName: FormatPlanName(planKey),
            ErrorCode: null,
            ErrorMessage: null);
    }

    internal static LicensePreviewResult BuildValidPreview(LicenseSale sale)
    {
        var validFrom = DateTime.SpecifyKind(sale.ValidFromUtc, DateTimeKind.Utc);
        var validUntil = DateTime.SpecifyKind(sale.ValidUntilUtc, DateTimeKind.Utc);
        var durationDays = Math.Max(1, (int)Math.Round((validUntil - validFrom).TotalDays));

        return new LicensePreviewResult(
            Valid: true,
            LicenseKey: sale.LicenseKey,
            ValidFromUtc: validFrom,
            ValidUntilUtc: validUntil,
            DurationDays: durationDays,
            DurationDisplay: FormatDurationDisplay(durationDays),
            Status: "valid",
            PlanName: FormatBillingPlanName(sale.LicensePlan),
            ErrorCode: null,
            ErrorMessage: null);
    }

    internal static string FormatBillingPlanName(string licensePlan) =>
        licensePlan switch
        {
            LicenseSalePlans.TwelveMonths => "12-month license",
            LicenseSalePlans.SixMonths => "6-month license",
            LicenseSalePlans.Custom => "Custom license",
            _ => "License",
        };

    internal static LicensePreviewResult BuildInvalidPreview(IssuedLicenseResolveResult resolved)
    {
        var status = resolved.ErrorCode switch
        {
            "expired" => "expired",
            _ => "invalid",
        };

        if (resolved.Issued == null)
        {
            return new LicensePreviewResult(
                Valid: false,
                LicenseKey: null,
                ValidFromUtc: null,
                ValidUntilUtc: null,
                DurationDays: null,
                DurationDisplay: null,
                Status: status,
                PlanName: null,
                ErrorCode: resolved.ErrorCode,
                ErrorMessage: resolved.ErrorMessage);
        }

        var issued = resolved.Issued;
        var validFrom = DateTime.SpecifyKind(issued.IssuedAtUtc, DateTimeKind.Utc);
        var validUntil = DateTime.SpecifyKind(issued.ExpiryAtUtc, DateTimeKind.Utc);
        var durationDays = Math.Max(1, (int)Math.Round((validUntil - validFrom).TotalDays));
        var planKey = InferPlanKey(durationDays);

        return new LicensePreviewResult(
            Valid: false,
            LicenseKey: issued.LicenseKey,
            ValidFromUtc: validFrom,
            ValidUntilUtc: validUntil,
            DurationDays: durationDays,
            DurationDisplay: FormatDurationDisplay(durationDays),
            Status: status,
            PlanName: FormatPlanName(planKey),
            ErrorCode: resolved.ErrorCode,
            ErrorMessage: resolved.ErrorMessage);
    }

    internal static LicensePreviewResult BuildInvalidPreview(BillingLicenseSaleResolveResult resolved)
    {
        var status = resolved.ErrorCode switch
        {
            "expired" => "expired",
            _ => "invalid",
        };

        if (resolved.Sale == null)
        {
            return new LicensePreviewResult(
                Valid: false,
                LicenseKey: null,
                ValidFromUtc: null,
                ValidUntilUtc: null,
                DurationDays: null,
                DurationDisplay: null,
                Status: status,
                PlanName: null,
                ErrorCode: resolved.ErrorCode,
                ErrorMessage: resolved.ErrorMessage);
        }

        var sale = resolved.Sale;
        var validFrom = DateTime.SpecifyKind(sale.ValidFromUtc, DateTimeKind.Utc);
        var validUntil = DateTime.SpecifyKind(sale.ValidUntilUtc, DateTimeKind.Utc);
        var durationDays = Math.Max(1, (int)Math.Round((validUntil - validFrom).TotalDays));

        return new LicensePreviewResult(
            Valid: false,
            LicenseKey: sale.LicenseKey,
            ValidFromUtc: validFrom,
            ValidUntilUtc: validUntil,
            DurationDays: durationDays,
            DurationDisplay: FormatDurationDisplay(durationDays),
            Status: status,
            PlanName: FormatBillingPlanName(sale.LicensePlan),
            ErrorCode: resolved.ErrorCode,
            ErrorMessage: resolved.ErrorMessage);
    }

    internal static LicensePreviewResult FromUnified(
        LicenseKeyValidationResult validation,
        LicenseInfo info)
    {
        var validUntil = info.ValidUntilUtc ?? validation.DatabaseValidUntilUtc ?? validation.EncodedValidUntilUtc;
        int? durationDays = null;
        string? durationDisplay = null;
        if (validUntil is DateTime until)
        {
            var untilUtc = until.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(until, DateTimeKind.Utc)
                : until.ToUniversalTime();
            durationDays = Math.Max(0, (int)Math.Ceiling((untilUtc - DateTime.UtcNow).TotalDays));
            durationDisplay = durationDays.Value > 0
                ? FormatDurationDisplay(durationDays.Value)
                : "expired";
        }

        var status = validation.IsValid
            ? "valid"
            : validation.ErrorCode switch
            {
                "expired" => "expired",
                "revoked" => "invalid",
                _ => "invalid",
            };

        return new LicensePreviewResult(
            Valid: validation.IsValid,
            LicenseKey: string.IsNullOrWhiteSpace(info.CanonicalLicenseKey)
                ? info.LicenseKey
                : info.CanonicalLicenseKey,
            ValidFromUtc: null,
            ValidUntilUtc: validUntil,
            DurationDays: durationDays,
            DurationDisplay: durationDisplay,
            Status: status,
            PlanName: info.LicenseKind,
            ErrorCode: validation.ErrorCode,
            ErrorMessage: validation.Message);
    }
}
