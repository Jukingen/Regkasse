/**
 * Core authenticated user fields from login response and GET /api/Auth/me.
 * Extended session context lives in `AuthUser` (`@/shared/auth/types`).
 */
export interface User {
  id: string;
  email: string;
  userName: string;
  role: string;
  /** When true, user must change password before using the admin app. */
  mustChangePasswordOnNextLogin: boolean;
}
