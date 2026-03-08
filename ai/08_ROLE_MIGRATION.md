# Role migration: legacy alias → canonical (Phase 7)

## Summary

- **Canonical role:** `Admin`
- **Legacy alias:** `Administrator` (treated as Admin in policy and JWT; DB/token may still contain the old value until migration runs).

Temporary alias handling ensures existing DB/tokens with `Administrator` continue to work. After the migration milestone, the alias can be removed.

---

## 1) Temporary alias handling (current)

- **Backend:** `Auth/RoleCanonicalization.cs` maps `Administrator` → `Admin`.
- **JWT:** Login emits canonical role in the token (`GetCanonicalRole(primaryRole)`).
- **Policies:** `Program.cs` still lists both `"Admin"` and `"Administrator"` so existing tokens work until re-login.
- **Reset-password rule:** UserManagementController uses canonical role for “only SuperAdmin can reset SuperAdmin” (so `Administrator` is never treated as SuperAdmin).

---

## 2) One-time migration to canonicalize DB

**Option A – EF migration (recommended)**

```bash
cd backend
dotnet ef database update
```

This applies `20260308140000_CanonicalizeLegacyRoleNames` (updates `AspNetUsers.role` and `AspNetUserRoles`).

**Option B – Manual SQL**

Run the script once (e.g. after backup):

```bash
psql -d YourDatabase -f backend/Scripts/CanonicalizeLegacyRoleNames.sql
```

---

## 3) Remove alias after migration milestone (documented)

**When:** After the migration has been applied in all environments and you have verified no user has `Administrator` in DB or in active tokens (e.g. after token expiry or forced re-login).

**Steps:**

1. **Policies** – In `Program.cs`, remove `"Administrator"` from all `RequireRole(...)` lists so only `"Admin"` remains:
   - `AdminUsers`: `"SuperAdmin", "Admin"`
   - `UsersView`: `"SuperAdmin", "Admin", "BranchManager", "Auditor"`
   - `UsersManage`: `"SuperAdmin", "Admin", "BranchManager"`

2. **RoleCanonicalization** – In `Auth/RoleCanonicalization.cs`, remove the entry `{ "Administrator", Canonical.Admin }` from `LegacyToCanonical` (or delete the dictionary and have `GetCanonicalRole` return the input as-is for non-legacy roles).

3. **RoleSeedData** – In `Data/RoleSeedData.cs`, stop creating the `"Administrator"` role (keep only `"Admin"`). Optional: add a one-off step to delete the `Administrator` role from `AspNetRoles` if desired.

4. **Tests** – Update `UserManagementAuthorizationPolicyTests.cs`: remove or adjust tests that assert `Administrator` is allowed (e.g. keep only `Admin` in policy tests).

5. **Frontend** – In `frontend-admin/src/features/auth/constants/roles.ts`, remove `'Administrator'` from `ROLES_CAN_VIEW_USERS`, `ROLES_CAN_MANAGE_USERS`, and `ROLES_RKSV_MENU` if present.

6. **Deploy** – Deploy backend first, then frontend; ensure all clients re-login so new tokens only contain `Admin`.

---

## Rollback

- **Migration Down:** The data migration has no safe automatic Down (original Administrator assignments are not stored). To revert, restore from backup or re-run manual SQL to set `role = 'Administrator'` for affected users and re-assign the Administrator Identity role.
- **Alias removal:** To re-enable the alias, re-add `"Administrator"` to policies and to `RoleCanonicalization.LegacyToCanonical`.
