# Role seed & migration notes (ops)

## Seed (runtime)

- **File:** `backend/Data/RoleSeedData.cs`
- **Behavior:** Creates missing `Roles.Canonical` only; does **not** delete legacy rows.
- **Order:** Run before any migration that inserts `AspNetUserRoles` for a target role (Cashier, SuperAdmin, …).

## Migrations (already in repo)

| Migration | Purpose |
|-----------|---------|
| `20260308140000_CanonicalizeLegacyRoleNames` | Administrator → Admin chain |
| `20260309120000_DropAdministratorRole` | Remove Administrator row |
| `20260310175350_MigrateAdminToSuperAdminAndDropAdminRole` | Admin → SuperAdmin |
| `20260310180000_RemoveDemoRoleUseIsDemoFlag` | Demo role → Cashier + `is_demo`; drop Demo |

## Apply order

1. Deploy backend with seed on startup (or ensure canonical roles exist).
2. `dotnet ef database update` (all pending).

## Demo role

- Demo must not exist in `AspNetRoles` after `RemoveDemoRoleUseIsDemoFlag`.
- Client uses `ApplicationUser.IsDemo` only; `DemoUserHelper` + `PermissionHelper.setIsDemoUser` for UI restrictions.

## IsDemo vs role — drift (Cashier görünür, payment demo reddedilir)

- **Migration `RemoveDemoRoleUseIsDemoFlag`** sets `role = 'Cashier'` **and** `is_demo = true` for former Demo users — intentional: they stay restricted until explicitly promoted.
- **User update paths** (`UserManagementController.UpdateUser`, `AdminUsersController.Patch`) previously **never** wrote `IsDemo`; changing role alone left `is_demo` unchanged → drift if admin expects “real” cashier.
- **UserSeedData** creates `demo@demo.com` as `Role = Cashier` + `IsDemo = true` by design.
- **Residual risk:** Any user with `is_demo = true` and `role = 'Cashier'` is still blocked in `PaymentService` via `DemoUserHelper.IsDemoUser` (flag wins).
- **Fix:** Explicit API field `isDemo` on PUT/Patch (optional); set `false` to clear drift. Frontend can add a switch later; until then use API or SQL.

## Manager (and other non-admin canonical roles) — why still system

- **System** = name is in `Roles.Canonical`. That list is what `RoleManagementService.IsSystemRole` uses; there is no per-role override.
- **Manager** remains in `Canonical` in this PR **on purpose**: backward compatibility and avoid a breaking migration (existing users, matrix row, seed) in the same change set as governance hardening.
- **This PR does not** implement the long-term minimized taxonomy (e.g. Operator / Backoffice / Kitchen). Shrinking `Canonical` or reclassifying Manager as custom belongs to a **separate follow-up** with migration + matrix + FE policy updates.
