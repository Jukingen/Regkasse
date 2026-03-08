namespace KasseAPI_Final.Models;

/// <summary>
/// Tutarlı API hata modeli – FE formları ve istemci işleme için.
/// </summary>
public class ApiError
{
    public string Type { get; set; } = "Problem";
    public string Title { get; set; } = string.Empty;
    public int Status { get; set; }
    public string? Detail { get; set; }
    public string? Instance { get; set; }
    /// <summary>Optional reason code for 403 Forbidden (e.g. USERS_MANAGE_REQUIRED). FE can map to i18n.</summary>
    public string? ReasonCode { get; set; }
    /// <summary>Validation hataları: alan adı → hata mesajları.</summary>
    public Dictionary<string, string[]>? Errors { get; set; }

    public static ApiError Validation(string title, Dictionary<string, string[]> errors, string? detail = null) =>
        new() { Type = "ValidationError", Title = title, Status = 400, Detail = detail, Errors = errors };

    public static ApiError NotFound(string title, string? detail = null) =>
        new() { Type = "NotFound", Title = title, Status = 404, Detail = detail };

    public static ApiError Forbidden(string title = "Forbidden", string? detail = null, string? reasonCode = null) =>
        new() { Type = "Forbidden", Title = title, Status = 403, Detail = detail, ReasonCode = reasonCode };

    public static ApiError Conflict(string title, string? detail = null) =>
        new() { Type = "Conflict", Title = title, Status = 409, Detail = detail };

    public static ApiError ConcurrencyConflict(string? detail = null) =>
        new() { Type = "Conflict", Title = "The resource was modified by another request. Please refresh and try again.", Status = 412, Detail = detail };

    public static ApiError BusinessRule(string title, string? detail = null, int status = 400) =>
        new() { Type = "BusinessRule", Title = title, Status = status, Detail = detail };

    /// <summary>Structured 403 payload for API contract and FE diagnostics. No secret leakage.</summary>
    public class ForbiddenPayload
    {
        public const string Code = "AUTH_FORBIDDEN";
        public const string Reason = "MISSING_ROLE_OR_SCOPE";

        public string CodeValue { get; set; } = Code;
        public string ReasonValue { get; set; } = Reason;
        public string? RequiredPolicy { get; set; }
        public string? MissingRequirement { get; set; }
        public string? CorrelationId { get; set; }
    }

    /// <summary>Stable reason codes for 403 Forbidden. FE can map to i18n (e.g. errors.forbidden.USERS_MANAGE_REQUIRED).</summary>
    public static class ForbiddenReasonCodes
    {
        public const string Forbidden = "FORBIDDEN";
        public const string UsersViewRequired = "USERS_VIEW_REQUIRED";
        public const string UsersManageRequired = "USERS_MANAGE_REQUIRED";
        public const string UsersExportRequired = "USERS_EXPORT_REQUIRED";
        public const string UsersAssignRoleRequired = "USERS_ASSIGN_ROLE_REQUIRED";
        public const string UsersTransferBranchRequired = "USERS_TRANSFER_BRANCH_REQUIRED";
        public const string ScopeBranch = "SCOPE_BRANCH";
    }
}
