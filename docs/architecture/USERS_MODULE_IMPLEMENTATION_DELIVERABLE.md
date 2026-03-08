# FE-Admin Users Lifecycle – Implementation Deliverable (Austrian Fiscal Traceability)

**Date:** 2025-03-07

---

## 1) Changed files

### Backend
| File | Change |
|------|--------|
| `backend/Program.cs` | Added `UsersView` (Administrator, SuperAdmin, BranchManager, Auditor) and `UsersManage` (Administrator, SuperAdmin, BranchManager); extended `AdminUsers` with SuperAdmin. |
| `backend/Controllers/UserManagementController.cs` | Replaced `[Authorize(Roles = "Administrator")]` with `[Authorize(Policy = "UsersView")]` on GET list, GET by id, GET roles, GET search; `[Authorize(Policy = "UsersManage")]` on POST, PUT, PUT deactivate/reactivate/reset-password, DELETE, POST roles. |
| `backend/Controllers/AuditLogController.cs` | GET audit logs and GET user/{userId} now use `[Authorize(Policy = "UsersView")]` (Auditor can view). |
| `backend/Data/AppDbContext.cs` | `AuditLog.EntityId` made optional (`IsRequired(false)`) so user-lifecycle audit (EntityName only) persists. |
| `backend/KasseAPI_Final.Tests/UserManagementControllerUserLifecycleTests.cs` | Added `DeleteUser_SoftDeleteOnly_UserStillExistsWithIsActiveFalse`; removed 403 unit test (authorization runs in pipeline, not in isolated controller test). |
| `backend/KasseAPI_Final.Tests/UserLifecycleAuditIntegrationTests.cs` | **New.** Integration tests: `LogUserLifecycleAsync_PersistsDeactivateEventToDb`, `LogUserLifecycle_DeactivateAndReactivate_PersistsTwoEvents` (immutable audit persistence). |

### Frontend-admin
| File | Change |
|------|--------|
| `frontend-admin/src/app/(protected)/users/page.tsx` | `canManageUsers` = Administrator \|\| BranchManager \|\| SuperAdmin; added force password reset (state, modal, `usePutApiUserManagementIdResetPassword`, handler, button). |
| `frontend-admin/src/features/users/constants/copy.ts` | Added `resetPassword`, `resetPasswordUser`, `newPassword`, `successResetPassword`. |

### Docs
| File | Change |
|------|--------|
| `docs/architecture/USERS_MODULE_OPERATIONAL_SOP.md` | Added **Reason codes (examples)** table for deactivation (Ausscheiden, Vertragsende, Suspendierung, etc.) and reference to permission matrix. |
| `docs/architecture/USERS_MODULE_PERMISSION_MATRIX.md` | No code change; confirmed as canonical. |

---

## 2) Migration notes

- **Database:** `AuditLog.EntityId` was previously configured as required in `AppDbContext`. It is now optional. For **PostgreSQL** (and any existing migrations that created `AuditLog` with `EntityId` NOT NULL), add a migration to make the column nullable if you want to align with the new behaviour:
  - `dotnet ef migrations add MakeAuditLogEntityIdOptional -p backend/KasseAPI_Final.csproj -s backend/KasseAPI_Final.csproj`
  - In the generated migration, ensure `EntityId` is altered to nullable (e.g. `AlterColumn` with `nullable: true`). If your current schema already has `EntityId` as nullable, no migration is needed.
- **Roles:** BranchManager and SuperAdmin are seeded in `RoleSeedData.cs` (from earlier work). Ensure seed has run so these roles exist when using the new policies.
- **Frontend:** No API regeneration required; `usePutApiUserManagementIdResetPassword` and `ResetPasswordRequest` already exist in the generated client.

---

## 3) Test commands + results

```bash
cd backend
dotnet test KasseAPI_Final.Tests/KasseAPI_Final.Tests.csproj --filter "FullyQualifiedName~UserManagementControllerUserLifecycleTests|FullyQualifiedName~UserLifecycleAuditIntegrationTests|FullyQualifiedName~AdminUsersControllerTests"
```

**Result:** Passed: 25, Failed: 0, Skipped: 0.

| Test class | Coverage |
|------------|----------|
| **AdminUsersControllerTests** | ApiError shapes, GetById 404, Deactivate reason required (400), self-deactivate (400), already deactivated (400), Patch 412, ForcePasswordReset validation, Reactivate already active (400), GetActivity 404. |
| **UserManagementControllerUserLifecycleTests** | Deactivate reason empty/null (400), not found (404), self-deactivate (400), already deactivated (400), success + audit + session invalidation; Reactivate not found, already active, success + audit; **DeleteUser soft-delete only** (user still exists, IsActive false). |
| **UserLifecycleAuditIntegrationTests** | **LogUserLifecycleAsync** persists USER_DEACTIVATE to in-memory DB with reason; **Deactivate + Reactivate** lifecycle persists two audit events. |

**E2E smoke:** No Playwright/Cypress in repo. Manual smoke: log in as Administrator → Users → create user → deactivate (with reason) → open Activity tab → reactivate. Backend integration test covers audit persistence for deactivate/reactivate flow.

---

## 4) Known limitations

- **No hard delete:** There is no endpoint that removes a user from the database. `DELETE api/UserManagement/{id}` is soft-delete (sets IsActive = false, writes USER_DEACTIVATE with “Legacy DELETE (no reason)”). Prefer `PUT api/UserManagement/{id}/deactivate` with reason for compliance.
- **Session invalidation:** Implemented as **stub** (`StubUserSessionInvalidation`). Deactivate/force reset/role change call `InvalidateSessionsForUserAsync`, but no refresh-token table exists yet; only logging is performed. Real revocation requires a RefreshToken store and implementation.
- **Policy 403:** Enforced by the ASP.NET Core authorization pipeline. Unit tests that call the controller directly do not run authorization filters; 403 behaviour should be verified via API/integration tests (e.g. WebApplicationFactory) or manual testing.
- **Branch filter:** Users table has copy for “Standort” (branch); backend and FE do not yet filter by branch. Branch is shown as “—” (branchNotAvailable). Branch scope for BranchManager is documented in the permission matrix but not implemented.
- **E2E:** No automated E2E (create → deactivate → reactivate). Rely on manual smoke and backend integration tests above.
- **Auditor on Users route:** Route access to `/users` is not yet restricted by role; layout/gate may allow only Administrator/BranchManager/Auditor to reach the page. If the app uses a single “admin” gate, Auditor may need explicit access to the Users page (read-only). Confirm route guards match `UsersView` (Auditor can view list + activity).
