# Demo Role → IsDemo Flag Migration

**Last updated:** 2025-03-10

## Goal

Replace the legacy "Demo" **role** with an explicit **IsDemo** flag on the user. Demo restrictions are preserved; the role is no longer used for authorization in new code paths.

## Code Paths That Depended on Role == "Demo"

| Location | Purpose | Replacement |
|----------|---------|-------------|
| `backend/Services/PaymentService.cs` | Block create/cancel/refund for demo users | `user?.IsDemo == true \|\| user?.Role == "Demo"` (both kept for backward compat) |
| `backend/Services/UserService.cs` (HasPermissionAsync) | Limit demo users to view-only permissions | First check: `user.IsDemo \|\| user.Role == "Demo"` → allow only payment.view, customer.view, product.view; "Demo" removed from role switch |
| `backend/Data/RoleSeedData.cs` | No longer seeds "Demo" role | N/A (already only seeds 8 canonical roles) |
| `backend/Data/UserSeedData.cs` | Demo seed user | Uses **Cashier** role + **IsDemo = true** (no Demo role) |

**Not changed:** TSE/config "Demo" (e.g. `TseMode: "Demo"`) is **device mode**, not user role; unchanged.

## Replacement Model

- **Source of truth for “demo user”:** `ApplicationUser.IsDemo` (and, during transition, `Role == "Demo"`).
- **Permissions:** Demo users (by flag or legacy role) get only: `payment.view`, `customer.view`, `product.view`.
- **Payment actions:** Create/cancel/refund are blocked in `PaymentService` for users with `IsDemo == true` or `Role == "Demo"`.
- **New demo accounts:** Create user with a normal role (e.g. Cashier) and set `IsDemo = true`; do not assign a "Demo" role.

## Migration and rollout impact

1. **Existing DBs with Role = "Demo":** Still supported: both `PaymentService` and `UserService` treat `Role == "Demo"` as demo (restrictions + view-only permissions). No mandatory data migration.
2. **Optional data migration (recommended):** For each user with `Role = 'Demo'`:
   - Set `IsDemo = true`.
   - Reassign to a canonical role (e.g. Cashier): update `ApplicationUser.Role` and `AspNetUserRoles` (remove Demo, add Cashier).
   - After no user has the Demo role, delete the "Demo" role from `AspNetRoles` (custom-role delete when userCount = 0).
3. **After migration:** You can remove the `user?.Role == "Demo"` fallback from `PaymentService` and the `Role == "Demo"` check from `UserService` (optional cleanup).

**Rollout:** Deploy the flag-based logic first (current code is backward compatible). Optionally run data migration to set IsDemo and reassign Demo-role users to Cashier; then delete the Demo role when empty. Only after all Demo-role users are migrated consider removing the `Role == "Demo"` fallback from code.

## Risks

- **Removing Role == "Demo" too early:** If you remove the fallback before migrating users, accounts that still have Role = "Demo" and IsDemo = false would lose view-only behavior (UserService would give them no permissions) and would not be blocked by PaymentService. Keep the fallback until data migration is done.
- **Permission widening:** Not done. Demo users (by flag or legacy role) are restricted to view-only in UserService and blocked from create/cancel/refund in PaymentService.
- **Live payment logic:** Unchanged; blocking conditions are additive (IsDemo || Role == "Demo").

## Optional SQL (manual run after backup)

Use only after backup and in a maintenance window. Reassigns Demo-role users to Cashier and sets IsDemo.

```sql
-- 1) Set IsDemo and Role for users that still have Role = 'Demo'
UPDATE "AspNetUsers"
SET "IsDemo" = true, "Role" = 'Cashier'
WHERE "Role" = 'Demo';

-- 2) Reassign in AspNetUserRoles (requires role IDs; run per environment)
-- First add user to Cashier, then remove from Demo (see application logic in UserManagementController for role swap).
-- 3) After no user is in Demo role, delete the Demo role (via API or manually).
```

Prefer using the **Admin UI**: change each Demo-role user to role Cashier and check “Demo user” (IsDemo), then delete the Demo role when it has zero users.

## Files Reference

| File | Change |
|------|--------|
| `backend/Services/UserService.cs` | Demo = IsDemo \|\| Role=="Demo"; view-only permissions; "Demo" removed from role switch |
| `backend/Services/PaymentService.cs` | No change; already uses IsDemo \|\| Role=="Demo" |
| `backend/Data/UserSeedData.cs` | Demo seed user: Cashier + IsDemo = true |
| `backend/Data/RoleSeedData.cs` | Does not seed Demo role |
| `backend/Models/ApplicationUser.cs` | IsDemo property (existing) |
