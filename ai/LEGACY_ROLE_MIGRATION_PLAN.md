# Legacy Role Migration Plan

**Goal:** Safe transition from legacy role names to the canonical role set without leaving users without roles, while keeping `AspNetUsers.role` and `AspNetUserRoles` consistent.

**Canonical set (source of truth):** `backend/Authorization/Roles.cs` → `Roles.Canonical`  
SuperAdmin, Manager, Cashier, Waiter, Kitchen, ReportViewer, Accountant

**Existing migrations already applied in repo:**
- `20260308140000_CanonicalizeLegacyRoleNames` — Administrator → Admin (AspNetUsers.role + AspNetUserRoles), then Administrator row removed from user-role links
- `20260309120000_DropAdministratorRole` — DELETE Administrator from AspNetRoles
- `20260310175350_MigrateAdminToSuperAdminAndDropAdminRole` — Admin → SuperAdmin, drop Admin role
- `20260310180000_RemoveDemoRoleUseIsDemoFlag` — Demo → Cashier + `is_demo = true`, drop Demo role

**Code map:** `backend/Auth/RoleLegacyMapping.cs` — extend when adding new legacy→canonical pairs.

---

## 1. Legacy roles detected (repo + typical DB drift)

| Legacy name | Evidence in repo | Typical origin |
|-------------|------------------|----------------|
| **Administrator** | Migrations above | Old Identity seed / admin alias |
| **Admin** | Migration MigrateAdminToSuperAdmin… | Intermediate step before SuperAdmin |
| **Demo** | Migration RemoveDemoRole…, `DemoUserHelper` | Demo-as-role (replaced by `IsDemo`) |
| **Kellner** | Not seeded; FE label for Waiter only | German-named role row if created manually |
| **BranchManager** | Comment-only in RoleSeedData | Custom / old product |
| **Auditor** | Comment-only | Custom; closest canonical is ReportViewer/Accountant |
| **Super-Administrator** | FE display only (`copy.ts`); not a role constant | Hyphenated duplicate if created as custom |
| **Waiter / Manager / …** | Canonical | No migration if name matches exactly |

**Discovery query (run before migration):**
```sql
-- All role names in Identity
SELECT "Id", "Name" FROM "AspNetRoles" ORDER BY "Name";

-- Users whose AspNetUsers.role is not in canonical set (adjust list as needed)
SELECT id, user_name, role FROM "AspNetUsers"
WHERE role IS NOT NULL AND role NOT IN (
  'SuperAdmin','Manager','Cashier','Waiter','Kitchen','ReportViewer','Accountant'
);

-- Users with AspNetUserRoles pointing to non-canonical role names
SELECT u.id, u.user_name, r."Name" AS role_name
FROM "AspNetUsers" u
JOIN "AspNetUserRoles" ur ON ur."UserId" = u.id
JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
WHERE r."Name" NOT IN ('SuperAdmin','Manager','Cashier','Waiter','Kitchen','ReportViewer','Accountant');
```

---

## 2. Canonical mapping proposal

| Legacy | Target | Notes |
|--------|--------|--------|
| Administrator | SuperAdmin | Already migrated via Admin chain |
| Admin | SuperAdmin | Done in 20260310175350 |
| Demo | Cashier + `IsDemo = true` | Done in 20260310180000 |
| **Kellner** | **Waiter** | Same operational scope; matrix has Waiter only |
| **Super-Administrator** | **SuperAdmin** | Normalize display vs Identity Name; if AspNetRoles row exists with hyphen, merge to SuperAdmin |
| **BranchManager** | **Manager** *or* **custom review** | If branch-scoped permissions existed, map to Manager first; if custom claims only, create temporary custom role then reassign — **belirsiz** |
| **Auditor** | **ReportViewer** *or* **Accountant** *or* **custom** | Report-only → ReportViewer; audit+invoice read → Accountant; otherwise preserve as custom until preset defined — **belirsiz** |

---

## 3. Uncertain mappings (manual / business decision)

- **BranchManager → Manager:** Safe default if no extra permissions; if BranchManager had custom claims, clone claims to a new custom role or map to Manager and accept permission change.
- **Auditor:** No matrix row; choose ReportViewer (narrower) vs Accountant (audit/invoice read) vs keep custom.
- **Any role not in LegacyToCanonicalRole:** Run discovery query; either add to map or leave as custom (not deleted).

---

## 4. Migration phases

### Phase 0 — Backup
- Full DB backup (pg_dump) or snapshot.
- Optional: export `AspNetRoles`, `AspNetUserRoles`, `AspNetUsers` (id, role) to CSV.

### Phase 1 — Seed canonical roles
- Ensure app startup runs `RoleSeedData.SeedRolesAsync` **before** any reassignment migration, or run migration that INSERTs missing canonical roles only if your process does not auto-seed.
- **Rule:** Target role row must exist before `INSERT INTO AspNetUserRoles`.

### Phase 2 — Reassign users (per legacy role)
For each legacy role `L` → canonical `C`:
1. `INSERT INTO AspNetUserRoles` — add `C` for every user who has `L` and does not already have `C` (same pattern as existing Demo/Admin migrations).
2. `UPDATE AspNetUsers SET role = 'C' WHERE role = 'L'`.
3. `DELETE FROM AspNetUserRoles` where `RoleId` = role id of `L`.
4. Optional: `UPDATE AspNetUsers SET is_demo = true` when migrating Demo-like roles to Cashier.

**Order:** Always add new role membership **before** removing old, so no user is left without a role between statements.

### Phase 3 — Verification
- Run checklist queries (section 5). Fix any row failing before delete.
- Smoke test: login as migrated users; JWT role claim should match `AspNetUsers.role` after re-login.

### Phase 4 — Deprecate / delete legacy role rows
- `DELETE FROM AspNetRoleClaims WHERE RoleId = …`
- `DELETE FROM AspNetRoles WHERE Name = 'L'`
- **Do not delete** canonical system roles.

### Phase 5 — Reserve names (optional)
- Add legacy names to `Roles.ReservedRoleNames` to block new custom roles with same name (already done for Admin, Administrator, Demo).

---

## 5. Verification checklist

Run after each batch migration.

| Check | Query / action |
|-------|----------------|
| **Users without role (column)** | `SELECT id, user_name FROM "AspNetUsers" WHERE role IS NULL OR trim(role) = '';` → must be 0 |
| **Users without any AspNetUserRoles** | `SELECT u.id FROM "AspNetUsers" u WHERE NOT EXISTS (SELECT 1 FROM "AspNetUserRoles" ur WHERE ur."UserId" = u.id);` → should be 0 for active app |
| **Mismatched role tables** | For each user, `AspNetUsers.role` should equal the single role in `AspNetUserRoles` if you enforce single-role model; if multi-role, document rule |
| **Users still on deprecated roles** | `SELECT r."Name", count(*) FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id" = ur."RoleId" GROUP BY r."Name";` — deprecated names must be 0 before delete |
| **Duplicate canonical equivalents** | Same user with both `L` and `C` after migration is OK until `L` removed; after cleanup, each user should not have duplicate rows for same logical role |

**Consistency rule:** After migration, for single-role policy:  
`AspNetUsers.role` = name of the role in `AspNetUserRoles` (or primary role if multi-role is ever allowed).

---

## 6. Migration pseudocode / template

Pattern matches `RemoveDemoRoleUseIsDemoFlag` / `MigrateAdminToSuperAdminAndDropAdminRole` (PostgreSQL).

```text
-- Prerequisites: canonical role TARGET exists in AspNetRoles.

-- 1) AspNetUserRoles: assign TARGET to each user on LEGACY who does not already have TARGET
INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
SELECT ur."UserId", (SELECT r."Id" FROM "AspNetRoles" r WHERE r."Name" = 'TARGET' LIMIT 1)
FROM "AspNetUserRoles" ur
INNER JOIN "AspNetRoles" r ON r."Id" = ur."RoleId" AND r."Name" = 'LEGACY'
WHERE EXISTS (SELECT 1 FROM "AspNetRoles" c WHERE c."Name" = 'TARGET')
  AND NOT EXISTS (
    SELECT 1 FROM "AspNetUserRoles" ur2
    INNER JOIN "AspNetRoles" r2 ON r2."Id" = ur2."RoleId" AND r2."Name" = 'TARGET'
    WHERE ur2."UserId" = ur."UserId"
);

-- 2) AspNetUsers.role column
UPDATE "AspNetUsers" SET role = 'TARGET' WHERE role = 'LEGACY';

-- 3) Optional: Demo-like → IsDemo
-- UPDATE "AspNetUsers" SET is_demo = true WHERE ... ;

-- 4) Remove LEGACY assignments
DELETE FROM "AspNetUserRoles"
WHERE "RoleId" IN (SELECT "Id" FROM "AspNetRoles" WHERE "Name" = 'LEGACY');

-- 5) Claims then role row
DELETE FROM "AspNetRoleClaims"
WHERE "RoleId" IN (SELECT "Id" FROM "AspNetRoles" WHERE "Name" = 'LEGACY');

DELETE FROM "AspNetRoles" WHERE "Name" = 'LEGACY';
```

**Example: Kellner → Waiter** — replace `LEGACY` = `Kellner`, `TARGET` = `Waiter`.  
**Example: Super-Administrator → SuperAdmin** — only if a role row with that exact name exists; otherwise normalize at token layer only.

---

## 7. Specific mapping evaluation (requested)

| Mapping | Recommendation |
|---------|----------------|
| **Kellner → Waiter** | **Approve.** Same POS surface; matrix and seed use Waiter only. |
| **BranchManager → Manager or custom review** | **Default Manager** if no custom claims; **custom review** if AspNetRoleClaims exist — export claims before merge. |
| **Auditor → report-read preset / custom** | **ReportViewer** if read-only reports; **Accountant** if audit log + invoice read; else **keep custom** and add preset later. |
| **Demo → operational role + IsDemo** | **Done:** Cashier + `is_demo = true` in 20260310180000. |
| **Super-Administrator → SuperAdmin** | **Approve** if Identity name is hyphenated duplicate; if only UI label, no DB change. |
| **Administrator → merge or deprecate** | **Already merged** into SuperAdmin via Admin migration chain; no separate Administrator row should remain. |

---

## 8. Backward compatibility

- **JWT:** Old tokens carry old role names until expiry; `RoleCanonicalization` handles Admin → SuperAdmin; extend for Kellner → Waiter if needed until re-login.
- **APIs filtering by `AspNetUsers.role`:** After UPDATE, list filters align; invalidate sessions if role change must take effect immediately.
- **Down:** Migrations are data-only down-unsupported by design; restore from backup to revert.

---

## 9. Long-term note

Target cleaner POS model (e.g. SuperAdmin, Operator, Backoffice, Kitchen) should be a **later** migration with new matrix rows and seed list change — not mixed into this legacy rename pass.
