/**
 * Auth user model — aligned with GET /api/Auth/me and OpenAPI `UserInfo` (Orval).
 * Permissions match JWT effective set (IRolePermissionResolver).
 * Tenant/branch: optional; populated when the API resolves context (see mapMeResponseToAuthUser).
 */
export interface AuthUser {
  id: string | null;
  userName?: string | null;
  email?: string | null;
  firstName?: string | null;
  lastName?: string | null;
  /** Canonical role (e.g. SuperAdmin, Manager). */
  role?: string | null;
  /** All role names from Identity. */
  roles?: string[];
  /** Permission strings (resource.action). Same effective set as JWT permission claims. */
  permissions?: string[];
  employeeNumber?: string | null;
  taxNumber?: string | null;
  notes?: string | null;
  isActive?: boolean;
  isDemo?: boolean;
  /** From JWT app_context when present (admin | pos). */
  appContext?: string | null;
  tenantId?: string | null;
  tenantDisplayName?: string | null;
  branchId?: string | null;
  branchDisplayName?: string | null;
  createdAt?: string | null;
  lastLoginAt?: string | null;
}
