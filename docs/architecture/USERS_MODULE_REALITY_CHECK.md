# FE-Admin Users Module – Strict Repository Reality-Check

**Goal:** Verify whether the claimed SOP and user lifecycle implementation exist in code (not only in docs).

**Date:** 2025-03-07

---

## 1) Artifact scan with exact file paths and status

### 1.1 Users module backend APIs (create / update / deactivate / reactivate / reset / activity)

| API | File path | Status | Evidence |
|-----|-----------|--------|----------|
| **AdminUsersController** (create, update, deactivate, reactivate, force-password-reset, activity) | `backend/Controllers/AdminUsersController.cs` | EXISTS_AND_CONNECTED | Route `api/admin/users`; GET list, GET {id}, POST create, PATCH {id}, POST {id}/deactivate, POST {id}/reactivate, POST {id}/force-password-reset, GET {id}/activity. |
| **UserManagementController** (list, get, create, update, deactivate, reactivate, reset-password, DELETE soft) | `backend/Controllers/UserManagementController.cs` | EXISTS_AND_CONNECTED | Route `api/UserManagement`; GET, GET {id}, POST, PUT {id}, PUT {id}/deactivate, PUT {id}/reactivate, PUT {id}/reset-password, DELETE {id}. FE uses this for list + deactivate + reactivate (`frontend-admin/src/features/users/api/usersApi.ts`). |
| **AuditLog user activity** (GET logs for one user) | `backend/Controllers/AuditLogController.cs` | EXISTS_BUT_PARTIAL | GET `api/AuditLog/user/{userId}`; calls `GetUserAuditLogsAsync(userId)`. Service filters by `UserId == userId` (actor), not by target user; lifecycle events store target in `EntityName`, so activity tab shows wrong scope. |

### 1.2 Authorization policies for SuperAdmin / BranchManager / Cashier / Kellner / Auditor

| Artifact | File path | Status | Evidence |
|----------|-----------|--------|----------|
| **AdminUsers policy** | `backend/Program.cs` (lines 127–131) | EXISTS_AND_CONNECTED | `AddPolicy("AdminUsers", policy => policy.RequireRole("Administrator"))`. |
| **UserManagement role checks** | `backend/Controllers/UserManagementController.cs` | EXISTS_AND_CONNECTED | `[Authorize(Roles = "Administrator")]` on GET list, GET {id}, POST, PUT, PUT deactivate, PUT reactivate, PUT reset-password, DELETE, GET roles, POST roles, GET search. |
| **SuperAdmin / BranchManager in backend** | `backend/Program.cs`, `backend/Data/RoleSeedData.cs` | MISSING | No policy uses SuperAdmin or BranchManager. RoleSeedData seeds Administrator, Admin, Cashier, Kellner, Auditor, Demo, Manager only. |
| **Auditor / Cashier / Kellner as roles** | `backend/Data/RoleSeedData.cs` | EXISTS_AND_CONNECTED | Roles created: Administrator, Admin, Cashier, Kellner, Auditor, Demo, Manager. Not used for Users module endpoints (only Administrator). |

### 1.3 Audit event writers (USER_CREATE, USER_DEACTIVATE, USER_REACTIVATE, USER_PASSWORD_RESET)

| Artifact | File path | Status | Evidence |
|----------|-----------|--------|----------|
| **Constants** | `backend/Models/AuditLog.cs` (AuditLogActions) | EXISTS_AND_CONNECTED | USER_CREATE, USER_UPDATE, USER_DEACTIVATE, USER_REACTIVATE, USER_ROLE_CHANGE, USER_PASSWORD_RESET. |
| **LogUserLifecycleAsync** | `backend/Services/AuditLogService.cs` (lines 328–376) | EXISTS_AND_CONNECTED | Writes to `_context.AuditLogs`; SaveChangesAsync; stores actor, targetUserId (EntityName), reason, description. |
| **AdminUsersController** | `backend/Controllers/AdminUsersController.cs` | EXISTS_AND_CONNECTED | Calls LogUserLifecycleAsync for create (155), update (204), role change (207), deactivate (247), reactivate (279), password reset (304). |
| **UserManagementController** | `backend/Controllers/UserManagementController.cs` | EXISTS_AND_CONNECTED | Calls LogUserLifecycleAsync for create (213), update/role (305), reset-password (392), deactivate (450), reactivate (500), DELETE soft (548). |

### 1.4 Session invalidation / revocation logic

| Artifact | File path | Status | Evidence |
|----------|-----------|--------|----------|
| **Interface** | `backend/Services/IUserSessionInvalidation.cs` | EXISTS_AND_CONNECTED | `InvalidateSessionsForUserAsync(string userId)`. |
| **Stub implementation** | `backend/Services/StubUserSessionInvalidation.cs` | EXISTS_BUT_PARTIAL | Logs only; no token revocation (no RefreshToken table). |
| **Registration** | `backend/Program.cs` (lines 133–134) | EXISTS_AND_CONNECTED | `AddScoped<IUserSessionInvalidation, StubUserSessionInvalidation>()`. |
| **AdminUsersController** | `backend/Controllers/AdminUsersController.cs` | EXISTS_AND_CONNECTED | Calls InvalidateSessionsForUserAsync after deactivate (208), force-password-reset (250), role change (306). |
| **UserManagementController** | `backend/Controllers/UserManagementController.cs` | MISSING | Does not inject or call IUserSessionInvalidation. FE uses this path for deactivate/reactivate; deactivated user sessions are not invalidated. |

### 1.5 FE-Admin users page actions + reason modal + activity timeline

| Artifact | File path | Status | Evidence |
|----------|-----------|--------|----------|
| **Users page** | `frontend-admin/src/app/(protected)/users/page.tsx` | EXISTS_AND_CONNECTED | List, filters, create/edit drawer, deactivate/reactivate buttons, modals. |
| **Deactivate reason modal** | `frontend-admin/src/app/(protected)/users/page.tsx` (Modal + Form reason) | EXISTS_AND_CONNECTED | Form with `name="reason"`, `rules={[{ required: true, message: usersCopy.reasonRequiredMessage }]}`; calls `deactivateUser(id, { reason })`. |
| **usersApi (deactivate/reactivate)** | `frontend-admin/src/features/users/api/usersApi.ts` | EXISTS_AND_CONNECTED | `deactivateUser` → PUT `/api/UserManagement/${id}/deactivate`; `reactivateUser` → PUT `/api/UserManagement/${id}/reactivate`. |
| **UserDetailDrawer + Activity tab** | `frontend-admin/src/features/users/components/UserDetailDrawer.tsx` | EXISTS_AND_CONNECTED | Tabs: Aktivität (activity), Details. |
| **UserActivityTimeline** | `frontend-admin/src/features/users/components/UserActivityTimeline.tsx` | EXISTS_AND_CONNECTED | Uses `useGetApiAuditLogUserUserId(userId)` → GET `/api/AuditLog/user/${userId}`. Backend returns wrong scope (actor vs target). |
| **Copy (reason labels)** | `frontend-admin/src/features/users/constants/copy.ts` | EXISTS_AND_CONNECTED | reasonRequired, reasonPlaceholder, reasonRequiredMessage. |

### 1.6 Tests covering business / auth / audit rules

| Artifact | File path | Status | Evidence |
|----------|-----------|--------|----------|
| **AdminUsersController unit tests** | `backend/KasseAPI_Final.Tests/AdminUsersControllerTests.cs` | EXISTS_AND_CONNECTED | ApiError shapes; GetById 404; Deactivate reason required (400); Deactivate self (400); Deactivate success; Patch concurrency 412; ForcePasswordReset. |
| **UserManagementController tests** | — | MISSING | No unit or integration tests for UserManagementController deactivate/reactivate/reason/audit. |
| **User lifecycle integration tests** | — | MISSING | No integration test for create → deactivate → reactivate with audit and session invalidation. |
| **E2E / smoke for Users** | — | MISSING | No E2E or smoke test for Users module. |

### 1.7 SOP file under docs/architecture

| Artifact | File path | Status |
|----------|-----------|--------|
| **SOP** | `docs/architecture/USERS_MODULE_OPERATIONAL_SOP.md` | EXISTS_AND_CONNECTED |
| **Permission matrix** | `docs/architecture/USERS_MODULE_PERMISSION_MATRIX.md` | EXISTS_AND_CONNECTED |
| **Audit event schema** | `docs/architecture/USER_LIFECYCLE_AUDIT_EVENT_SCHEMA.md` | EXISTS_AND_CONNECTED |

---

## 2) Gap list sorted by production risk (high → low)

| Priority | Risk | Gap | Affected artifact |
|----------|------|-----|-------------------|
| 1 | HIGH | Session invalidation not called on deactivate/reset when using UserManagement API (FE path). Deactivated user can keep using existing sessions. | UserManagementController |
| 2 | HIGH | User activity timeline shows “actions by user” (UserId = actor) instead of “actions on user” (target). SOP requires reviewing deactivate/reactivate/reset events *for* that user. | AuditLogService.GetUserAuditLogsAsync |
| 3 | MEDIUM | No unit tests for UserManagementController (reason required, audit calls, self-deactivate). Regression risk. | Tests |
| 4 | MEDIUM | Session invalidation is stub only; no real token revocation until RefreshToken store exists. | StubUserSessionInvalidation |
| 5 | LOW | SuperAdmin / BranchManager not in backend policies or role seed; permission matrix describes them but code does not. | Program.cs, RoleSeedData.cs |
| 6 | LOW | No integration or E2E/smoke tests for user lifecycle. | Tests |

---

## 3) File-level patch plan (MISSING / partial only)

| # | Action | File(s) |
|---|--------|--------|
| 1 | Add session invalidation to UserManagementController | `backend/Controllers/UserManagementController.cs`: inject IUserSessionInvalidation; call InvalidateSessionsForUserAsync(id) after PUT deactivate and after PUT reset-password. |
| 2 | Fix user activity API to “events on user” (target) | `backend/Services/AuditLogService.cs`: In GetUserAuditLogsAsync, filter by EntityType == AuditLogEntityTypes.USER && EntityName == userId (and keep date filters). Optionally keep backward compatibility: add overload or parameter “asTarget” if other callers need “by actor”. Audit: only AuditLogController and AdminUsersController.GetActivity use it; both intend “activity for user X” = target. So change filter to target. |
| 3 | Add UserManagementController unit tests | New or existing test file: tests for DeactivateUser (reason required 400, self 400, success + audit + session invalidation mock), ReactivateUser (success + audit), ResetPassword (session invalidation mock). |
| 4 | Document policy/role gap | `docs/architecture/USERS_MODULE_PERMISSION_MATRIX.md` or reality-check: add note that backend currently only uses Administrator; SuperAdmin/BranchManager/Auditor scope not yet implemented. |
| 5 | (Optional) Seed BranchManager / SuperAdmin roles | `backend/Data/RoleSeedData.cs`: add BranchManager and SuperAdmin if product requires; policies still to be extended separately. |

**Out of scope for this patch:** Real session invalidation (RefreshToken table), integration/e2e tests (separate task), hard-delete (remains disabled; DELETE stays soft with comment).

---

## 5) Implementation completed (2025-03-07)

| # | Action | Status | File(s) changed |
|---|--------|--------|-----------------|
| 1 | Session invalidation in UserManagementController | Done | `backend/Controllers/UserManagementController.cs`: IUserSessionInvalidation injected; called after PUT deactivate, PUT reset-password, PUT {id} (role change), DELETE {id}. |
| 2 | User activity API: filter by target user | Done | `backend/Services/AuditLogService.cs`: GetUserAuditLogsAsync now filters EntityType == USER && EntityName == userId; added GetUserLifecycleAuditLogsCountAsync. `backend/Controllers/AuditLogController.cs`, `AdminUsersController.cs`: use new count for user activity. |
| 3 | UserManagementController unit tests | Done | `backend/KasseAPI_Final.Tests/UserManagementControllerUserLifecycleTests.cs`: reason required (400), not found (404), self-deactivate (400), already deactivated (400), success + audit + session invalidation; reactivate not found, already active, success + audit. |
| 4 | Policy/role gap documented | Done | This doc (gap list + patch plan). |
| 5 | BranchManager / SuperAdmin roles seeded | Done | `backend/Data/RoleSeedData.cs`: added BranchManager and SuperAdmin. Backend policies still use only Administrator for Users endpoints. |

---

## 4) Constraints verified

- **Hard-delete disabled:** AdminUsersController has no delete. UserManagementController DELETE {id} is soft-deactivate (IsActive = false) with comment “prefer PUT deactivate for compliance”. No hard delete of fiscally-linked users.
- **Immutable audit:** LogUserLifecycleAsync only adds; no update/delete of audit records in this flow.
