# FE-Admin Users Module — Permission Matrix

Copy-paste ready design for roles, backend policies, FE guards, and 403 response model.

**Roles:** SuperAdmin, BranchManager, Cashier, Kellner, Auditor.  
**Note:** Backend currently uses `Administrator`; treat **SuperAdmin** as equivalent to Administrator for policy mapping until a separate SuperAdmin role is introduced.

---

## 1) Matrix table

| Action | SuperAdmin | BranchManager | Cashier | Kellner | Auditor |
|--------|:----------:|:-------------:|:-------:|:-------:|:-------:|
| View users list | ✅ | ✅ | ❌ | ❌ | ✅ (read-only) |
| Create user | ✅ | ✅* | ❌ | ❌ | ❌ |
| Update user (profile) | ✅ | ✅* | ❌ | ❌ | ❌ |
| Update role | ✅ | ✅** | ❌ | ❌ | ❌ |
| Transfer branch | ✅ | ✅** | ❌ | ❌ | ❌ |
| Deactivate / Reactivate | ✅ | ✅* | ❌ | ❌ | ❌ |
| Reset password (force) | ✅ | ✅* | ❌ | ❌ | ❌ |
| View audit activity (per user) | ✅ | ✅ | ❌ | ❌ | ✅ |
| Export user activity report | ✅ | ✅* | ❌ | ❌ | ✅ |

- **\*** BranchManager: scope limited to own branch when branch model is enforced.
- **\*\*** BranchManager: cannot assign SuperAdmin or transfer users outside own branch (when enforced).

**Summary**

| Role | Capability |
|------|------------|
| **SuperAdmin** | Full user management; all actions. |
| **BranchManager** | Full within branch: view, create, update, role, transfer, deactivate, reset, view audit, export. |
| **Cashier** | No access to Users module (POS only). |
| **Kellner** | No access to Users module (POS only). |
| **Auditor** | Read-only: view users list, view audit activity, export user activity report. No create/update/deactivate/reset. |

---

## 2) Backend policy names

Use one policy per “permission scope” and attach to controller/actions. Suggested names and role requirements:

| Policy name | Required role(s) | Use for |
|-------------|------------------|--------|
| `UsersView` | SuperAdmin, BranchManager, Auditor | GET list, GET by id, GET activity (read-only). |
| `UsersManage` | SuperAdmin, BranchManager | POST create, PATCH update, POST deactivate, POST reactivate, POST force-password-reset. |
| `UsersAssignRole` | SuperAdmin (or SuperAdmin + BranchManager with scope) | PATCH when role is being changed. |
| `UsersTransferBranch` | SuperAdmin, BranchManager | PATCH when branch is being changed. |
| `UsersExportReport` | SuperAdmin, BranchManager, Auditor | GET export user activity report. |

**Minimal implementation (current style):**  
A single policy for “admin” user management is enough to start:

- `AdminUsers` → `RequireRole("Administrator")` (or `"SuperAdmin"` when added)  
  Use for: list, get, create, PATCH, deactivate, reactivate, force-password-reset, activity, export.

**Extended (when BranchManager + Auditor exist):**

```csharp
// Program.cs – example
options.AddPolicy("UsersView", policy =>
    policy.RequireRole("Administrator", "SuperAdmin", "BranchManager", "Auditor"));
options.AddPolicy("UsersManage", policy =>
    policy.RequireRole("Administrator", "SuperAdmin", "BranchManager"));
options.AddPolicy("UsersExportReport", policy =>
    policy.RequireRole("Administrator", "SuperAdmin", "BranchManager", "Auditor"));
```

**Controller usage:**

- `[Authorize(Policy = "UsersView")]` on GET list, GET {id}, GET {id}/activity.
- `[Authorize(Policy = "UsersManage")]` on POST, PATCH, POST deactivate, POST reactivate, POST force-password-reset.
- `[Authorize(Policy = "UsersExportReport")]` on GET export endpoint (if separate).

---

## 3) FE route guards

**Route layout (suggestion):**

- `/users` — Users module (list, filters, create button, row actions).

**Guard logic by role:**

| Route / area | SuperAdmin | BranchManager | Cashier | Kellner | Auditor |
|--------------|:----------:|:-------------:|:-------:|:-------:|:-------:|
| `/users` | Full access | Full access* | Redirect 403 | Redirect 403 | Read-only (hide create/edit/deactivate/reactivate/reset) |

**Implementation options:**

**Option A — Single “users module” guard (current pattern):**

- Wrap `/users` in a gate that allows: `Administrator` (or SuperAdmin), `BranchManager`, `Auditor`.
- Cashier / Kellner → redirect to `/403` or dashboard.
- Inside the page, hide create/edit/deactivate/reactivate/reset for `Auditor` (e.g. `canManageUsers = role === 'SuperAdmin' || role === 'Administrator' || role === 'BranchManager'`).

**Option B — Reusable permission gate component:**

```tsx
// shared/auth/RequirePermissionGate.tsx
type Permission = 'users.view' | 'users.manage' | 'users.export';
const ROLE_PERMISSIONS: Record<string, Permission[]> = {
  Administrator: ['users.view', 'users.manage', 'users.export'],
  SuperAdmin: ['users.view', 'users.manage', 'users.export'],
  BranchManager: ['users.view', 'users.manage', 'users.export'],
  Auditor: ['users.view', 'users.export'],
  Cashier: [],
  Kellner: [],
};
// Gate: if user lacks permission, redirect to /403 or show forbidden message.
```

**Route guard (e.g. layout or middleware):**

- For `/users`: require at least `users.view` (SuperAdmin, BranchManager, Auditor). If missing → 403.
- Buttons: Create/Edit/Deactivate/Reactivate/Reset → require `users.manage`; Export → require `users.export`.

**Example layout guard (Next.js App Router):**

```tsx
// app/(protected)/users/layout.tsx
export default function UsersLayout({ children }: { children: React.ReactNode }) {
  return (
    <RequirePermissionGate permission="users.view" fallbackPath="/403">
      {children}
    </RequirePermissionGate>
  );
}
```

**Button visibility (inside page):**

```tsx
const canManage = ['Administrator', 'SuperAdmin', 'BranchManager'].includes(user?.role ?? '');
const canExport = canManage || user?.role === 'Auditor';
// Show Create / Edit / Deactivate / Reactivate / Reset only when canManage.
// Show Export only when canExport.
```

---

## 4) Forbidden action response model (403 + reason code)

**Goal:** Same structure for all 403 responses so the FE can show a specific message or redirect based on a reason code.

**Suggested response body (align with existing ApiError):**

```json
{
  "type": "Forbidden",
  "title": "Forbidden",
  "status": 403,
  "detail": "You do not have permission to perform this action.",
  "reasonCode": "USERS_MANAGE_REQUIRED",
  "instance": "/api/admin/users"
}
```

**Reason codes (stable; FE can switch on them):**

| reasonCode | Meaning |
|------------|--------|
| `FORBIDDEN` | Generic (no role / policy failed). |
| `USERS_VIEW_REQUIRED` | Need at least UsersView (e.g. view list, view activity). |
| `USERS_MANAGE_REQUIRED` | Need UsersManage (create, update, deactivate, reactivate, reset). |
| `USERS_EXPORT_REQUIRED` | Need UsersExportReport. |
| `USERS_ASSIGN_ROLE_REQUIRED` | Need permission to change roles (e.g. SuperAdmin only). |
| `USERS_TRANSFER_BRANCH_REQUIRED` | Need permission to transfer branch. |
| `SCOPE_BRANCH` | Actor is BranchManager and action targets user outside own branch. |

**Backend model (implemented in `ApiError`):**

- `ApiError` has optional `ReasonCode` (string). Use `ApiError.Forbidden(title, detail, reasonCode)`.
- `ApiError.ForbiddenReasonCodes` contains stable constants: `Forbidden`, `UsersViewRequired`, `UsersManageRequired`, `UsersExportRequired`, `UsersAssignRoleRequired`, `UsersTransferBranchRequired`, `ScopeBranch`.
- Example: `return Forbidden(ApiError.Forbidden("Forbidden", "You cannot manage users.", ApiError.ForbiddenReasonCodes.UsersManageRequired));` (or equivalent with your controller base).

**FE handling:**

- On 403, read `body.reasonCode` (or `body.detail`).
- Map to i18n key, e.g. `errors.forbidden.USERS_MANAGE_REQUIRED` → "Nur Administratoren und Branch-Manager dürfen Benutzer anlegen oder bearbeiten."
- Redirect to `/403` with optional query: `/403?reason=USERS_MANAGE_REQUIRED`.

---

## 5) Quick reference

| Deliverable | Location |
|------------|----------|
| Matrix table | Section 1 above. |
| Backend policy names | Section 2: `UsersView`, `UsersManage`, `UsersAssignRole`, `UsersTransferBranch`, `UsersExportReport`. |
| FE route guards | Section 3: Require `users.view` for `/users`; use `RequirePermissionGate` or role allowlist; button visibility by `users.manage` / `users.export`. |
| 403 response model | Section 4: Status 403, `type` "Forbidden", optional `reasonCode`; table of codes and FE handling. |

---

*Backend currently uses `Administrator`; map SuperAdmin to Administrator until a distinct SuperAdmin role is seeded. BranchManager and Auditor are in RoleSeedData as Manager and Auditor; rename or add BranchManager as needed.*
