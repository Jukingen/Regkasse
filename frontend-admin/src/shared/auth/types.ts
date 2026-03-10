/**
 * Auth user model with permissions – backend /me and login response.
 * Keep in sync with API; role from Identity (e.g. SuperAdmin, Manager). Legacy Admin may appear until re-login.
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
  /** Permission strings (resource.action). Single source for UI guards. Empty when backend does not send yet. */
  permissions?: string[];
  employeeNumber?: string | null;
  taxNumber?: string | null;
  notes?: string | null;
  isActive?: boolean;
  createdAt?: string | null;
  lastLoginAt?: string | null;
}
