# Permission-First Migration Preparation

**Current model:** [FINAL_AUTHORIZATION_MODEL.md](FINAL_AUTHORIZATION_MODEL.md). Migration is **complete**: legacy role policies are **not registered**; all endpoints use `[HasPermission(AppPermissions.X)]` only. Single admin role is **Admin** (no Administrator).

**Purpose:** Historical — preparation for permission-first rollout. Kept for reference.  
**References:** `backend/Authorization/PermissionCatalog.cs`, `RolePermissionMatrix.cs`, `AuthorizationExtensions.cs`, `HasPermissionAttribute.cs`.

---

## 1) Current state (final)

### Permission infrastructure summary (already in place)

- **AddPermissionPolicies:** Registers one policy per permission in `PermissionCatalog.All` (name: `Permission:{permission}`).  
- **HasPermissionAttribute:** `[HasPermission(AppPermissions.X)]` → `Policy = "Permission:X"`.  
- **PermissionAuthorizationHandler:** Evaluates JWT permission claims and falls back to `RolePermissionMatrix` for role-derived permissions.  
- **RolePermissionMatrix:** Single source of truth; SuperAdmin has full set; Admin has all except system.critical, tse.diagnostics, audit.cleanup, inventory.delete.  
- **Legacy role policies:** **Not registered.** Controllers use `[HasPermission]` only. See [FINAL_AUTHORIZATION_MODEL.md](FINAL_AUTHORIZATION_MODEL.md).

### Permission infrastructure (detail)

| Component | File:Line | Notes |
|-----------|-----------|--------|
| Permission list | `PermissionCatalog.cs:22-95` | `PermissionCatalog.All` drives policy registration |
| Policy prefix | `PermissionCatalog.cs:12` | `"Permission:"` → policy name `Permission:{permission}` |
| Permission policies | `AuthorizationExtensions.cs:36-45` | One policy per permission via `AddPermissionPolicies()` |
| Handler | `PermissionAuthorizationHandler` | Evaluates JWT permission claims + RolePermissionMatrix |
| Attribute | `HasPermissionAttribute.cs:11-17` | `[HasPermission(AppPermissions.X)]` → `Policy = "Permission:X"` |

### Endpoints already using permission (HasPermission / Permission:*)

| Controller | Method / Location | Permission | File:Line |
|------------|-------------------|------------|-----------|
| InventoryController | Adjust | `AppPermissions.InventoryAdjust` | `InventoryController.cs:287` |
| FinanzOnlineController | GET view, manage, submit | `FinanzOnlineView`, `FinanzOnlineManage`, `FinanzOnlineSubmit` | `FinanzOnlineController.cs:29,64,151` |
| AuditLogController | Cleanup, Export | `AuditCleanup`, `AuditExport` | `AuditLogController.cs:341,379` |
| CompanySettingsController | Multiple actions | `SettingsManage` | `CompanySettingsController.cs:94,187,245,319,388,425` |
| OrdersController | Update, Cancel | `OrderUpdate`, `OrderCancel` | `OrdersController.cs:171,198` |
| PaymentController | Take, Cancel, Refund | `PaymentTake`, `PaymentCancel`, `RefundCreate` | `PaymentController.cs:78,279,316` |

---

## 2) Historical: legacy policy names (no longer in use)

The following policy names (AdminUsers, UsersView, PosSales, etc.) are **not** registered. All endpoints are protected by `[HasPermission(AppPermissions.X)]`. This section is kept for migration reference only.

### By category (historical)

### Users

| Controller | Current policy | File:Line | Endpoints (class/method) |
|------------|----------------|-----------|---------------------------|
| AdminUsersController | AdminUsers | `AdminUsersController.cs:16` | Class |
| UserManagementController | UsersView, UsersManage | `UserManagementController.cs:116,196,238,335,440,491,580,644,698,751,768` | List/Get, Create/Update/Delete users |
| AuditLogController | UsersView | `AuditLogController.cs:32,158,242,273` | User audit logs |

**Candidate permission policies:**  
- Class / list: `Permission:user.view` (replaces UsersView)  
- Create/Update/Delete: `Permission:user.manage` (replaces UsersManage)  
- AdminUsers: keep or use `Permission:user.manage` (same roles in matrix).

---

### POS (Cart, Sales, Orders, Tables, Payments, Receipts)

| Controller | Current policy | File:Line | Notes |
|------------|----------------|-----------|--------|
| CartController | PosSales | `CartController.cs:17` | Class |
| OrdersController | PosTableOrder | `OrdersController.cs:10` | Class; methods use `OrderUpdate`, `OrderCancel` |
| TableController | PosTableOrder | `TableController.cs:9` | Class |
| CustomerController | PosTableOrder | `CustomerController.cs:16` | Class |
| PaymentController | PosSales, PosTse, PosTseDiagnostics | `PaymentController.cs:27,494,524,580` | Mixed |
| ReceiptsController | PosSales | `ReceiptsController.cs:12` | Class |

**Candidate permission policies:**  
- PosSales → `Permission:sale.create` and/or `Permission:payment.take` (controller-level: require one of, or keep class `Permission:cart.manage`); method-level already uses `PaymentTake`, `PaymentCancel`, `RefundCreate`.  
- PosTableOrder → `Permission:order.create` + `Permission:table.manage` (or single `Permission:order.create` for entry).  
- PosTse → keep or map to a single TSE permission if added (e.g. `receipt.sign`); today no TSE permission in catalog.  
- PosTseDiagnostics → Admin-only; `Permission:settings.view` or dedicated TSE permission.

---

### Catalog (Products, Categories, Modifiers)

| Controller | Current policy | File:Line | Notes |
|------------|----------------|-----------|--------|
| ProductController | PosCatalogRead | `ProductController.cs:22` | Class |
| CategoriesController | PosCatalogRead, CatalogManage | `CategoriesController.cs:13,74,118,165` | Read vs create/update/delete |
| ModifierGroupsController | PosCatalogRead, CatalogManage | `ModifierGroupsController.cs:17,82,116,146,185,304` | Same pattern |
| AdminProductsController | CatalogManage | `AdminProductsController.cs:17` | Class |

**Candidate permission policies:**  
- PosCatalogRead → `Permission:product.view`, `Permission:category.view`, `Permission:modifier.view` (or one of them at class level).  
- CatalogManage → `Permission:product.manage`, `Permission:category.manage`, `Permission:modifier.manage` (method-level by resource).

---

### Inventory

| Controller | Current policy | File:Line | Notes |
|------------|----------------|-----------|--------|
| InventoryController | [Authorize], InventoryManage, InventoryDelete | `InventoryController.cs:11,119,182,226,286,415` | One method already `HasPermission(InventoryAdjust)` |

**Candidate permission policies:**  
- InventoryManage → `Permission:inventory.manage` (and optionally `Permission:inventory.view` for read).  
- InventoryDelete → `Permission:inventory.manage` (or stricter dedicated permission if added).

---

### Audit

| Controller | Current policy | File:Line | Notes |
|------------|----------------|-----------|--------|
| AuditLogController | UsersView, AuditView, AuditViewWithCashier, AuditAdmin | `AuditLogController.cs:32,83,115,158,208,242,273,307,378` | Some methods already HasPermission(AuditCleanup), (AuditExport) |

**Candidate permission policies:**  
- AuditView / AuditViewWithCashier → `Permission:audit.view`.  
- AuditAdmin → `Permission:audit.export` and/or `Permission:audit.cleanup`.  
- UsersView (audit by user) → keep or `Permission:user.view`.

---

### TSE

| Controller | Current policy | File:Line | Notes |
|------------|----------------|-----------|--------|
| TagesabschlussController | PosTse | `TagesabschlussController.cs:12` | Class |
| TseController | PosTse | `TseController.cs:11` | Class |
| PaymentController | PosTse, PosTseDiagnostics | `PaymentController.cs:494,524,580` | TSE-related actions |

**Candidate permission policies:**  
- PosTse → today no dedicated TSE permission in `AppPermissions`; could add e.g. `receipt.sign` or keep role policy until permission is defined.  
- PosTseDiagnostics → Admin-only; `Permission:settings.view` or new `tse.diagnostics`.

---

### Settings (Backoffice)

| Controller | Current policy | File:Line | Notes |
|------------|----------------|-----------|--------|
| FinanzOnlineController | BackofficeSettings | `FinanzOnlineController.cs:13` | Class; methods use HasPermission(FinanzOnline*) |
| CompanySettingsController | BackofficeSettings, BackofficeManagement | `CompanySettingsController.cs:93,186,214,244,318,356,387,424` | Mixed |
| ReportsController | **Permission:report.view** (migrated) | `ReportsController.cs:10-11` | Class; `[HasPermission(AppPermissions.ReportView)]` |
| SettingsController | BackofficeSettings | `SettingsController.cs:79,152,179,210,271,306` | Multiple methods |
| LocalizationController | BackofficeSettings | `LocalizationController.cs:87,260,299,337,376,414` | Multiple methods |
| MultilingualReceiptController | BackofficeSettings | `MultilingualReceiptController.cs:110,178,237,391` | Multiple methods |
| CashRegisterController | [Authorize], CashRegisterManage | `CashRegisterController.cs:13,75` | Class + method |
| InvoiceController | [Authorize], SystemCritical | `InvoiceController.cs:20,885` | SystemCritical on one action |
| EntityController (Base) | SystemCritical | `EntityController.cs:157` | Permanent delete |

**Candidate permission policies:**  
- BackofficeSettings → `Permission:settings.view` at class + `Permission:settings.manage` where needed; FinanzOnline → `Permission:finanzonline.view` / `finanzonline.manage` / `finanzonline.submit`.  
- BackofficeManagement → `Permission:report.view` for reports; company/settings by `Permission:settings.manage`.  
- CashRegisterManage → `Permission:cashregister.manage`.  
- SystemCritical → keep role policy for now or introduce e.g. `system.critical` permission (admin-only in matrix).

---

## 3) Category → candidate permission mapping (summary)

| Category | Legacy policy(ies) | Candidate permission policy (class or method) |
|----------|-------------------|------------------------------------------------|
| Users | AdminUsers, UsersView, UsersManage | `Permission:user.view`, `Permission:user.manage` |
| POS | PosSales, PosTableOrder | `Permission:sale.create`, `Permission:payment.take`, `Permission:order.create`, `Permission:table.manage`, `Permission:cart.manage` |
| Catalog | PosCatalogRead, CatalogManage | `Permission:product.view`, `Permission:category.view`, `Permission:modifier.view`; `Permission:product.manage`, etc. |
| Inventory | InventoryManage, InventoryDelete | `Permission:inventory.manage`, `Permission:inventory.view` |
| Audit | AuditView, AuditViewWithCashier, AuditAdmin | `Permission:audit.view`, `Permission:audit.export`, `Permission:audit.cleanup` |
| TSE | PosTse, PosTseDiagnostics | Keep role policy until `AppPermissions` has TSE permission(s), or `Permission:settings.view` for diagnostics |
| Settings | BackofficeSettings, BackofficeManagement, CashRegisterManage, SystemCritical | `Permission:settings.view`, `Permission:settings.manage`, `Permission:report.view`, `Permission:cashregister.manage`; SystemCritical last |

---

## 4) Example migration (low-risk) — applied

**Chosen:** ReportsController class-level only.  
**Reason:** BackofficeManagement (SuperAdmin, Admin, Manager) previously; `Permission:report.view` is granted to ReportViewer, Manager, Admin, SuperAdmin in RolePermissionMatrix. Switching to `Permission:report.view` **expands** access to ReportViewer (read-only reports), which is desired and low risk.

**Change applied:** `[Authorize(Policy = "BackofficeManagement")]` → `[HasPermission(AppPermissions.ReportView)]` on ReportsController. `using KasseAPI_Final.Authorization` added. No method signatures or routes changed.

**Behavior effect:** ReportViewer role can now access all report endpoints (GET api/reports/*). Manager, Admin, SuperAdmin retain access. Matrix and catalog already define `report.view`; no new permission added.

**Tests that guarantee it:** `RolePermissionMatrixTests.RoleHasPermission_ReportViewer_Has_ReportView`; `PermissionAuthorizationHandlerTests` (permission policy evaluation for role-derived permissions). Full auth filter: `RolePermissionMatrixTests|UserManagementAuthorizationPolicyTests|PermissionAuthorizationHandlerTests|RoleCanonicalizationTests`.

---

## 5) Migration checklist

### Rollout

- [ ] **Phase 0 (done):** Permission policies and handler registered; HasPermission used on selected endpoints.
- [x] **Phase 1:** ReportsController migrated to `Permission:report.view`. Next: other read-only controllers as needed.
- [ ] **Phase 2:** Per-category: replace class-level role policy with one permission policy per controller (Users → user.view / user.manage, Catalog → product.view / category.view / modifier.view, etc.).
- [ ] **Phase 3:** Replace remaining method-level role policies with `[HasPermission(AppPermissions.X)]` where not already present.
- [ ] **Phase 4:** Remove legacy role policies from `AuthorizationExtensions.AddLegacyRolePolicies()` only when no controller uses them (grep `Authorize(Policy = "PosSales")` etc.).
- [ ] **Verification:** Run authorization tests; smoke-test POS, Admin, ReportViewer flows.
- [ ] **Docs:** Update `POS_AUTHORIZATION_SWAGGER_MIGRATION_DESIGN.md` and this file with final policy → permission map.

### Rollback

- [ ] Revert controller attributes from `Policy = "Permission:..."` back to `Policy = "AdminUsers"` / `PosSales` / etc. (per controller).
- [ ] No change to RolePermissionMatrix or token claims; JWT still carries permission claims derived from roles.
- [ ] If a full revert is needed: restore `AuthorizationExtensions.cs` and controller `[Authorize(Policy = "...")]` from version control; redeploy.
- [ ] Verify: same roles must get same access after rollback (no new 403s for previously working users).

### Risk notes

- **Behavior change:** Migrating a controller from a role policy to a permission policy can **widen** access if the permission is granted to more roles than the legacy policy (e.g. ReportViewer + report.view). Intended for read-only/reporting.
- **Narrowing:** Using a stricter permission (e.g. only `settings.manage`) can **restrict** access vs BackofficeSettings (Admin only); test Manager vs Admin after change.
- **TSE / SystemCritical:** Leave on role policy until permission model is explicitly extended and documented.

---

## 6) File reference index

| Topic | File | Lines |
|-------|------|--------|
| Legacy role policies | `backend/Authorization/AuthorizationExtensions.cs` | 51-97 |
| Permission policies registration | `backend/Authorization/AuthorizationExtensions.cs` | 36-45 |
| Permission catalog | `backend/Authorization/PermissionCatalog.cs` | 22-95 |
| Permission names | `backend/Authorization/AppPermissions.cs` | 8-88 |
| Role → permission matrix | `backend/Authorization/RolePermissionMatrix.cs` | 46-143 |
| HasPermission attribute | `backend/Authorization/HasPermissionAttribute.cs` | 11-17 |
