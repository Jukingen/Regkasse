# Role Taxonomy Analysis – POS Role Model

**Date:** 2025-03-10  
**Scope:** Admin panel + backend roles; goal: simplified, business-sensible role taxonomy for a standard POS.

---

## 1) Current Role Inventory

### Backend (source of truth)

| Source | Roles |
|--------|--------|
| **Roles.cs (Canonical)** | SuperAdmin, Admin, Manager, Cashier, Waiter, Kitchen, ReportViewer, Accountant |
| **RoleSeedData.cs** (seeded into AspNetRoles) | Admin, Cashier, **Kellner**, Waiter, **Auditor**, **Demo**, Manager, **BranchManager**, SuperAdmin |
| **RolePermissionMatrix** | Only the 8 canonical roles have a permission set. |

**Additional:**  
- `Roles.FallbackUnknown = "User"` – not a real role; used when no role is present (e.g. token).  
- **Administrator** – removed by migration `20260309120000_DropAdministratorRole`; users migrated to Admin.

### Frontend Admin

- **roles.ts:** Same 8 canonical roles; helpers for canViewUsers, canManageUsers, canCreateRole, canDeleteRole, canEditRolePermissions, RKSV.
- **users/page.tsx:** `ROLE_OPTIONS` hardcoded to the 8 canonical roles; `roleOptions` = `roles?.map(...) ?? ROLE_OPTIONS` where `roles` comes from `GET /api/UserManagement/roles`.
- **GET /api/UserManagement/roles** returns **all** AspNetRoles (all seeded names). So after seed, the dropdown can show up to 12 roles (8 canonical + Kellner, Auditor, Demo, BranchManager) if the API is used.

### Summary table

| Role | In Canonical | In Seed | In Matrix | Shown in UI (if API used) | Purpose |
|------|--------------|---------|-----------|---------------------------|---------|
| SuperAdmin | ✓ | ✓ | ✓ | ✓ | Full system; TSE/diagnostics, audit cleanup |
| Admin | ✓ | ✓ | ✓ | ✓ | Backoffice; all except SuperAdmin-only |
| Manager | ✓ | ✓ | ✓ | ✓ | POS + backoffice; no user manage |
| Cashier | ✓ | ✓ | ✓ | ✓ | POS sales, shift, no report/invoice manage |
| Waiter | ✓ | ✓ | ✓ | ✓ | Table/cart/order, limited payment |
| Kitchen | ✓ | — | ✓ | ✓ | Kitchen display, order update |
| ReportViewer | ✓ | — | ✓ | ✓ | Reports, audit view, settings view |
| Accountant | ✓ | — | ✓ | ✓ | Report, audit, FinanzOnline view |
| Kellner | — | ✓ | — | ✓ | Legacy; same as Waiter (DE name) |
| Auditor | — | ✓ | — | ✓ | Read-only audit; overlaps ReportViewer/Accountant |
| Demo | — | ✓ | — | ✓ | Used in PaymentService to block real payments; no matrix |
| BranchManager | — | ✓ | — | ✓ | No matrix; overlaps Manager |

---

## 2) Problematic / Overlapping Roles

1. **Kellner vs Waiter**  
   Same function (service staff). Kellner is German for Waiter. RoleSeedData comments say "legacy; policies use Waiter". No permission matrix for Kellner → anyone with only Kellner has no permissions unless custom claims were added. **Recommendation:** Treat as duplicate; migrate Kellner → Waiter and stop seeding Kellner.

2. **BranchManager vs Manager**  
   BranchManager is seeded but has no permission set. Semantically it could be “manager of one branch” vs “Manager” (general). In many POS systems one “Manager” role is enough; branch can be a separate dimension (e.g. scope/location). **Recommendation:** Do not treat as separate system role; merge into Manager. If branch-scoping is needed later, use scope/location, not a second manager role.

3. **Auditor vs ReportViewer / Accountant**  
   Auditor is read-only audit; ReportViewer has report + audit view; Accountant has report, audit, FinanzOnline. Auditor is redundant with a subset of ReportViewer/Accountant. **Recommendation:** Drop Auditor; use ReportViewer or Accountant with appropriate permissions.

4. **Demo as a role**  
   - PaymentService restricts **role name** `"Demo"` (no real payment/cancel/refund).  
   - Seeded demo user is **Cashier + IsDemo = true**, not role "Demo".  
   So: “Demo” role exists in DB and is blocked in payment flows, but the only seeded demo user does not use it. That is inconsistent.  
   - User rules: “Demo users cannot create real receipts; test environment only.”  
   **Recommendation:** Do not keep “Demo” as a system role. Treat demo as a **user flag** (IsDemo) or a **session/tenant** concept. If you need a dedicated demo role, define it explicitly (e.g. “DemoCashier” with fixed permission set and document it); otherwise enforce “no real fiscal operations” via IsDemo only and remove role "Demo".

5. **ReportViewer vs Accountant**  
   ReportViewer: report + audit view + settings view. Accountant: report + audit + FinanzOnline view (no settings). Slight overlap; both are read-heavy. Acceptable to keep both for different job functions (e.g. branch staff vs head office).

6. **User without role**  
   Create user allows `request.Role` to be empty; only `if (!string.IsNullOrEmpty(request.Role))` adds the role. So a user can be created with no Identity role and empty `ApplicationUser.Role`. **Problem:** “Users must never end up without a valid role” is not enforced. **Recommendation:** Require role on create and on update (or enforce a default, e.g. Cashier), and validate that the role exists and is assignable.

---

## 3) Recommended POS Default Roles (Lean Set)

| Role | Purpose | System role |
|------|--------|-------------|
| **SuperAdmin** | Full system; TSE, audit cleanup, user/role management | ✓ Protected |
| **Admin** | Backoffice; business config, users, no SuperAdmin-only actions | ✓ Protected |
| **Manager** | Branch/floor; POS + products/categories/tables; no user management | ✓ Protected |
| **Cashier** | Till; sales, payment, shift, receipt; no report/invoice manage | ✓ Protected |
| **Waiter** | Service; orders, tables, limited payment; no cash register manage | ✓ Protected |
| **Kitchen** | Kitchen display; order view/update only | Optional |
| **ReportViewer** | Read-only reports and audit | Optional |
| **Accountant** | Reports, audit, FinanzOnline view | Optional |

**Total: 5 core + 3 optional = 8.**  
No Kellner, Auditor, Demo, BranchManager as system roles. Demo behavior via **IsDemo** (or explicit “Demo” environment), not a role.

---

## 4) System Roles vs Custom Roles

| Concept | Definition | Lifecycle |
|--------|------------|-----------|
| **System roles** | Defined in code (`Roles.Canonical`); permission set in `RolePermissionMatrix`; seeded once. | Not deletable; permissions not editable via UI; name/behavior changed only in code. |
| **Custom roles** | Created via API (POST roles); permissions stored in AspNetRoleClaims. | Create, edit permissions, delete (if no users). |

**Recommendation:**  
- **System roles:** SuperAdmin, Admin, Manager, Cashier, Waiter, Kitchen, ReportViewer, Accountant. Mark in API (e.g. `isSystemRole: true`) and in admin UI (read-only, no delete).  
- **Custom roles:** Any role created through “Create role” and not in the system list. Editable permissions; deletable only when no users are assigned.  
- **Assignable roles:** For user create/update, only allow assigning either a system role or an existing custom role (role must exist in AspNetRoles and be either in Canonical or custom).  
- Do **not** seed legacy names (Kellner, Auditor, Demo, BranchManager) in new installs; migrate existing DBs (see §7).

---

## 5) Role Lifecycle Rules

| Action | Rule |
|--------|------|
| **Create** | Only custom roles. Name unique; valid name format (e.g. no empty, max length). Optional: restrict to allowlist of “templates” (e.g. clone from existing). |
| **Update permissions** | Only custom roles. System roles: permissions fixed in code; API returns 403 or 409 with clear message. |
| **Assign to user** | Role must exist. On create/update user: require at least one role; validate role exists and is assignable. |
| **Unassign** | When removing a role from a user, user must be assigned another valid role in the same request (or have a single role that is being changed, not only removed). So: “change role” allowed; “remove last role” disallowed. |
| **Delete** | Only custom roles. Block if (1) role is system, or (2) any user is assigned. Return 409 with message and optionally count of assigned users. |
| **Archive** | Not implemented. Optional: soft-delete custom role (hide from lists, prevent new assignment) and reassign users; then hard-delete when count = 0. |

**Invariants:**  
- Every user has exactly one role (current design) or at least one role (if you move to multiple roles later).  
- No user’s role is ever deleted without reassignment.  
- System roles are never deleted and never have their permission set changed via API.

---

## 6) Deletion / Reassignment Policy

- **Role with assigned users must not be deletable.**  
  Already implemented: `RoleManagementService.DeleteRoleAsync` returns `DeleteRoleResult.RoleHasAssignedUsers`; controller returns 409 and message.

- **Before deleting a custom role:**  
  1. Reassign all users to another role (e.g. Cashier or a new custom role).  
  2. Then delete the role.  
  Optional: API “replace role” (e.g. PUT replace Role A with Role B for all users) to simplify bulk reassignment; or require admin to change each user in UI.

- **Protected (system) roles:**  
  Never deletable; identifier in code. API and UI must treat them as non-deletable and show a clear message.

- **User must never end up without a valid role:**  
  - On update: if the only role is removed, require a new role in the same request (single-role model: “change role” is one field).  
  - On create: require role.  
  - Backend: validate role required and role exists (and is assignable) on create and update.

---

## 7) Migration Recommendations for Existing Roles

1. **Kellner → Waiter**  
   - Data migration: for every user with role Kellner, set role to Waiter (AspNetUserRoles + ApplicationUser.Role).  
   - Seed: stop creating Kellner.  
   - Optionally: delete Kellner role row after migration (if no users left).

2. **BranchManager → Manager**  
   - Same pattern: migrate users to Manager; stop seeding BranchManager; optionally delete role.

3. **Auditor → ReportViewer (or Accountant)**  
   - Migrate users to ReportViewer (or Accountant if they need FinanzOnline). Stop seeding Auditor; optionally delete.

4. **Demo**  
   - If no users have role "Demo": delete the Demo role from AspNetRoles; remove Demo from seed.  
   - Restrict “demo” behavior to **IsDemo** (and/or environment): in PaymentService, check `user?.IsDemo == true` (and optionally still allow role "Demo" for backward compatibility during transition).  
   - Document: “Demo users” = users with IsDemo flag (or in demo tenant); no separate “Demo” system role.

5. **Administrator**  
   - Already handled by migrations; no further action.

6. **GET /roles**  
   - Option A: Return only assignable roles (system + custom), so UI never shows legacy names after migration.  
   - Option B: Return all; filter in UI to “assignable” (e.g. exclude legacy or mark as deprecated).  
   Prefer A: backend returns only roles that are valid to assign (e.g. canonical + custom created via API). That may require a “valid for assignment” flag or allowlist.

7. **RolePermissionMatrix**  
   - No new entries needed for removed roles. Custom roles (if any) continue to use AspNetRoleClaims.

---

## 8) Risks / Open Questions

| Risk | Mitigation |
|------|------------|
| Existing users with Kellner/Auditor/BranchManager/Demo | Run data migrations; communicate; optionally keep legacy names in GET /roles as deprecated and hide from “create user” dropdown. |
| Demo user (demo@demo.com) today has Cashier + IsDemo | No change needed for that user. Ensure PaymentService uses IsDemo (or both IsDemo and role "Demo") so behavior is consistent. |
| “User” fallback role | Keep as token fallback only; never assign; never in Canonical list. |
| Multiple roles per user | Current design is one role per user (ApplicationUser.Role + single Identity role). If you later support multiple roles, permission union is already implemented (GetPermissionsForRoles); lifecycle rules must then enforce “at least one role” and “reassign before delete”. |
| Custom role name clashes | Prevent creating a custom role whose name equals a system role (case-insensitive). |
| GET /roles returns 12 roles today | After cleanup, seed only 8. For existing DBs, migration can remove legacy role rows after reassigning users; then GET /roles naturally returns fewer. |

**Open questions:**  
- Should **Kitchen** remain a system role or be a preset custom role (e.g. “Kitchen” template)?  
- Do you want an explicit **Demo** role with a fixed permission set (e.g. Cashier minus real payment) for demo tenants, or rely only on IsDemo?  
- Should **ReportViewer** and **Accountant** be merged into one “BackOfficeReadOnly” role to reduce options?

---

## Checklist (implementation)

- [ ] Require role on user create/update; validate role exists and is assignable.  
- [ ] Keep: role with assigned users not deletable; system roles not deletable/editable (permissions).  
- [ ] Migrate Kellner → Waiter, BranchManager → Manager, Auditor → ReportViewer (or Accountant); stop seeding them.  
- [ ] Resolve Demo: remove role "Demo" from seed and use IsDemo (and/or environment); or define Demo as a proper system role with matrix entry.  
- [ ] GET /roles: return only assignable roles (or mark deprecated).  
- [ ] Unassign: block removing the last role from a user (require new role in same request).  
- [ ] Document system vs custom in API and admin UI; show isSystemRole and disable delete/edit permissions for system roles.
