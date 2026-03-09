# Authorization Regression Test Report

**Current test matrix:** [AUTHORIZATION_HARDENING_TEST_MATRIX.md](AUTHORIZATION_HARDENING_TEST_MATRIX.md).  
**Auth model:** [architecture/FINAL_AUTHORIZATION_MODEL.md](architecture/FINAL_AUTHORIZATION_MODEL.md). Single admin role: **Admin** (no Administrator).

**Scope:** Permission-first auth tests, PaymentSecurityMiddleware (path/permission), RolePermissionMatrix, SuperAdmin/Admin separation.

---

## 1) Test run

**Command:**
```bash
dotnet test --filter "FullyQualifiedName~RolePermissionMatrixTests|FullyQualifiedName~UserManagementAuthorizationPolicyTests|FullyQualifiedName~PermissionAuthorizationHandlerTests|FullyQualifiedName~EndpointAuthorizationRepresentativeTests|FullyQualifiedName~PaymentSecurityMiddlewareTests"
```

**Expected:** All tests pass (e.g. 97+ backend auth tests). See AUTHORIZATION_HARDENING_TEST_MATRIX.md for exact filters and counts.

---

## 2) Test files and coverage

| Test class | Purpose |
|------------|---------|
| **RolePermissionMatrixTests** | Role → permission mapping; Administrator absent; SuperAdmin vs Admin (system.critical, tse.diagnostics, audit.cleanup, inventory.delete); Admin has PaymentTake. |
| **UserManagementAuthorizationPolicyTests** | user.view / user.manage: Admin, SuperAdmin, Manager (view only) vs Cashier, Waiter denied. |
| **PermissionAuthorizationHandlerTests** | Permission policies: allow/deny by role; SuperAdmin-only permissions (AuditCleanup, InventoryDelete, TseDiagnostics) deny Admin. |
| **EndpointAuthorizationRepresentativeTests** | Representative endpoint permissions: Settings, CashRegister, Inventory, Reports, SystemCritical. |
| **PaymentSecurityMiddlewareTests** | Path-based permission: refund/cancel/update-status; Admin/Cashier/Manager matrix permissions allow; Waiter matrix permissions → 403 on refund. |

---

## 3) Guarantees

| Guarantee | Evidence |
|-----------|----------|
| **Administrator absent** | RolePermissionMatrix has no "Administrator"; GetPermissionsForRoles("Administrator") returns empty. |
| **Admin access (users, payment)** | UserManagementAuthorizationPolicyTests: Admin passes user.view/user.manage. PaymentSecurityMiddlewareTests: Admin matrix permissions allow refund. |
| **SuperAdmin vs Admin** | RolePermissionMatrixTests + PermissionAuthorizationHandlerTests: SystemCritical, TseDiagnostics, AuditCleanup, InventoryDelete for SuperAdmin only; Admin denied. |
| **Permission-first** | All controller protection via `[HasPermission]`; no legacy role policies registered. |

---

## 4) No failing tests

All relevant authorization tests must pass. Single admin role is **Admin**; no Administrator in code or matrix.
