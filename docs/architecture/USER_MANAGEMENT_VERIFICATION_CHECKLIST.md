# User Management — Test & Security Verification Checklist

CI-friendly verification checklist for user management compliance (RKSV/DSGVO, no hard delete, audit, session invalidation). Each item has a **Test ID**, **Pass criteria**, and optional **Location/Command**.

---

## 1. Unit tests (business rules)

| ID | Description | Pass criteria | Location / Command |
|----|-------------|----------------|---------------------|
| **UM-UNIT-01** | No hard delete: AdminUsersController has no DELETE endpoint that removes user from store | No `HttpDelete` or `_userManager.DeleteAsync` in AdminUsersController | `backend/Controllers/AdminUsersController.cs` |
| **UM-UNIT-02** | Deactivate: empty reason returns 400 ValidationError | `Deactivate(id, { reason: "" })` → 400, body.type === "ValidationError", body.errors.reason present | `AdminUsersControllerTests.Deactivate_WhenReasonEmpty_ReturnsBadRequest` |
| **UM-UNIT-03** | Deactivate: user not found returns 404 | `Deactivate("nonexistent", { reason: "x" })` → 404 | `AdminUsersControllerTests.Deactivate_WhenUserNotFound_ReturnsNotFound` |
| **UM-UNIT-04** | Deactivate: actor cannot deactivate self (business rule) | When actorId === targetId, response 400 BusinessRule "You cannot deactivate your own account" | `AdminUsersControllerTests.Deactivate_WhenActorIsTarget_ReturnsBadRequest` |
| **UM-UNIT-05** | Deactivate: already deactivated user returns 400 BusinessRule | User.IsActive false → 400, type "BusinessRule" | Add: `Deactivate_WhenUserAlreadyDeactivated_ReturnsBadRequest` |
| **UM-UNIT-06** | Reactivate: already active returns 400 BusinessRule | `Reactivate(id)` when user is active → 400, type "BusinessRule" | `AdminUsersControllerTests.Reactivate_WhenUserAlreadyActive_ReturnsBadRequest` |
| **UM-UNIT-07** | PATCH: If-Match / etag mismatch returns 412 Conflict | PATCH with wrong `If-Match` → 412, ApiError.Type "Conflict" | `AdminUsersControllerTests.Patch_WhenIfMatchMismatch_Returns412` |
| **UM-UNIT-08** | ForcePasswordReset: password &lt; 6 chars returns 400 | NewPassword length &lt; 6 → 400, errors.newPassword | `AdminUsersControllerTests.ForcePasswordReset_WhenPasswordTooShort_ReturnsBadRequest` |
| **UM-UNIT-09** | GetById: not found returns 404; exists returns DTO without password/hash | 404 for missing id; 200 with Id, UserName, Etag, no PasswordHash | `AdminUsersControllerTests.GetById_*` |
| **UM-UNIT-10** | ApiError factory methods set correct Status and Type | Validation 400, NotFound 404, ConcurrencyConflict 412, BusinessRule 400 | `AdminUsersControllerTests.ApiError_*` |

**CI command (backend unit):**
```bash
cd backend && dotnet test --filter "FullyQualifiedName~AdminUsersControllerTests" --no-build
```

---

## 2. Integration tests (API auth policies & audit writes)

| ID | Description | Pass criteria | Notes |
|----|-------------|----------------|-------|
| **UM-INT-01** | GET /api/admin/users without auth returns 401 | No Authorization header → 401 | Use WebApplicationFactory + HttpClient |
| **UM-INT-02** | GET /api/admin/users with non-Administrator role returns 403 | Role Cashier/Kellner → 403 | Policy "AdminUsers" requires Administrator |
| **UM-INT-03** | GET /api/admin/users with Administrator returns 200 and list | Valid JWT with role Administrator → 200, array | |
| **UM-INT-04** | POST /api/admin/users/{id}/deactivate with valid auth writes audit log | After deactivate, AuditLog contains action USER_DEACTIVATE, EntityType User, target in RequestData/EntityName | Verify via DbContext or audit API |
| **UM-INT-05** | POST /api/admin/users/{id}/reactivate writes audit log | Entry with USER_REACTIVATE | |
| **UM-INT-06** | POST /api/admin/users (create) writes audit log | USER_CREATE or equivalent | |
| **UM-INT-07** | PATCH /api/admin/users/{id} (role change) writes audit and triggers session invalidation | Audit entry; IUserSessionInvalidation.InvalidateSessionsForUserAsync(id) called | Mock session invalidation and assert call |
| **UM-INT-08** | POST deactivate calls session invalidation for target user | After successful deactivate, InvalidateSessionsForUserAsync(targetId) invoked | Mock IUserSessionInvalidation |
| **UM-INT-09** | POST force-password-reset calls session invalidation | InvalidateSessionsForUserAsync(id) invoked | Mock |

**CI command (integration):**
```bash
dotnet test --filter "FullyQualifiedName~UserManagement" --no-build
```
*(Add integration test project or tag if separate.)*

---

## 3. E2E tests (FE-Admin critical flows)

| ID | Description | Pass criteria | Suggested tool |
|----|-------------|----------------|-----------------|
| **UM-E2E-01** | Login as Administrator → Users page loads | 200, table or empty state visible | Playwright / Cypress |
| **UM-E2E-02** | List users: table shows columns Name, Email, Role, Status, Last login, Actions | DOM contains headers or first row with expected cells | |
| **UM-E2E-03** | Filter by role: select role → list updates | Request to API with role param; table rows match filter | |
| **UM-E2E-04** | Create user: open drawer, fill required fields, submit → success message and list refresh | POST to create endpoint; success toast; new user in list or refetch | |
| **UM-E2E-05** | Edit user: open edit drawer, change name, save → success and updated value in table | PATCH sent; success; row shows new name | |
| **UM-E2E-06** | Deactivate: open deactivate modal, enter reason, confirm → user status Inaktiv and success message | Deactivate API called with reason; table shows inactive tag | |
| **UM-E2E-07** | Deactivate without reason: submit disabled or validation error | Reason required; no API call without reason | |
| **UM-E2E-08** | Reactivate: select inactive user, confirm reactivate → status Aktiv | Reactivate API called; status tag green | |
| **UM-E2E-09** | Activity timeline: open user detail/timeline → audit entries visible or empty state | GET /api/AuditLog/user/{id} or equivalent; table or empty message | |
| **UM-E2E-10** | Non-admin: Create/Edit/Deactivate/Reactivate buttons not visible or disabled | Only Activity (and possibly View) available | |

**CI command (E2E):**
```bash
cd frontend-admin && npm run test:e2e -- --grep "User management"
```
*(Implement when E2E suite exists.)*

---

## 4. Security tests

| ID | Description | Pass criteria | Notes |
|----|-------------|----------------|-------|
| **UM-SEC-01** | Privilege escalation: non-Administrator cannot call POST /api/admin/users | Cashier/Kellner token → 403 on create | Policy "AdminUsers" |
| **UM-SEC-02** | Privilege escalation: non-Administrator cannot call POST .../deactivate | 403 for non-admin | |
| **UM-SEC-03** | Privilege escalation: non-Administrator cannot call POST .../reactivate | 403 | |
| **UM-SEC-04** | Privilege escalation: non-Administrator cannot call PATCH .../users/{id} | 403 | |
| **UM-SEC-05** | Token reuse after deactivation: deactivated user’s token rejected on protected endpoints | After deactivate, request with that user’s JWT to e.g. GET /api/admin/users or POS endpoint → 401/403 | Requires auth pipeline to check IsActive or claims |
| **UM-SEC-06** | Forced logout behavior: after deactivate, target user’s refresh tokens invalidated | Stub or real IUserSessionInvalidation; refresh with target’s token fails after deactivate | Depends on refresh-token implementation |
| **UM-SEC-07** | Force password reset: target user’s sessions invalidated | Same as UM-SEC-06 for force-password-reset | |
| **UM-SEC-08** | Role change (e.g. Admin → Cashier): that user’s sessions invalidated | InvalidateSessionsForUserAsync called on PATCH when role changed | Unit or integration with mock |

**CI:** Run integration tests with different role tokens; add security test project or tags.

---

## 5. Compliance tests

| ID | Description | Pass criteria | Notes |
|----|-------------|----------------|-------|
| **UM-COMP-01** | Historical receipt records still show responsible operator after deactivation | Receipt.CashierId remains set; GET receipt (or list) returns cashier id/display; no nulling of CashierId on user deactivation | Receipt model: CashierId not changed by user lifecycle |
| **UM-COMP-02** | Audit log immutability: no update or delete of existing AuditLog rows by user management flow | No endpoint or service method that updates/deletes AuditLog by id or by user | AuditLogService only Add; no Update/Delete in controller for audit |
| **UM-COMP-03** | Deactivation stores reason and actor for audit | ApplicationUser.DeactivationReason, DeactivatedAt, DeactivatedBy set and persisted | Unit test or integration |
| **UM-COMP-04** | User lifecycle audit entry contains target user identifier | LogUserLifecycleAsync creates record with EntityType User, EntityName or RequestData containing target user id | AuditLogService.LogUserLifecycleAsync; EntityName = targetUserId |
| **UM-COMP-05** | No hard delete of users: UserManager.DeleteAsync never called in Admin or UserManagement controllers | Code search: no DeleteAsync(user) in user management paths | Grep / static check |

**CI:** Unit + integration; UM-COMP-01 can be DB assertion after deactivate; UM-COMP-02/05 code/spec.

---

## 6. CI summary (single checklist)

Run in order:

```text
# 1. Backend unit (user management business rules)
dotnet test backend/KasseAPI_Final.Tests/KasseAPI_Final.Tests.csproj --filter "FullyQualifiedName~AdminUsersControllerTests"

# 2. Backend integration (auth + audit) — when implemented
dotnet test --filter "Category=Integration&UserManagement"

# 3. E2E FE-Admin user flows — when implemented
cd frontend-admin && npm run test:e2e -- --grep "users|User management"

# 4. Security: 403 for non-admin on admin endpoints (integration)
# 5. Compliance: receipt CashierId unchanged; audit append-only (unit/integration)
```

**Exit code:** CI should fail if any of the above test runs fail.

---

## 7. Optional test implementations (reference)

- **UM-UNIT-04** — `Deactivate_WhenActorIsTarget_ReturnsBadRequest`: Implemented in `AdminUsersControllerTests`. Create controller with `actorId: "u1"`, user id `"u1"`, call `Deactivate("u1", { Reason: "x" })`, assert 400 and BusinessRule.
- **UM-UNIT-05** — `Deactivate_WhenUserAlreadyDeactivated_ReturnsBadRequest`: Implemented in `AdminUsersControllerTests`. User with `IsActive = false`, call Deactivate, assert 400 BusinessRule.
- **Integration:** Use `WebApplicationFactory<Program>` and `HttpClient` with JWT (Administrator vs Cashier); assert status 401/403/200 and audit log rows.

---

*Checklist version: 1.0. Scope: User management compliance (RKSV/DSGVO), no hard delete, deactivation constraints, audit writes, session invalidation, receipt operator preservation, audit immutability.*
