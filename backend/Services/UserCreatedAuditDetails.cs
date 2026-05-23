namespace KasseAPI_Final.Services;

/// <summary>Safe audit metadata for user creation (never includes password values).</summary>
public sealed record UserCreatedAuditDetails(
    string CreatedByUserId,
    string Role,
    Guid? TenantId = null,
    bool PasswordReturned = true);
