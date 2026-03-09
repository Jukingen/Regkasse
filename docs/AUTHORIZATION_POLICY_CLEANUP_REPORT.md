# Authorization Policy Cleanup — Report

**Status:** Superseded. The app uses **permission-first** authorization only. Legacy role policies (AdminUsers, UsersView, PosSales, etc.) are **not registered**. Single admin role is **Admin** (no Administrator).

**Current model:** [architecture/FINAL_AUTHORIZATION_MODEL.md](architecture/FINAL_AUTHORIZATION_MODEL.md).

---

## Historical summary

This report described cleanup to canonical roles (Admin only, no Administrator) and removal of legacy policy registration. That migration is **complete**:

- `AuthorizationExtensions.AddLegacyRolePolicies` is intentionally empty (no legacy policies registered).
- All endpoint protection is via `[HasPermission(AppPermissions.X)]`; handler uses `RolePermissionMatrix` and JWT permission claims.

## Token / role flow (current)

- **RoleCanonicalization:** Trim only; no role aliases.
- **TokenClaimsService:** Builds claims from Identity roles; permissions from `RolePermissionMatrix.GetPermissionsForRoles(roles)`.
- **Result:** JWT has `role` (canonical) and `permission` claims; authorization is permission-based.

## Rollback

- Revert `AuthorizationExtensions.cs` only if re-adding legacy policies; single admin role remains **Admin**.
