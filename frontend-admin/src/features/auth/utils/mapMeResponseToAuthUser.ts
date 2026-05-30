import type { UserInfo } from '@/api/generated/model';
import type { AuthUser } from '@/shared/auth/types';

/**
 * GET /api/Auth/me payload: OpenAPI UserInfo plus legacy PascalCase and permission arrays.
 */
type MeResponseCamelExtensions = {
    isDemo?: boolean;
    appContext?: string | null;
    tenantId?: string | null;
    tenantSlug?: string | null;
    tenantDisplayName?: string | null;
    branchId?: string | null;
    branchDisplayName?: string | null;
};

export type SessionPolicyResponse = {
    sessionTimeoutMinutes?: number;
    warningBeforeTimeoutMinutes?: number;
    keepCartAfterTimeout?: boolean;
    idleTimeoutEnabled?: boolean;
    SessionTimeoutMinutes?: number;
    WarningBeforeTimeoutMinutes?: number;
    KeepCartAfterTimeout?: boolean;
    IdleTimeoutEnabled?: boolean;
};

export type MeResponse = UserInfo & MeResponseCamelExtensions & {
    sessionPolicy?: SessionPolicyResponse;
    permissions?: string[];
    Permissions?: string[];
    roles?: string[];
    Roles?: string[];
    Id?: string | null;
    UserName?: string | null;
    Email?: string | null;
    FirstName?: string | null;
    LastName?: string | null;
    Role?: string | null;
    EmployeeNumber?: string | null;
    TaxNumber?: string | null;
    Notes?: string | null;
    IsActive?: boolean;
    IsDemo?: boolean;
    AppContext?: string | null;
    TenantId?: string | null;
    TenantSlug?: string | null;
    TenantDisplayName?: string | null;
    BranchId?: string | null;
    BranchDisplayName?: string | null;
    CreatedAt?: string;
    LastLoginAt?: string;
    mustChangePasswordOnNextLogin?: boolean;
    MustChangePasswordOnNextLogin?: boolean;
};

/** Maps /me (or equivalent) JSON to AuthUser; tenant/branch fields stay null-safe. */
export function mapMeResponseToAuthUser(res: MeResponse): AuthUser {
    const permissions = res.permissions ?? res.Permissions ?? [];
    const roles = res.roles ?? res.Roles ?? [];

    return {
        id: res.id ?? res.Id ?? null,
        userName: res.userName ?? res.UserName,
        email: res.email ?? res.Email,
        firstName: res.firstName ?? res.FirstName,
        lastName: res.lastName ?? res.LastName,
        role: res.role ?? res.Role,
        roles: roles.length > 0 ? roles : undefined,
        permissions,
        employeeNumber: res.employeeNumber ?? res.EmployeeNumber,
        taxNumber: res.taxNumber ?? res.TaxNumber,
        notes: res.notes ?? res.Notes,
        isActive: res.isActive ?? res.IsActive,
        isDemo: res.isDemo ?? res.IsDemo,
        appContext: res.appContext ?? res.AppContext ?? undefined,
        tenantId: res.tenantId ?? res.TenantId ?? null,
        tenantSlug: res.tenantSlug ?? res.TenantSlug ?? null,
        tenantDisplayName: res.tenantDisplayName ?? res.TenantDisplayName ?? null,
        branchId: res.branchId ?? res.BranchId ?? null,
        branchDisplayName: res.branchDisplayName ?? res.BranchDisplayName ?? null,
        createdAt: res.createdAt ?? res.CreatedAt,
        lastLoginAt: res.lastLoginAt ?? res.LastLoginAt,
        mustChangePasswordOnNextLogin:
            res.mustChangePasswordOnNextLogin ?? res.MustChangePasswordOnNextLogin ?? false,
        sessionPolicy: mapSessionPolicy(res.sessionPolicy),
    };
}

function mapSessionPolicy(raw: SessionPolicyResponse | undefined) {
    if (!raw) return undefined;
    const timeout =
        raw.sessionTimeoutMinutes ?? raw.SessionTimeoutMinutes;
    const warning =
        raw.warningBeforeTimeoutMinutes ?? raw.WarningBeforeTimeoutMinutes;
    if (timeout == null && warning == null) return undefined;
    return {
        sessionTimeoutMinutes: timeout ?? 30,
        warningBeforeTimeoutMinutes: warning ?? 1,
        keepCartAfterTimeout: raw.keepCartAfterTimeout ?? raw.KeepCartAfterTimeout,
        idleTimeoutEnabled: raw.idleTimeoutEnabled ?? raw.IdleTimeoutEnabled ?? true,
    };
}
