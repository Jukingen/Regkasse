# Role Cleanup and Migration Proposal

**Date:** 2025-03-10  
**Goal:** Map current roles to the simplified POS role model; safe migration sequence; no user left without a valid role; no delete when users are assigned.

**Context:** `ROLE_TAXONOMY_ANALYSIS.md`, `ROLE_AUTHORIZATION_DESIGN.md`, backend `Roles.Canonical` and `RoleSeedData`.

---

## 1) Current-to-target role mapping table

| Current role (DB / seed) | Action | Target role / outcome | Notes |
|--------------------------|--------|------------------------|-------|
| **SuperAdmin** | **Keep** | SuperAdmin | System role; no change. |
| **Admin** | **Keep** | Admin | System role; no change. |
| **Administrator** | **Already migrated** | Admin | Handled by `CanonicalizeLegacyRoleNames` + `DropAdministratorRole`; no action. |
| **Manager** | **Keep** | Manager | System role; no change. |
| **BranchManager** | **Merge** | Manager | Same function (branch/floor management). Reassign all BranchManager users to Manager, then remove BranchManager from seed and delete role row if 0 users. |
| **Cashier** | **Keep** | Cashier | System role; no change. |
| **Waiter** | **Keep** | Waiter | System role; no change. |
| **Kellner** | **Merge** | Waiter | German for Waiter; duplicate. Reassign all Kellner users to Waiter, then stop seeding Kellner and delete role if 0 users. |
| **Kitchen** | **Keep** | Kitchen | System role (optional); no change. |
| **ReportViewer** | **Keep** | ReportViewer | System role; no change. |
| **Accountant** | **Keep** | Accountant | System role; no change. |
| **Auditor** | **Merge** | ReportViewer (or Accountant) | Read-only audit; map to ReportViewer (or Accountant if FinanzOnline needed). Reassign users, then remove from seed and delete role if 0 users. |
| **Demo** | **Delete (after reassignment)** | — | Not a system role. Reassign any Demo users to Cashier (or set IsDemo = true). Remove Demo from seed; remove PaymentService checks for `Role == "Demo"` in favour of IsDemo; then delete Demo role when 0 users. |
| **User** | **Do not assign** | — | Fallback only (e.g. token); not in Canonical. Never migrate to "User"; migrate to a real role (e.g. Cashier) if present in DB. |
| **Unknown / typo (e.g. "nedone")** | **Reassign + delete** | — | If such a role exists in AspNetRoles: reassign all users to a valid system or custom role, then delete the role. Do not add to seed or Canonical. |

---

## 2) Suggested normalized naming convention

- **System roles:** PascalCase, English, single word or two words without space (e.g. `SuperAdmin`, `ReportViewer`). Defined in `Roles.Canonical` and `RolePermissionMatrix` only.
- **Display names:** Localized in UI (e.g. German "Kassierer", "Berichte (nur Lesen)") via frontend `roleDisplayName` / `ROLE_DISPLAY_NAMES`; DB and API keep the canonical English identifier.
- **Custom roles:** Any name allowed by API (unique, not reserved). Prefer PascalCase for consistency; avoid names that match system roles (reserved).
- **Do not use:** "Administrator" (use Admin), "Kellner" (use Waiter), "BranchManager" (use Manager), "Auditor" (use ReportViewer or Accountant), "Demo" as a role name (use IsDemo flag or environment).

---

## 3) Roles that remain system roles

These stay in code and seed (where not yet seeded); they are **protected** (not deletable, permissions not editable via API):

| Role | In Roles.Canonical | In RoleSeedData (after cleanup) | Purpose |
|------|--------------------|---------------------------------|---------|
| SuperAdmin | ✓ | ✓ | Full system; TSE, audit cleanup, user/role management. |
| Admin | ✓ | ✓ | Backoffice; all except SuperAdmin-only. |
| Manager | ✓ | ✓ | POS + backoffice; no user management. |
| Cashier | ✓ | ✓ | Till; sales, payment, shift, receipt. |
| Waiter | ✓ | ✓ | Service; orders, tables, limited payment. |
| Kitchen | ✓ | ✓ (add if missing) | Kitchen display; order view/update. |
| ReportViewer | ✓ | ✓ (add if missing) | Read-only reports and audit. |
| Accountant | ✓ | ✓ (add if missing) | Reports, audit, FinanzOnline view. |

**Total: 8 system roles.** No Kellner, BranchManager, Auditor, Demo in system set.

---

## 4) Roles that become custom or are removed

| Current | Becomes | Reason |
|---------|---------|--------|
| Kellner | **Removed** (users → Waiter) | Duplicate of Waiter; merge into Waiter. |
| BranchManager | **Removed** (users → Manager) | Overlaps Manager; merge into Manager. |
| Auditor | **Removed** (users → ReportViewer or Accountant) | Overlaps ReportViewer/Accountant. |
| Demo | **Removed** (users → Cashier + IsDemo or Cashier only) | Demo behaviour via IsDemo flag or environment; not a separate role. |
| Any other non-canonical role in DB | **Custom** (if kept) or **Removed** (if reassigned + deleted) | Only canonical roles are system; rest are custom or legacy to clean. |

After migration, **custom roles** are only those created via POST /api/UserManagement/roles (or any role in AspNetRoles not in Canonical). They remain editable and deletable when userCount = 0.

---

## 5) Safe migration sequence

**Principle:** Never delete a role that has assigned users. Always reassign users first, then delete.

| Phase | Step | Action | Safe? |
|-------|------|--------|--------|
| **0** | Backup | Full DB backup; document current user–role assignments. | Required. |
| **1** | Report | Query: list roles with userCount; list users per legacy role (Kellner, BranchManager, Auditor, Demo). | Read-only. |
| **2** | Kellner → Waiter | For each user in Kellner: set role to Waiter (AspNetUserRoles + ApplicationUser.Role). Then delete Kellner role row (only if 0 users). | Yes if reassignment succeeds. |
| **3** | BranchManager → Manager | Same: reassign all BranchManager users to Manager; then delete BranchManager role. | Yes if reassignment succeeds. |
| **4** | Auditor → ReportViewer (or Accountant) | Reassign Auditor users to ReportViewer (or Accountant); then delete Auditor role. | Yes if reassignment succeeds. |
| **5** | Demo → Cashier (+ IsDemo) | For each user in Demo: set role to Cashier; set IsDemo = true if they are demo accounts. Then remove PaymentService checks for `Role == "Demo"` (use IsDemo only, or keep both during transition). Then delete Demo role when 0 users. | Yes after code change and reassignment. |
| **6** | Seed cleanup | Update RoleSeedData: remove creation of Kellner, BranchManager, Auditor, Demo. Ensure Kitchen, ReportViewer, Accountant are seeded if not already. | Safe; affects only new environments. |
| **7** | Optional: unknown roles | For any role not in Canonical and not a known custom: reassign users to a chosen target (e.g. Cashier or Manager), then delete role. | Safe if reassignment is correct. |

**Order:** Do 2 → 3 → 4 → 5 in separate migrations or scripts so each step can be verified (userCount = 0 before delete).

---

## 6) Risks when users are already assigned

| Risk | Mitigation |
|------|------------|
| Users in Kellner lose access if role is deleted without reassignment | **Never** delete Kellner until every Kellner user has been moved to Waiter (and AspNetUserRoles + ApplicationUser.Role updated). |
| Same for BranchManager, Auditor, Demo | Same rule: reassign first; verify userCount = 0; then delete. |
| Permission change | Kellner has no matrix; Waiter has full Waiter permissions—so reassignment to Waiter is an **increase** in permissions. BranchManager has no matrix; Manager has Manager permissions—again an increase. Auditor → ReportViewer/Accountant: choose ReportViewer or Accountant and document. Demo → Cashier: ensure IsDemo is set where needed so PaymentService still restricts real payments if required. |
| Concurrent updates | Run migrations in maintenance window or use transactions; avoid assigning users to a role that is about to be deleted. |
| Rollback | Keep backup; if a migration step fails, restore and fix script. No “down” migration that removes user assignments. |

**Rule:** If a role has userCount > 0, the only safe operation is **reassign users to another role**. Deletion is safe only when userCount = 0.

---

## 7) Suggested admin warnings and messages (migration UI or scripts)

Use these (or equivalent) in migration scripts, admin UI, or runbooks:

- **Before any legacy role cleanup:**  
  *"Legacy roles (Kellner, BranchManager, Auditor, Demo) will be merged or removed. All affected users will be reassigned to a target role. Ensure you have a backup and have reviewed the user list per role."*

- **When listing users for a legacy role:**  
  *"X user(s) have role [RoleName]. They will be reassigned to [TargetRole]. Proceed only after backup."*

- **Reassignment confirmation:**  
  *"Reassign all N user(s) from [LegacyRole] to [TargetRole]? This will update their role and cannot be undone without restore."*

- **Before deleting a role:**  
  *"Role [RoleName] has 0 users. It is safe to delete. Delete role from database?"*

- **If delete attempted with userCount > 0:**  
  *"This role cannot be deleted because N user(s) are still assigned. Reassign those users to another role first."*  
  (Already implemented in API 409 + UI.)

- **After Demo reassignment (code change):**  
  *"Demo users are now identified by the IsDemo flag. The 'Demo' role has been removed. Ensure demo accounts have IsDemo = true where appropriate."*

- **Unknown/typo role:**  
  *"Role '[Name]' is not a system role. Reassign its users to a valid role (e.g. Cashier or Manager), then delete the role manually or via cleanup script."*

---

## Summary table (quick reference)

| Legacy role   | Target        | Safe migration                          |
|---------------|---------------|-----------------------------------------|
| Kellner       | Waiter        | Reassign all → Waiter; then delete.     |
| BranchManager | Manager       | Reassign all → Manager; then delete.    |
| Auditor       | ReportViewer  | Reassign all → ReportViewer; then delete. |
| Demo          | Cashier+IsDemo| Reassign all → Cashier; set IsDemo; remove role; then delete Demo. |
| Administrator | Admin         | Already done (migrations).               |
| Unknown       | e.g. Cashier  | Reassign; then delete.                   |

**System roles to keep (8):** SuperAdmin, Admin, Manager, Cashier, Waiter, Kitchen, ReportViewer, Accountant.  
**Normalized naming:** PascalCase English in code/API; display names localized in UI.  
**Delete only when userCount = 0;** always reassign first and document.
