using KasseAPI_Final.Models;
using KasseAPI_Final.Services.License;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LicenseAuditLogMapperTests
{
    [Fact]
    public void FromBillingRow_NormalizesActionAndSummarizesReminder()
    {
        var details = """{"dedupKey":"abc","daysBeforeExpiry":7,"recipientEmail":"a@b.c"}""";
        var row = LicenseAuditLogMapper.FromBillingRow(
            Guid.NewGuid(),
            DateTime.UtcNow,
            Guid.NewGuid(),
            "Cafe Test",
            BillingAuditEventTypes.LicenseReminderSent,
            details,
            "System");

        Assert.Equal("LICENSE_REMINDER_SENT", row.Action);
        Assert.Null(row.FromStatus);
        Assert.Contains("a@b.c", row.Reason, StringComparison.Ordinal);
        Assert.Equal(LicenseAuditLogMapper.SourceBilling, row.Source);
    }

    [Fact]
    public void FromAuditLogRow_InfersStatusesFromExpiryPayload()
    {
        var reference = new DateTime(2026, 7, 26, 12, 0, 0, DateTimeKind.Utc);
        var request = """
            {
              "PreviousExpiryUtc": "2026-07-20T00:00:00Z",
              "NewExpiryDate": "2027-07-26T00:00:00Z"
            }
            """;

        var row = LicenseAuditLogMapper.FromAuditLogRow(
            Guid.NewGuid(),
            reference,
            Guid.NewGuid(),
            "Cafe Test",
            AuditEventType.LicenseRenewed,
            AuditLogActions.LICENSE_RENEWED,
            "License renewed.",
            request,
            "Ada Admin",
            gracePeriodDays: 7);

        Assert.Equal("LICENSE_RENEWED", row.Action);
        Assert.Equal("Grace", row.FromStatus);
        Assert.Equal("Active", row.ToStatus);
        Assert.Equal("License renewed.", row.Reason);
    }

    [Fact]
    public void InferLifecycleStatus_UsesGraceWindow()
    {
        var now = new DateTime(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal("Active", LicenseAuditLogMapper.InferLifecycleStatus(now.AddDays(10), now));
        Assert.Equal("Grace", LicenseAuditLogMapper.InferLifecycleStatus(now.AddDays(-3), now, 7));
        Assert.Equal("Expired", LicenseAuditLogMapper.InferLifecycleStatus(now.AddDays(-20), now, 7));
        Assert.Null(LicenseAuditLogMapper.InferLifecycleStatus(null, now));
    }

    [Fact]
    public void MaskLicenseKey_MasksRegkKeysInText()
    {
        var masked = LicenseAuditLogMapper.MaskSecretsInText(
            "Key REGK-AAAAA-BBBBB-CCCCC issued");
        Assert.DoesNotContain("BBBBB", masked, StringComparison.Ordinal);
        Assert.Contains("REGK-AAA…", masked, StringComparison.Ordinal);
    }

    [Fact]
    public void DeduplicatePreferBilling_DropsMatchingAuditWithinOneMinute()
    {
        var tenantId = Guid.NewGuid();
        var t = DateTime.UtcNow;
        var billing = LicenseAuditLogMapper.FromBillingRow(
            Guid.NewGuid(),
            t,
            tenantId,
            "T",
            BillingAuditEventTypes.LicenseExtended,
            null,
            "SA");
        var audit = LicenseAuditLogMapper.FromAuditLogRow(
            Guid.NewGuid(),
            t.AddSeconds(30),
            tenantId,
            "T",
            AuditEventType.LicenseExtended,
            AuditLogActions.LICENSE_EXTENDED,
            "Tenant license extended.",
            null,
            "SA");

        var result = LicenseAuditLogMapper.DeduplicatePreferBilling([audit, billing]);
        Assert.Single(result);
        Assert.Equal(LicenseAuditLogMapper.SourceBilling, result[0].Source);
    }
}
