/**
 * FA permission catalog entrypoint — string values must match
 * `backend/Authorization/AppPermissions.cs` exactly (case-sensitive).
 *
 * Implementation lives in `./permissions.ts` (PERMISSIONS + AppPermissions helpers).
 * Prefer importing from here when documenting / auditing the catalog surface;
 * existing call sites may keep `@/shared/auth/permissions`.
 *
 * Consistency gate: `node scripts/verify-permission-keys.mjs`
 */
export {
  ANY_AUTHENTICATED_PERMISSION,
  AppPermissions,
  hasAllPermissions,
  hasAnyPermission,
  hasPermission,
  type Permission,
  PERMISSIONS,
  type UserWithPermissions,
} from './permissions';
