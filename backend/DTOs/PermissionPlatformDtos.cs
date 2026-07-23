namespace KasseAPI_Final.DTOs;

public sealed class PermissionRequestDto
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string RequesterUserId { get; set; } = string.Empty;
    public string? RequesterUserName { get; set; }
    public string Permission { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string RequestedDuration { get; set; } = string.Empty;
    public DateTime? RequestedExpiresAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public string? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
    public Guid? ResultingOverrideId { get; set; }
}

public sealed class CreatePermissionRequestBody
{
    public string Permission { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Duration { get; set; } = "7d";
    public DateTime? CustomExpiresAt { get; set; }
}

public sealed class ResolvePermissionRequestBody
{
    public string? Note { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public sealed class PermissionRequestStatsDto
{
    public int Pending { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public int Total { get; set; }
}

public sealed class PermissionRequestMutationResult
{
    public bool Succeeded { get; set; }
    public string? Code { get; set; }
    public string? Error { get; set; }
    public PermissionRequestDto? Request { get; set; }
}

public sealed class PermissionPackageDto
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public int PermissionCount { get; set; }
    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class UpsertPermissionPackageRequest
{
    public string? Slug { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
}

public sealed class RoleAssignedPackageDto
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int PermissionCount { get; set; }
}

public sealed class RolePermissionSimulateRequest
{
    public IReadOnlyList<string> ProposedPermissions { get; set; } = Array.Empty<string>();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed class RolePermissionSimulateUserImpactDto
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string DisplayRole { get; set; } = string.Empty;
    public int PermissionsGained { get; set; }
    public int PermissionsLost { get; set; }
    public IReadOnlyList<string> GainedKeysSample { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> LostKeysSample { get; set; } = Array.Empty<string>();
}

public sealed class RolePermissionSimulateResultDto
{
    public string RoleName { get; set; } = string.Empty;
    public IReadOnlyList<string> CurrentPermissions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ProposedPermissions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Added { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Removed { get; set; } = Array.Empty<string>();
    public int AffectedUserCount { get; set; }
    public IReadOnlyList<RolePermissionSimulateUserImpactDto> Users { get; set; } = Array.Empty<RolePermissionSimulateUserImpactDto>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
}

public sealed class PermissionConfigBackupListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedByUserId { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
}

public sealed class CreatePermissionConfigBackupRequest
{
    public string? Name { get; set; }
    public string? Note { get; set; }
}

public sealed class PermissionConfigBackupSettingsDto
{
    public bool AutoBackupBeforeChanges { get; set; }
}

public sealed class PermissionConfigRestorePreviewDto
{
    public Guid BackupId { get; set; }
    public int CustomRolesChanged { get; set; }
    public int PackagesChanged { get; set; }
    public int OverridesChanged { get; set; }
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> SampleRoleDeltas { get; set; } = Array.Empty<string>();
}

public sealed class PermissionAnalyticsNamedCountDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int UserCount { get; set; }
    public double Percent { get; set; }
}

public sealed class PermissionAnalyticsRecommendationDto
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
    public string Message { get; set; } = string.Empty;
    public string? Arg { get; set; }
}

public sealed class PermissionAnalyticsSummaryDto
{
    public int TotalUsers { get; set; }
    public int TotalRoles { get; set; }
    public int TotalPermissions { get; set; }
    public IReadOnlyList<PermissionAnalyticsNamedCountDto> MostUsed { get; set; } = Array.Empty<PermissionAnalyticsNamedCountDto>();
    public IReadOnlyList<PermissionAnalyticsNamedCountDto> LeastUsed { get; set; } = Array.Empty<PermissionAnalyticsNamedCountDto>();
    public IReadOnlyList<PermissionAnalyticsNamedCountDto> RoleDistribution { get; set; } = Array.Empty<PermissionAnalyticsNamedCountDto>();
    public IReadOnlyList<PermissionAnalyticsNamedCountDto> OverPrivilegedUsers { get; set; } = Array.Empty<PermissionAnalyticsNamedCountDto>();
    public IReadOnlyList<string> UnusedPermissions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<PermissionAnalyticsRecommendationDto> Recommendations { get; set; } = Array.Empty<PermissionAnalyticsRecommendationDto>();
}

public sealed class PermissionAnalyticsTrendPointDto
{
    public string Date { get; set; } = string.Empty;
    public int TotalUsers { get; set; }
    public string PayloadJson { get; set; } = "{}";
}

public sealed class IndustryTemplateSlotDto
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string SystemRole { get; set; } = string.Empty;
    public IReadOnlyList<string> RecommendedPackageSlugs { get; set; } = Array.Empty<string>();
    public bool SeedStarterUser { get; set; }
}

public sealed class IndustryTemplateDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? SuggestedDemoImportProfileId { get; set; }
    public IReadOnlyList<IndustryTemplateSlotDto> Slots { get; set; } = Array.Empty<IndustryTemplateSlotDto>();
}

public sealed class SetTenantIndustryTemplateRequest
{
    public string? IndustryTemplateId { get; set; }
    public bool SeedMissingStarters { get; set; }
}
