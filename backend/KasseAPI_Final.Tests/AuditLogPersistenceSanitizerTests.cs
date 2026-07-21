using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AuditLogPersistenceSanitizerTests
{
    [Fact]
    public void Truncate_short_path_unchanged()
    {
        var s = "/api/admin/backup/runs";
        Assert.Equal(s, AuditLogPersistenceSanitizer.Truncate(s, AuditLogPersistenceSanitizer.EndpointMaxLength));
    }

    [Fact]
    public void Truncate_long_download_path_fits_varchar_100()
    {
        var run = "11111111-1111-1111-1111-111111111111";
        var art = "22222222-2222-2222-2222-222222222222";
        var path = $"/api/admin/backup/runs/{run}/artifacts/{art}/download";
        Assert.True(path.Length > 100, "precondition: route must exceed legacy audit endpoint column");

        var t = AuditLogPersistenceSanitizer.Truncate(path, AuditLogPersistenceSanitizer.EndpointMaxLength);
        Assert.Equal(100, t!.Length);
        Assert.StartsWith("/api/admin/backup/runs/", t, StringComparison.Ordinal);
    }

    [Fact]
    public void Truncate_user_id_matches_audit_column_450()
    {
        var id = new string('a', 450);
        Assert.Equal(id, AuditLogPersistenceSanitizer.TruncateUserId(id));
        var longId = new string('b', 451);
        Assert.Equal(450, AuditLogPersistenceSanitizer.TruncateUserId(longId).Length);
    }

    [Fact]
    public void SerializeObjectToJsonColumn_truncates_to_json_payload_max()
    {
        var huge = new { x = new string('z', AuditLogPersistenceSanitizer.JsonPayloadMaxLength + 500) };
        var json = AuditLogPersistenceSanitizer.SerializeObjectToJsonColumn(huge);
        Assert.NotNull(json);
        Assert.Equal(AuditLogPersistenceSanitizer.JsonPayloadMaxLength, json!.Length);
    }

    [Fact]
    public void SerializeObjectToJsonColumn_redacts_password_and_voucher_code()
    {
        var payload = new
        {
            userName = "cashier1",
            password = "Secret123!",
            voucherCode = "GIFT-ABC-999",
            amount = 12.5m,
        };

        var json = AuditLogPersistenceSanitizer.SerializeObjectToJsonColumn(payload);
        Assert.NotNull(json);
        Assert.DoesNotContain("Secret123!", json, StringComparison.Ordinal);
        Assert.DoesNotContain("GIFT-ABC-999", json, StringComparison.Ordinal);
        Assert.Contains(AuditLogPersistenceSanitizer.RedactedPlaceholder, json, StringComparison.Ordinal);
        Assert.Contains("cashier1", json, StringComparison.Ordinal);
    }

    [Fact]
    public void TruncateForAction_entityType_userRole_respect_model_limits()
    {
        Assert.Equal(50, AuditLogPersistenceSanitizer.TruncateForAction(new string('a', 80)).Length);
        Assert.Equal(100, AuditLogPersistenceSanitizer.TruncateForEntityType(new string('b', 120)).Length);
        Assert.Equal(50, AuditLogPersistenceSanitizer.TruncateForUserRole(new string('c', 60)).Length);
    }
}
