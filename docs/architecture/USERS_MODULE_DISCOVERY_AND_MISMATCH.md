# UserManagement Module — Discovery and Mismatch Table

## 1) UserManagement controller endpoints + Authorize attributes

| Endpoint | Method | Authorize | Purpose |
|----------|--------|-----------|---------|
| `/api/UserManagement` | GET | Policy: **UsersView** | List users (role, isActive filters) |
| `/api/UserManagement/{id}` | GET | Policy: **UsersView** | Get user by id |
| `/api/UserManagement` | POST | Policy: **UsersManage** | Create user |
| `/api/UserManagement/{id}` | PUT | Policy: **UsersManage** | Update user |
| `/api/UserManagement/{id}/password` | PUT | Policy: **UsersManage** | Change password (CurrentPassword + NewPassword) — **semantics wrong for admin** |
| `/api/UserManagement/{id}/reset-password` | PUT | Policy: **UsersManage** | Force reset (NewPassword only) |
| `/api/UserManagement/{id}/deactivate` | PUT | Policy: **UsersManage** | Deactivate (reason required) |
| `/api/UserManagement/{id}/reactivate` | PUT | Policy: **UsersManage** | Reactivate |
| `/api/UserManagement/{id}` | DELETE | Policy: **UsersManage** | Soft-delete (no reason) |
| `/api/UserManagement/roles` | GET | Policy: **UsersView** | List role names |
| `/api/UserManagement/roles` | POST | Policy: **UsersManage** | Create role |
| `/api/UserManagement/search` | GET | Policy: **UsersView** | Search users by query |

Controller: `backend/Controllers/UserManagementController.cs`. Base: `[Authorize]` (all require auth).

---

## 2) Role seed definitions

**File:** `backend/Data/RoleSeedData.cs`

Seeded role names: **Administrator**, **Admin**, **Cashier**, **Kellner**, **Auditor**, **Demo**, **Manager**, **BranchManager**, **SuperAdmin**.

**File:** `backend/Data/UserSeedData.cs`

- Default admin: `admin@admin.com` → Identity role **SuperAdmin**, `ApplicationUser.Role` = **SuperAdmin**.
- Demo user: `demo@demo.com` → **Cashier**.

---

## 3) JWT generation claims

**File:** `backend/Controllers/AuthController.cs` → `GenerateJwtToken(ApplicationUser user)`.

Claims added:

- `ClaimTypes.NameIdentifier` = user.Id  
- `ClaimTypes.Name` = user.Email  
- `ClaimTypes.Email` = user.Email  
- `"user_id"` = user.Id  
- `"user_role"` = user.Role ?? "User"  
- **`ClaimTypes.Role`** = **user.Role ?? "User"** (source: **ApplicationUser.Role** property only; Identity roles from `GetRolesAsync` are not used in the token).

---

## 4) JWT validation parameters (RoleClaimType / NameClaimType)

**File:** `backend/Program.cs` — `AddJwtBearer(options => { ... })`.

- **RoleClaimType / NameClaimType:** Not set. Defaults: ASP.NET Core uses `ClaimTypes.Role` for role authorization. JWT handler does not remap inbound claim types, so the token’s claim type is preserved (we emit `ClaimTypes.Role`, so it matches).
- **TokenValidationParameters:** ValidateIssuerSigningKey, ValidateIssuer, ValidateAudience, ValidateLifetime, ClockSkew = 0. No custom role/name claim type mapping.

**Conclusion:** Role in token must match policy role names. If `ApplicationUser.Role` is "Administrator" but policies only allow "SuperAdmin", "Admin", "BranchManager", "Auditor", then **Administrator** gets 403.

---

## 5) FE Users page calls and menu/route role checks

**Users page:** `frontend-admin/src/app/(protected)/users/page.tsx`

- **API calls:** `useUsersList` (GET list/search via `usersApi.ts`), `useGetApiUserManagementRoles`, `usePostApiUserManagement`, `usePutApiUserManagementId`, `usePutApiUserManagementIdResetPassword`; deactivate/reactivate via `usersApi` (PUT deactivate/reactivate).
- **Role guard:** `canManageUsers = ['SuperAdmin', 'Admin', 'BranchManager'].includes(currentUser?.role ?? '')`. So **Administrator** and **Auditor** are not in `canManageUsers`; Auditor should only view (no manage). **Administrator** should be able to manage but is currently missing → mismatch.

**Menu:** `frontend-admin/src/app/(protected)/layout.tsx`

- Users link: shown to all authenticated users (no role filter). RKSV submenu: only `['SuperAdmin', 'Admin'].includes(user?.role)` — **Administrator** not included for RKSV.

**Auth source:** `useAuth()` → GET `/api/Auth/me` → `user.role` from response (backend returns `user.Role` from DB).

---

## 6) Mismatch table

| Area | Current behavior | Expected behavior | Risk | Fix |
|------|------------------|-------------------|------|-----|
| **Backend policies** | UsersView/UsersManage require SuperAdmin, Admin, BranchManager, Auditor (no **Administrator**). | Admin-like users (including legacy **Administrator**) can access UserManagement. | Users with role "Administrator" get 403. | Add **Administrator** to UsersView and UsersManage (and AdminUsers) in Program.cs. |
| **JWT role source** | Token role = `ApplicationUser.Role` only. | Single source of truth; prefer Identity roles when present. | Stale or inconsistent Role vs Identity roles. | Optionally build JWT role from `GetRolesAsync()` first, fallback to user.Role. |
| **FE canManageUsers** | SuperAdmin, Admin, BranchManager only. | Administrator can manage; Auditor can only view. | Administrator cannot see manage actions. | Add **Administrator** to `canManageUsers`. Add **Auditor** to a view-only guard if list is hidden for them. |
| **FE menu (Users)** | Users link visible to all. | Only roles with UsersView see Users link. | Cashier can open /users and hit 403. | Filter menu: show Users only when `user.role` is in UsersView set (SuperAdmin, Admin, Administrator, BranchManager, Auditor). |
| **Password semantics** | PUT `/{id}/password` requires UsersManage + CurrentPassword+NewPassword; used for both self and admin. | (A) Change **own** password: currentPassword + newPassword, any authenticated user. (B) **Force** reset **other** user: newPassword only, admin only, no self. | Admin cannot force-reset without knowing user’s current password; self-change requires manage permission. | Add **PUT /api/UserManagement/me/password** (change own). Keep **PUT /api/UserManagement/{id}/reset-password** for force reset; block self; add audit FORCE_RESET_PASSWORD. Deprecate or remove use of `/{id}/password` for admin reset. |
| **Force reset self** | Allowed (id can be current user). | Block self-reset via force endpoint; user must use change-own-password. | Audit/safety: force reset should not be used for self. | In reset-password: if target id == currentUserId, return 400 with message to use me/password. |
| **Privilege escalation** | Admin can reset SuperAdmin. | Only SuperAdmin can reset SuperAdmin (configurable). | Lower-privilege admin could lock out SuperAdmin. | In reset-password (and optionally deactivate): if target has role SuperAdmin, require actor role SuperAdmin. |
| **Audit** | USER_PASSWORD_RESET for force reset; USER_UPDATE for own change. | Distinct events: CHANGE_OWN_PASSWORD and FORCE_RESET_PASSWORD. | Harder to distinguish in audit. | Add audit actions CHANGE_OWN_PASSWORD, FORCE_RESET_PASSWORD; log accordingly. |

---

## 7) Password endpoint semantics (summary)

- **Change own password:** New endpoint **PUT /api/UserManagement/me/password** — body: `{ currentPassword, newPassword }`. `[Authorize]` only. Any authenticated active user. Audit: **CHANGE_OWN_PASSWORD**.
- **Force reset another user:** Existing **PUT /api/UserManagement/{id}/reset-password** — body: `{ newPassword }`. Policy: **UsersManage**. Block if id == current user. Optionally: only SuperAdmin can reset SuperAdmin. Invalidate target sessions. Audit: **FORCE_RESET_PASSWORD** (or keep USER_PASSWORD_RESET with description).
- **PUT /api/UserManagement/{id}/password** (current): Remove from use for admin flow; keep only if we want “admin changes password knowing current” (rare). Prefer removing or documenting as deprecated in favor of me/password (self) and reset-password (force).

---

## 8) Verification checklist (deterministic)

### Backend tests

```bash
cd backend
dotnet test KasseAPI_Final.Tests/KasseAPI_Final.Tests.csproj --filter "FullyQualifiedName~UserManagementAuthorizationPolicyTests|FullyQualifiedName~UserManagementControllerUserLifecycleTests"
```

- **UserManagementAuthorizationPolicyTests:** UsersView/UsersManage allow SuperAdmin, Admin, **Administrator**; deny Cashier.
- **UserManagementControllerUserLifecycleTests:** ResetPassword_WhenTargetIsSelf_ReturnsBadRequest returns 400.

### Manual verification

| Step | Action | Expected |
|------|--------|----------|
| 1 | Log in as admin@admin.com (SuperAdmin). Open Users. | List and roles load; create/edit/deactivate/reactivate/reset visible. |
| 2 | Log in as user with role **Administrator**. Open Users. | Same as step 1 (no 403). |
| 3 | Log in as user with role **Admin**. Open Users. | Same as step 1. |
| 4 | Log in as **Auditor**. Open Users. | List and roles load; manage actions hidden. |
| 5 | Log in as **Cashier**. | Users link not in menu; direct navigate to /users shows access-denied message; no API call. |
| 6 | As admin: force-reset another user's password. | 200; target sessions invalidated; audit FORCE_RESET_PASSWORD. |
| 7 | As admin: call PUT .../reset-password with id = own id. | 400, message to use me/password. |
| 8 | As any authenticated user: PUT .../me/password with currentPassword + newPassword. | 200; audit CHANGE_OWN_PASSWORD. |
| 9 | As Admin: try to force-reset a **SuperAdmin** user. | 403. |
