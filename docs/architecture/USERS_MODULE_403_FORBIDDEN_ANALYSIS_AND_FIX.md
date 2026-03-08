# FE-Admin Users 403 Forbidden — Analysis and Fix

## A) Root cause

**Primary:** Role alias mismatch.  
- Endpoints `GET /api/UserManagement` and `GET /api/UserManagement/roles` are protected by policy **UsersView**.  
- UsersView was defined to require one of: `Administrator`, `SuperAdmin`, `BranchManager`, `Auditor`.  
- The authenticated user had role **Admin** (legacy/seed role).  
- **Admin** was not in the policy, so authorization failed → 403.

**Secondary:** FE-Admin did not treat **Admin** as a management role for the Users page, so `canManageUsers` was false for Admin; the list/roles API would still be called when opening the page, and the backend 403 was the visible failure.

---

## B) Changed files and why

| File | Why |
|------|-----|
| `backend/Program.cs` | Added **Admin** to policies **AdminUsers**, **UsersView**, **UsersManage** so token with role `Admin` passes authorization. |
| `frontend-admin/src/app/(protected)/users/page.tsx` | Added **Admin** to `canManageUsers` so Admin users see Edit/Deactivate/Reactivate/Reset actions. |
| `backend/KasseAPI_Final.Tests/UserManagementAuthorizationPolicyTests.cs` | New tests: Admin passes UsersView and UsersManage; Cashier fails both. |

No custom 403 response body was added (e.g. `IAuthorizationMiddlewareResultHandler`), to avoid API/version coupling; server log and optional future middleware can add `code`/`reason` later.

---

## C) Before/after snippets

### Backend — Program.cs (authorization policies)

**Before:**
```csharp
options.AddPolicy("AdminUsers", policy =>
    policy.RequireRole("Administrator", "SuperAdmin"));
options.AddPolicy("UsersView", policy =>
    policy.RequireRole("Administrator", "SuperAdmin", "BranchManager", "Auditor"));
options.AddPolicy("UsersManage", policy =>
    policy.RequireRole("Administrator", "SuperAdmin", "BranchManager"));
```

**After:**
```csharp
// "Admin" = legacy alias for Administrator
options.AddPolicy("AdminUsers", policy =>
    policy.RequireRole("Administrator", "SuperAdmin", "Admin"));
options.AddPolicy("UsersView", policy =>
    policy.RequireRole("Administrator", "SuperAdmin", "BranchManager", "Auditor", "Admin"));
options.AddPolicy("UsersManage", policy =>
    policy.RequireRole("Administrator", "SuperAdmin", "BranchManager", "Admin"));
```

### Backend — Deny point (unchanged)

- **Controller:** `UserManagementController`, route `api/[controller]` → `api/UserManagement`.
- **GET list:** `[Authorize(Policy = "UsersView")]` on `GetUsers`.
- **GET roles:** `[Authorize(Policy = "UsersView")]` on `GetRoles`.
- **403 log:** JWT Bearer `OnForbidden` in `Program.cs` logs `userId`, `roles`, `path` (no change).

### Frontend — users/page.tsx (canManageUsers)

**Before:**
```ts
const canManageUsers = ['Administrator', 'BranchManager', 'SuperAdmin'].includes(currentUser?.role ?? '');
```

**After:**
```ts
const canManageUsers = ['Administrator', 'BranchManager', 'SuperAdmin', 'Admin'].includes(currentUser?.role ?? '');
```

### JWT / claim mapping (unchanged)

- Login builds JWT with `ClaimTypes.Role` = `user.Role` (ApplicationUser.Role).
- No RoleClaimType override; default matches.
- Token for admin@admin.com has role **Admin** when that user’s `Role` is "Admin".

---

## D) Test commands + outputs

```bash
cd backend
dotnet test KasseAPI_Final.Tests/KasseAPI_Final.Tests.csproj --filter "FullyQualifiedName~UserManagementAuthorizationPolicyTests"
```

**Result:** Passed — 5 tests (Admin/Administrator pass UsersView and UsersManage; Cashier fails both).

```
Passed!  - Failed: 0, Passed: 5, Skipped: 0, Total: 5
```

- `UsersView_Policy_Allows_Admin_Role`
- `UsersView_Policy_Allows_Administrator_Role`
- `UsersManage_Policy_Allows_Admin_Role`
- `UsersView_Policy_Denies_Cashier_Role`
- `UsersManage_Policy_Denies_Cashier_Role`

---

## E) Manual verification checklist

| Actor | GET /api/UserManagement | GET /api/UserManagement/roles | FE Users page list | FE manage actions |
|-------|-------------------------|-------------------------------|--------------------|-------------------|
| **Admin** | 200 | 200 | Loads | Visible |
| **Administrator** | 200 | 200 | Loads | Visible |
| **BranchManager** | 200 | 200 | Loads | Visible |
| **Auditor** | 200 | 200 | Loads | Hidden (read-only) |
| **Cashier** | 403 | 403 | — | — |

Steps:

1. Log in as user with role **Admin** (e.g. admin@admin.com). Open **Users**. List and roles should load; no 403.
2. Log in as **Administrator**. Same as above.
3. Log in as **BranchManager**. Same as above; manage actions visible.
4. Log in as **Auditor**. List and roles load; manage actions hidden.
5. Log in as **Cashier**. Navigating to Users should result in 403 from the API (or FE can hide the menu for non–UsersView roles if desired).

---

## Endpoint → policy matrix (reference)

| Endpoint | Method | Policy | Admin after fix |
|----------|--------|--------|-----------------|
| /api/UserManagement | GET | UsersView | Allowed |
| /api/UserManagement/roles | GET | UsersView | Allowed |
| /api/UserManagement/{id} | GET | UsersView | Allowed |
| /api/UserManagement | POST | UsersManage | Allowed |
| /api/UserManagement/{id} | PUT | UsersManage | Allowed |
| … (other mutating actions) | … | UsersManage | Allowed |
