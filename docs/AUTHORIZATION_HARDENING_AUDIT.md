# Authorization Hardening Sprint – Audit Verification

## Audit Findings vs. Code Verification (File-by-File)

### 1. Legacy admin role (removed); single admin role is Admin

| Location | Finding | Verified |
|---------|---------|----------|
| **backend/Authorization/Roles.cs** | Single admin role is `Admin` | ✅ Only Admin, SuperAdmin, Manager, etc. No legacy constant. |
| **backend/Data/RoleSeedData.cs** | Does not seed legacy role names | ✅ Seeds only canonical roles; comment: single admin is Admin. |
| **backend/Migrations** | Historical data migration and drop | ✅ SQL uses legacy DB value for one-time migration/drop only; not an active role. |
| **backend/Auth/RoleCanonicalization.cs** | No legacy alias mapping | ✅ GetCanonicalRole only trims. |
| **frontend / frontend-admin** | Canonical roles only | ✅ roles and guards use Admin, SuperAdmin; no legacy role. |

**Conclusion:** System uses only **Admin** (and SuperAdmin). No active code path or constant references the legacy role. Migration SQL keeps the legacy DB value for historical data only.

---

### 2. PaymentSecurityMiddleware "canonical Admin excluded"

| Location | Finding | Verified |
|---------|---------|----------|
| **backend/Middleware/PaymentSecurityMiddleware.cs** | `AllowedPaymentRoles = { SuperAdmin, Admin, Manager, Cashier }` | ✅ Admin **is** included. Audit may have referred to an older version or different interpretation. |

**Conclusion:** Middleware does not exclude Admin. Improvement: make the check permission-based (e.g. `PaymentTake`) instead of role list for permission-first consistency.

---

### 3. FE-Admin PermissionRouteGuard fail-open

| Location | Finding | Verified |
|---------|---------|----------|
| **frontend-admin/src/shared/auth/PermissionRouteGuard.tsx** | When `permissions` is missing or empty, access is allowed | ✅ `allowed = !permissions \|\| permissions.length === 0 ? true : checkRoutePermission(...)` → fail-open. |
| **frontend-admin/src/app/(protected)/layout.tsx** | PermissionRouteGuard not used; only AuthGate wraps children | ✅ Layout uses `<AuthGate mode="protected">{children}</AuthGate>`; no PermissionRouteGuard. |

**Conclusion:** Route-level permission check is both fail-open and not applied. Fix: (1) fail-closed when permissions are missing/empty, (2) wrap protected layout content with PermissionRouteGuard.

---

### 4. Settings / CashRegister / SystemCritical permission enforcement

| Controller / Area | Expected | Verified |
|-------------------|----------|----------|
| **SettingsController** | SettingsView, SettingsManage | ✅ [HasPermission(AppPermissions.SettingsView)] GET, [HasPermission(AppPermissions.SettingsManage)] PUT and others. |
| **CompanySettingsController** | SettingsView, SettingsManage | ✅ [HasPermission(AppPermissions.SettingsView)] GET, [HasPermission(AppPermissions.SettingsManage)] on write actions. |
| **CashRegisterController** | CashRegisterView, CashRegisterManage | ✅ [HasPermission(AppPermissions.CashRegisterView)] GET, [HasPermission(AppPermissions.CashRegisterManage)] POST. |
| **InvoiceController** (e.g. permanent delete) | SystemCritical | ✅ [HasPermission(AppPermissions.SystemCritical)] on critical action. |
| **EntityController** | SystemCritical | ✅ [HasPermission(AppPermissions.SystemCritical)] on critical action. |
| **RolePermissionMatrix** | Admin has SettingsManage, not SystemCritical; SuperAdmin has both | ✅ Admin = all except SystemCritical; SuperAdmin = all. |

**Conclusion:** Backend permission enforcement for these areas is in place. No change required for controller attributes.

---

### 5. Permission-first partial; test phase readiness

| Area | Status |
|------|--------|
| Backend policies | One policy per permission (PermissionCatalog.All); [HasPermission] on controllers. |
| Token claims | TokenClaimsService builds permission claims from RolePermissionMatrix. |
| FE-Admin | Menu and route permissions defined; guard is fail-open and not used in layout. |
| POS (frontend) | usePermission has role shortcuts (isAdmin, etc.); PermissionHelper uses roles for screen access. |

**Conclusion:** Permission-first is partially applied. To reach test-phase readiness: enforce fail-closed route guard in FE-Admin, optionally make middleware permission-based, and ensure no hard-coded role strings (use Roles.* / constants).

---

## Implementation Order (Smallest Safe Steps)

1. **FE-Admin: PermissionRouteGuard** – Fail-closed when permissions missing/empty; use in protected layout.
2. **PaymentSecurityMiddleware** – Optionally use permission (e.g. PaymentTake) instead of role list.
3. **Legacy role removal** – Migration drops legacy admin role from AspNetRoles; seed uses Admin only.
4. **Constants** – Replace hard-coded role strings with Roles.* (backend) and shared constants (FE/FE-Admin).
5. **PR summary** – Document changed files, risk, test impact, rollback.

---

## Files Touched (Summary)

- **Backend:** RoleSeedData.cs, UserSeedData.cs, PaymentSecurityMiddleware.cs, new migration (drop Administrator role), AuthController/TokenClaimsService (fallback role constant if desired).
- **Frontend-Admin:** PermissionRouteGuard.tsx, (protected)/layout.tsx, routePermissions.ts (no change needed for map).
- **Frontend (POS):** usePermission.ts, RoleGuard.tsx, PermissionHelper.ts (use constants; prefer permission where possible).
- **Docs:** This file, plus PR summary at end of sprint.
