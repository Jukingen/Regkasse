# Safe Migration Plan: Legacy Roles → Canonical Roles

**Last updated:** 2025-03-10

This document defines a **safe, step-by-step** approach to migrate users from legacy role names to the agreed canonical model. It assumes **reassignment before deletion** and includes verification so that **no user becomes role-less**.

---

## 1) Legacy role mapping

| Legacy role    | Target role   | Extra step (e.g. Demo)     |
|----------------|---------------|----------------------------|
| Administrator  | Admin         | — (may already be done by existing migration) |
| Kellner        | Waiter        | —                          |
| BranchManager  | Manager       | —                          |
| Auditor        | ReportViewer  | —                          |
| Demo           | Cashier       | Set `IsDemo = true`        |
| Unknown/typo   | —             | Manual reassignment, then delete role when empty |

---

## 2) Safe step-by-step migration sequence

Execute in this order. **Do not delete a role until all its users have been reassigned.**

### Phase A: Pre-migration (mandatory)

1. **Backup the database** (full backup or at least `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`).
2. **Ensure canonical roles exist.** Run the application once so that `RoleSeedData.SeedRolesAsync` has created Admin, Waiter, Manager, ReportViewer, Cashier, etc. Or run the verification query below and confirm all target roles exist in `AspNetRoles`.
3. **Run the pre-migration verification** (Section 5) and store the results. Resolve any users with **no role** or **unknown roles** before continuing.

### Phase B: Reassign users (no role row deleted yet)

4. For each legacy role in the table above (except Unknown):
   - **Reassign users** to the target role:
     - Update `AspNetUsers.role` to the target role name for all users where `role = '<LegacyRole>'`.
     - In `AspNetUserRoles`: add the target role for each user who had only the legacy role (avoid duplicate target assignment), then remove the legacy role assignment.
   - **Demo only:** For users with role `Demo`, also set `AspNetUsers.is_demo = true`.
5. **Do not delete** any row from `AspNetRoles` in this phase. Legacy role rows can remain; they will have zero users in `AspNetUserRoles` after reassignment.

### Phase C: Verify no user is role-less

6. **Run the post-reassignment verification** (Section 5). Every user must have:
   - `AspNetUsers.role` in the set of canonical role names (SuperAdmin, Admin, Manager, Cashier, Waiter, Kitchen, ReportViewer, Accountant).
   - At least one row in `AspNetUserRoles` linking them to that same role (role name in `AspNetRoles`).
7. If any user has a legacy role still in `AspNetUsers.role` or has no role assignment, fix before proceeding.

### Phase D: Delete legacy role rows (optional)

8. Only after **all** users have been reassigned and verification passes:
   - Delete from `AspNetUserRoles` where `RoleId` is the legacy role (should be 0 rows if reassignment was done correctly).
   - Delete from `AspNetRoles` where `Name` in ('Administrator','Kellner','BranchManager','Auditor','Demo') (and any other legacy names you migrated).
9. Run the verification again to confirm no broken references.

### Phase E: Unknown / typo roles

10. For any role **not** in the canonical list and **not** in the legacy mapping table:
    - List users in that role (query below).
    - **Manually reassign** each user to a valid canonical role via Admin UI or API (or bulk SQL if you have a clear mapping).
    - After the role has zero users, delete the role via API (DELETE role when `userCount = 0`) or via SQL.

---

## 3) Optional: EF Core data migration

The repository already has a pattern: `CanonicalizeLegacyRoleNames` (Administrator → Admin) and `DropAdministratorRole`. You can add a **single new migration** that performs the remaining legacy role reassignments in one go, **without** deleting role rows (deletion can be a separate migration or manual step).

**Order of operations in the migration Up():**

1. **Kellner → Waiter**  
   - Update `AspNetUsers` set `role = 'Waiter'` where `role = 'Kellner'`.  
   - Insert into `AspNetUserRoles` (UserId, RoleId) for users who had Kellner but do not already have Waiter (use subquery with NOT EXISTS on Waiter).  
   - Delete from `AspNetUserRoles` where RoleId = Kellner.

2. **BranchManager → Manager**  
   - Same pattern: update `AspNetUsers`, insert Waiter role for users missing it, delete BranchManager from `AspNetUserRoles`.

3. **Auditor → ReportViewer**  
   - Same pattern with ReportViewer.

4. **Demo → Cashier + IsDemo**  
   - Update `AspNetUsers` set `role = 'Cashier'`, `is_demo = true` where `role = 'Demo'`.  
   - Insert into `AspNetUserRoles` for users who had Demo but do not already have Cashier; then delete Demo from `AspNetUserRoles`.

5. **Do not** delete from `AspNetRoles` in this migration; do that in a **separate** migration or manually after verification.

**Down():** Not supported for data migrations. Rollback = restore from backup.

Example migration file name: `YYYYMMDDHHMMSS_MigrateLegacyRolesToCanonical.cs`. See existing `20260308140000_CanonicalizeLegacyRoleNames.cs` for SQL style (PostgreSQL quoted identifiers: `"AspNetUsers"`, `"AspNetRoles"`, `"AspNetUserRoles"`, column `role`).

---

## 4) Rollback considerations

- **No Down() for data:** Reassigning and deleting role rows is a one-way data change. The migration `Down()` should be left empty or only document that rollback is not supported.
- **Rollback = restore from backup.** Before Phase B, take a full backup (or at least the three Identity tables). To roll back, restore that backup.
- **Partial rollback:** If you have run only part of the steps (e.g. only Kellner → Waiter), you can manually reverse that step by:
  - Re-inserting the legacy role into `AspNetRoles` if it was deleted,
  - Re-assigning affected users back to the legacy role in both `AspNetUsers.role` and `AspNetUserRoles`,
  - Setting `IsDemo = false` for Demo users if you had set it to true.
- **Recommendation:** Prefer doing one legacy role at a time (or one migration per role) in staging, verify, then run in production; or run the full migration in a maintenance window with backup and verification.

---

## 5) Admin / operator warnings

- **Do not run destructive deletes** (e.g. `DELETE FROM AspNetRoles WHERE Name = 'Demo'`) **before** every user in that role has been reassigned. Otherwise users can be left with no valid role or orphaned `AspNetUserRoles` references.
- **ApplicationUser.Role must stay in sync** with `AspNetUserRoles`. Any script or migration that changes a user’s role must update **both** `AspNetUsers.role` and the corresponding row(s) in `AspNetUserRoles`.
- **Demo:** Setting `IsDemo = true` is required when moving from Demo role to Cashier so that payment restrictions and view-only permissions still apply.
- **Run verification before and after.** Keep the pre-migration output; after reassignment, run the verification again and confirm zero users with legacy/empty role and zero orphaned assignments.
- **Idempotency:** If you run the same reassignment SQL twice (e.g. migration run twice), design it so that the second run does not create duplicate `AspNetUserRoles` rows (use `NOT EXISTS` / “only if not already in target role”) and does not change users already in the target role.

---

## 6) Verification: no user becomes role-less

Use these checks **before** (to see current state) and **after** (to ensure no user is left without a valid role).

### 6.1) Canonical role names (must exist in DB)

```sql
-- All 8 canonical roles should exist in AspNetRoles.
SELECT "Name" FROM "AspNetRoles" WHERE "Name" IN (
  'SuperAdmin', 'Admin', 'Manager', 'Cashier', 'Waiter', 'Kitchen', 'ReportViewer', 'Accountant'
);
-- Expect 8 rows (or more if you have custom roles too).
```

### 6.2) Users whose ApplicationUser.Role is not canonical

```sql
-- Users with legacy or unknown role in AspNetUsers.role.
SELECT u."Id", u."UserName", u.role
FROM "AspNetUsers" u
WHERE u.role IS NULL OR u.role = ''
   OR u.role NOT IN (
     'SuperAdmin', 'Admin', 'Manager', 'Cashier', 'Waiter', 'Kitchen', 'ReportViewer', 'Accountant'
   );
-- Before migration: may show Administrator, Kellner, BranchManager, Auditor, Demo, or typos.
-- After migration: expect 0 rows.
```

### 6.3) Users with no AspNetUserRoles assignment

```sql
-- Users who have no row in AspNetUserRoles (role-less).
SELECT u."Id", u."UserName", u.role
FROM "AspNetUsers" u
LEFT JOIN "AspNetUserRoles" ur ON ur."UserId" = u."Id"
WHERE ur."UserId" IS NULL;
-- Must be 0 rows at all times.
```

### 6.4) Users whose role column does not match their AspNetUserRoles role

```sql
-- AspNetUsers.role should match the role name in AspNetUserRoles/AspNetRoles.
SELECT u."Id", u."UserName", u.role AS user_role, r."Name" AS aspnet_role_name
FROM "AspNetUsers" u
INNER JOIN "AspNetUserRoles" ur ON ur."UserId" = u."Id"
INNER JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
WHERE u.role IS DISTINCT FROM r."Name";
-- After migration: expect 0 rows (sync between denormalized role and Identity).
```

### 6.5) Count of users per role (before/after snapshot)

```sql
SELECT u.role, COUNT(*) AS user_count
FROM "AspNetUsers" u
GROUP BY u.role
ORDER BY u.role;
-- Before: may include Administrator, Kellner, BranchManager, Auditor, Demo.
-- After: only canonical names (and any custom roles you keep).
```

---

## 7) Verification checklist

Use this as a sign-off list.

- [ ] Database backup taken (AspNetUsers, AspNetRoles, AspNetUserRoles).
- [ ] Pre-migration verification (6.1–6.5) run and results saved.
- [ ] All canonical roles exist in AspNetRoles (6.1).
- [ ] Reassignment executed (Phase B) for each legacy role; no role row deleted before reassignment.
- [ ] For Demo: `is_demo = true` set for migrated users.
- [ ] Post-migration verification run: (6.2) returns 0 rows (no legacy/empty role); (6.3) returns 0 rows (no role-less user); (6.4) returns 0 rows (sync).
- [ ] Optional: legacy role rows deleted from AspNetRoles only after verification.
- [ ] Unknown/typo roles: users reassigned manually; role deleted only when userCount = 0.

---

## 8) Optional SQL script (manual run)

If you prefer **not** to add an EF migration and will run SQL manually (after backup and verification), you can use the following as a template. Run in a transaction and verify after each block; roll back if anything is wrong.

**PostgreSQL.** Ensure canonical roles exist (seed has run). Column names: `role`, `is_demo` on `AspNetUsers` (if your schema uses quoted snake_case, adjust to `"is_demo"` etc.).

```sql
BEGIN;

-- 1) Kellner → Waiter
UPDATE "AspNetUsers" SET role = 'Waiter' WHERE role = 'Kellner';
INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
SELECT ur."UserId", (SELECT r."Id" FROM "AspNetRoles" r WHERE r."Name" = 'Waiter' LIMIT 1)
FROM "AspNetUserRoles" ur
INNER JOIN "AspNetRoles" r ON r."Id" = ur."RoleId" AND r."Name" = 'Kellner'
WHERE NOT EXISTS (
  SELECT 1 FROM "AspNetUserRoles" ur2
  INNER JOIN "AspNetRoles" r2 ON r2."Id" = ur2."RoleId" AND r2."Name" = 'Waiter'
  WHERE ur2."UserId" = ur."UserId"
);
DELETE FROM "AspNetUserRoles"
WHERE "RoleId" = (SELECT "Id" FROM "AspNetRoles" WHERE "Name" = 'Kellner');

-- 2) BranchManager → Manager
UPDATE "AspNetUsers" SET role = 'Manager' WHERE role = 'BranchManager';
INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
SELECT ur."UserId", (SELECT r."Id" FROM "AspNetRoles" r WHERE r."Name" = 'Manager' LIMIT 1)
FROM "AspNetUserRoles" ur
INNER JOIN "AspNetRoles" r ON r."Id" = ur."RoleId" AND r."Name" = 'BranchManager'
WHERE NOT EXISTS (
  SELECT 1 FROM "AspNetUserRoles" ur2
  INNER JOIN "AspNetRoles" r2 ON r2."Id" = ur2."RoleId" AND r2."Name" = 'Manager'
  WHERE ur2."UserId" = ur."UserId"
);
DELETE FROM "AspNetUserRoles"
WHERE "RoleId" = (SELECT "Id" FROM "AspNetRoles" WHERE "Name" = 'BranchManager');

-- 3) Auditor → ReportViewer
UPDATE "AspNetUsers" SET role = 'ReportViewer' WHERE role = 'Auditor';
INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
SELECT ur."UserId", (SELECT r."Id" FROM "AspNetRoles" r WHERE r."Name" = 'ReportViewer' LIMIT 1)
FROM "AspNetUserRoles" ur
INNER JOIN "AspNetRoles" r ON r."Id" = ur."RoleId" AND r."Name" = 'Auditor'
WHERE NOT EXISTS (
  SELECT 1 FROM "AspNetUserRoles" ur2
  INNER JOIN "AspNetRoles" r2 ON r2."Id" = ur2."RoleId" AND r2."Name" = 'ReportViewer'
  WHERE ur2."UserId" = ur."UserId"
);
DELETE FROM "AspNetUserRoles"
WHERE "RoleId" = (SELECT "Id" FROM "AspNetRoles" WHERE "Name" = 'Auditor');

-- 4) Demo → Cashier + IsDemo
UPDATE "AspNetUsers" SET role = 'Cashier', is_demo = true WHERE role = 'Demo';
INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
SELECT ur."UserId", (SELECT r."Id" FROM "AspNetRoles" r WHERE r."Name" = 'Cashier' LIMIT 1)
FROM "AspNetUserRoles" ur
INNER JOIN "AspNetRoles" r ON r."Id" = ur."RoleId" AND r."Name" = 'Demo'
WHERE NOT EXISTS (
  SELECT 1 FROM "AspNetUserRoles" ur2
  INNER JOIN "AspNetRoles" r2 ON r2."Id" = ur2."RoleId" AND r2."Name" = 'Cashier'
  WHERE ur2."UserId" = ur."UserId"
);
DELETE FROM "AspNetUserRoles"
WHERE "RoleId" = (SELECT "Id" FROM "AspNetRoles" WHERE "Name" = 'Demo');

-- Run verification queries (6.2–6.4); expect 0 rows. Then:
COMMIT;
-- To drop legacy role rows (optional, after verification):
-- DELETE FROM "AspNetRoles" WHERE "Name" IN ('Kellner','BranchManager','Auditor','Demo');
```

If your schema uses **snake_case** for columns (e.g. `is_demo`), keep as above. If the column is `"IsDemo"` (PascalCase), use `"IsDemo" = true` in the UPDATE.

---

## 9) References

- **Existing migrations:** `backend/Migrations/20260308140000_CanonicalizeLegacyRoleNames.cs`, `20260309120000_DropAdministratorRole.cs`
- **Seed:** `backend/Data/RoleSeedData.cs` (only creates canonical roles; does not delete)
- **Legacy handling:** `ai/ROLE_SEED_AND_LEGACY_MIGRATION_NOTES.md`
- **Demo flag:** `ai/DEMO_FLAG_MIGRATION.md`
