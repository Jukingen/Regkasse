# Role Seed and Legacy Role Handling

**Last updated:** 2025-03-10

## Canonical roles (seed and system)

The application uses **eight canonical system roles** defined in `backend/Authorization/Roles.cs` and seeded by `backend/Data/RoleSeedData.cs`:

- SuperAdmin  
- Admin  
- Manager  
- Cashier  
- Waiter  
- Kitchen  
- ReportViewer  
- Accountant  

**Seed strategy:** `RoleSeedData.SeedRolesAsync` only **creates** any of these eight roles if they do not exist. It does **not** delete or rename any existing roles. This keeps backward compatibility: existing databases may still contain legacy role rows and user assignments; the seed does not touch them.

## Roles removed from canonical/seed

The following role names are **no longer** in `Roles.Canonical` and **no longer created** by seed:

| Legacy name   | Target (normalization) | Notes |
|---------------|------------------------|--------|
| Administrator | Admin                  | Merge into Admin (same permissions). |
| Kellner       | Waiter                 | Same role, German name. |
| BranchManager | Manager                | Merge into Manager. |
| Auditor       | ReportViewer           | Same intent (read-only reports). |
| Demo          | (none as role)         | Use user flag `IsDemo`; assign e.g. Cashier. |

## Where legacy roles may still exist

- **AspNetRoles:** Rows with `Name` in `('Administrator','Kellner','BranchManager','Auditor','Demo')` may still exist if they were created by an older seed or manually.
- **AspNetUserRoles:** Users may still be assigned to these legacy roles.
- **ApplicationUser.Role:** Denormalized role name; may still hold a legacy name for some users.

The backend treats any role **not** in `Roles.Canonical` as a **custom role**: it can be edited and deleted (when no users are assigned). So legacy roles appear as custom in the admin UI and are subject to the same “cannot delete if users assigned” rule.

## Safe handling of legacy roles

1. **Do not delete in seed.** Seed only ensures the eight canonical roles exist. It never deletes or renames roles.
2. **Reassign then delete.** For each legacy role that should go away:
   - In the admin UI (or via API), reassign all users from the legacy role to the target canonical role (e.g. Kellner → Waiter, Auditor → ReportViewer).
   - After the role has zero users, delete the role (custom-role delete is allowed when `userCount == 0`).
3. **Demo users.** Prefer the `IsDemo` flag on the user instead of a “Demo” role. Demo seed user is created with role **Cashier** and `IsDemo = true`. Payment restrictions and view-only permissions use `user.IsDemo == true` (and for backward compatibility still treat `user.Role == "Demo"`). See **ai/DEMO_FLAG_MIGRATION.md** for full migration and code paths.
4. **Safe migration plan.** For a full step-by-step sequence, verification queries, optional SQL/EF migration, rollback, and admin warnings, see **ai/LEGACY_ROLE_MIGRATION_PLAN.md**.
5. **Optional data migration.** If you want to migrate in bulk (e.g. move all Kellner users to Waiter and then drop Kellner), use a separate EF/data migration or script that:
   - Updates `AspNetUserRoles` and `ApplicationUser.Role` from legacy name to target name.
   - Then deletes the legacy role row from `AspNetRoles` only when that role has no remaining user assignments.  
   Do **not** do this inside `RoleSeedData`; keep seed additive-only.

## Migration risks

- **Users left on legacy roles:** If you run a migration that deletes a legacy role row without reassigning users, Identity can leave orphaned or inconsistent state. Always reassign (or migrate) users first, then delete the role.
- **ApplicationUser.Role:** Must be kept in sync when reassigning (e.g. UserManagementController update user role). Any custom migration script should update both AspNetUserRoles and ApplicationUser.Role.
- **Demo role:** Code restricts “demo” behavior by `IsDemo` and, for backward compatibility, by `Role == "Demo"`. After moving all Demo-role users to a canonical role + IsDemo flag, you can remove the `Role == "Demo"` check in a later cleanup.

## Files reference

| File | Purpose |
|------|--------|
| `backend/Authorization/Roles.cs` | Canonical role names and `Roles.Canonical` list (source of truth for “system role”). |
| `backend/Data/RoleSeedData.cs` | Seeds only the eight canonical roles; never deletes. |
| `backend/Data/UserSeedData.cs` | Creates demo user with **Cashier** + `IsDemo = true` (no Demo role). |
| `backend/Services/PaymentService.cs` | Restricts demo users via `IsDemo` and `Role == "Demo"` (backward compat). |
| `backend/Services/UserService.cs` | HasPermissionAsync: demo users (IsDemo or Role=="Demo") get view-only permissions; "Demo" removed from role switch. |
| `ai/DEMO_FLAG_MIGRATION.md` | Demo role → IsDemo flag migration and code paths. |
| `ai/LEGACY_ROLE_MIGRATION_PLAN.md` | Safe step-by-step legacy role migration, verification, optional SQL/EF, rollback. |
