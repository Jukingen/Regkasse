# Permission-First Architecture Design (Post Role Cleanup)

**Current model (implemented):** [FINAL_AUTHORIZATION_MODEL.md](FINAL_AUTHORIZATION_MODEL.md). Single admin role: **Admin** (no Administrator). Legacy role policies are **not registered**; controllers use `[HasPermission(AppPermissions.X)]` only.

**Purpose:** Document permission infrastructure and migration path. **Migration is complete**; this doc is kept for design reference.

**References:** `backend/Authorization/AppPermissions.cs`, `PermissionCatalog.cs`, `RolePermissionMatrix.cs`, `HasPermissionAttribute.cs`, `AuthorizationExtensions.cs`

---

## 1. Current state

### 1.1 AppPermissions.cs

| Area | Permissions (existing) |
|------|-------------------------|
| User/Role | `user.view`, `user.manage`, `role.view`, `role.manage` |
| Catalog | `product.view`, `product.manage`, `category.view`, `category.manage`, `modifier.view`, `modifier.manage` |
| Order/Table/Cart/Sale | `order.view`, `order.create`, `order.update`, `order.cancel`, `table.view`, `table.manage`, `cart.view`, `cart.manage`, `sale.view`, `sale.create`, `sale.cancel` |
| Payment | `payment.view`, `payment.take`, `payment.cancel`, `refund.create`, `discount.apply` |
| CashRegister/Shift | `cashregister.view`, `cashregister.manage`, `cashdrawer.open/close`, `shift.view/open/close` |
| Inventory | `inventory.view`, `inventory.manage`, `inventory.adjust` |
| Settings | `settings.view`, `settings.manage`, `localization.view/manage`, `receipttemplate.view/manage` |
| Audit/Report | `audit.view`, `audit.export`, `audit.cleanup`, `report.view`, `report.export` |
| FinanzOnline | `finanzonline.view`, `finanzonline.manage`, `finanzonline.submit` |
| Kitchen | `kitchen.view`, `kitchen.update` |
| Legacy | `price.override`, `receipt.reprint` |

**Present:** `inventory.delete`, `tse.sign`, `tse.diagnostics`, `system.critical` (in AppPermissions and PermissionCatalog). SuperAdmin-only: system.critical, tse.diagnostics, audit.cleanup, inventory.delete.

### 1.2 PermissionCatalog.cs

- `PolicyPrefix = "Permission:"`; policy name = `Permission:{permission}`.
- `PermissionClaimType = "permission"`.
- `All` is built from the full list in `AppPermissions` (no separate list); used to register one policy per permission in `AddPermissionPolicies()`.

### 1.3 RolePermissionMatrix.cs

- **SuperAdmin:** Full set (all permissions including system.critical, tse.diagnostics, audit.cleanup, inventory.delete).
- **Admin:** All except SuperAdmin-only: system.critical, tse.diagnostics, audit.cleanup, inventory.delete.
- Manager: ProductManage, CategoryManage, ModifierManage, InventoryManage, AuditExport, ReportView, etc.; **not** AuditCleanup, InventoryDelete, TseDiagnostics.
- Cashier: SaleCreate, PaymentTake, RefundCreate, CartManage, InventoryView, TseSign; no user.view, no report.view, no settings.
- Waiter: OrderCreate, OrderUpdate, TableManage, SaleCreate, PaymentTake, CartView only (no CartManage, no RefundCreate).
- ReportViewer / Accountant: ReportView, ReportExport, AuditView, etc.

**SuperAdmin-only permissions:** system.critical, tse.diagnostics, audit.cleanup, inventory.delete.

### 1.4 HasPermissionAttribute.cs

- `[HasPermission(permission)]` → `Authorize(Policy = "Permission:" + permission)`.
- No logic change needed for migration; attribute is the migration vehicle.

### 1.5 AuthorizationExtensions.cs

- **Legacy role policies:** **Not registered.** `AddLegacyRolePolicies` is empty or unused.
- **Permission policies:** One policy per entry in `PermissionCatalog.All` via `AddPermissionPolicies()`.
- **Handler:** `PermissionAuthorizationHandler` evaluates requirement against JWT permission claims and role-derived permissions (RolePermissionMatrix).

### 1.6 Controllers: permission-only (current)

All controllers use `[HasPermission(AppPermissions.X)]` (class and/or method level). No legacy policy names (AdminUsers, PosSales, etc.) are in use. See ENDPOINT_PERMISSION_MAP_FINAL.md and FINAL_AUTHORIZATION_MODEL.md for endpoint → permission mapping.

---

## 2. Gaps

| Gap | Description |
|-----|-------------|
| **inventory.delete** | Not in `AppPermissions`. Legacy policy `InventoryDelete` is AdminOnly; today “delete” is enforced by role only. Either add `inventory.delete` (proposal) or document that delete stays as “inventory.manage + AdminOnly” (or same roles as current policy). |
| **TSE permissions** | No `receipt.sign`, `tse.diagnostics` (or similar) in catalog. PosTse / PosTseDiagnostics remain role-only by design until TSE permission model is defined. |
| **system.critical** | No `system.critical` (or equivalent) in catalog. SystemCritical stays role-only; high-risk, leave for last. |
| **Catalog: method-level granularity** | CatalogManage is one policy for product/category/modifier. Permissions `product.manage`, `category.manage`, `modifier.manage` already exist; gap is only in controller usage (still using policy name "CatalogManage" instead of resource-specific permission). |
| **PosSales class-level** | CartController, ReceiptsController use class-level PosSales; method-level already uses `PaymentTake` etc. Gap: class-level migration to a single or combined permission (e.g. `sale.create` or `payment.take`) not yet done. |
| **PosTableOrder class-level** | OrdersController, TableController, CustomerController use class-level PosTableOrder; some methods use `OrderUpdate`/`OrderCancel`. Gap: class-level migration to `order.create` / `table.manage` (or both) not done. |
| **BackofficeSettings scope** | Many endpoints use BackofficeSettings; some already use `SettingsManage` or FinanzOnline permissions. Gap: consistent class vs method use of `settings.view` / `settings.manage` and removal of BackofficeSettings where redundant. |

---

## 3. Proposed permission catalog changes

Candidate mapping from the task (legacy policy → permission) is aligned with **existing** permissions as follows. No catalog renames or new permissions are required for the main migration path except where marked **proposal**.

### 3.1 CatalogManage → product.manage / category.manage / modifier.manage

- **Catalog:** No change. `product.manage`, `category.manage`, `modifier.manage` already exist.
- **Migration:** Replace `[Authorize(Policy = "CatalogManage")]` with resource-specific `[HasPermission(AppPermissions.ProductManage)]` (or Category/Modifier) per endpoint. RolePermissionMatrix already grants these to Manager, Admin, SuperAdmin.

### 3.2 InventoryManage → inventory.manage

- **Catalog:** No change. `inventory.manage` exists.
- **Migration:** Replace `[Authorize(Policy = "InventoryManage")]` with `[HasPermission(AppPermissions.InventoryManage)]`. Matrix already grants to Manager, Admin, SuperAdmin.

### 3.3 InventoryDelete → inventory.delete vs inventory.manage (decision note)

- **Option A (proposal):** Add `inventory.delete` to `AppPermissions` and `PermissionCatalog.All`; grant only to Admin/SuperAdmin in RolePermissionMatrix. Then replace `[Authorize(Policy = "InventoryDelete")]` with `[HasPermission(AppPermissions.InventoryDelete)]`. Clear separation of “manage” vs “delete”.
- **Option B (no catalog change):** Do not add `inventory.delete`. Keep InventoryDelete as legacy role policy (AdminOnly), or require both `inventory.manage` and a role check until a later phase. Document that “delete” is intentionally admin-only and not (yet) a separate permission.

**Recommendation:** Option B for minimal change; add Option A as a **proposal** for a future PR if finer-grained audit is needed.

### 3.4 AuditAdmin → audit.cleanup / audit.export

- **Catalog:** No change. `audit.cleanup`, `audit.export` exist. Matrix: Manager has `AuditExport`, not `AuditCleanup`; Admin has both.
- **Migration:** Endpoints already using `[HasPermission(AppPermissions.AuditCleanup)]` and `[HasPermission(AppPermissions.AuditExport)]` are correct. Remaining `[Authorize(Policy = "AuditAdmin")]` can be replaced by method-level `HasPermission(AuditExport)` or `AuditCleanup` as appropriate.

### 3.5 BackofficeSettings → settings.view / settings.manage

- **Catalog:** No change. `settings.view`, `settings.manage` exist.
- **Migration:** Class-level BackofficeSettings can become `[HasPermission(AppPermissions.SettingsView)]` for read-only controllers or `SettingsManage` where write is required. FinanzOnline already uses `FinanzOnlineView` / `FinanzOnlineManage` / `FinanzOnlineSubmit`; no catalog change.

### 3.6 PosSales → sale.create / payment.take

- **Catalog:** No change. `sale.create`, `payment.take` exist; Cashier/Manager/Admin/SuperAdmin have them in matrix.
- **Migration:** Class-level PosSales can be replaced by `[HasPermission(AppPermissions.SaleCreate)]` or `[HasPermission(AppPermissions.PaymentTake)]` (or both with multiple attributes / composite requirement as needed). PaymentController already uses `PaymentTake`, `PaymentCancel`, `RefundCreate` at method level.

### 3.7 PosTableOrder → order.create / order.update / table.manage

- **Catalog:** No change. `order.create`, `order.update`, `table.manage` exist.
- **Migration:** Class-level PosTableOrder can be replaced by e.g. `[HasPermission(AppPermissions.OrderCreate)]` and/or `[HasPermission(AppPermissions.TableManage)]` depending on controller. OrdersController already uses `OrderUpdate`, `OrderCancel` on methods.

### 3.8 Legacy policies to leave for last (no catalog change in this phase)

- **PosTse:** No TSE permission in catalog. Keep `[Authorize(Policy = "PosTse")]` until a dedicated permission (e.g. **proposal:** `receipt.sign` or `tse.sign`) is added and matrix/controllers are updated.
- **PosTseDiagnostics:** Same; keep role policy. **Proposal** for later: `tse.diagnostics` (AdminOnly in matrix).
- **SystemCritical:** Keep `[Authorize(Policy = "SystemCritical")]`. **Proposal** for later: `system.critical` (AdminOnly) if we ever move to permission-only.

These three remain role-based in the design; no changes to AppPermissions or PermissionCatalog in the current taslak.

---

## 4. Migration order (lowest risk first)

Recommended order so that behavior stays aligned with current role policies and existing RolePermissionMatrix.

| Phase | Target | Rationale |
|-------|--------|-----------|
| **1** | **BackofficeSettings → settings.view / settings.manage** | Many endpoints already use `SettingsManage` or FinanzOnline permissions; class-level swap is low risk. Matrix already restricts SettingsManage to Admin/SuperAdmin where needed. |
| **2** | **CatalogManage → product.manage, category.manage, modifier.manage** | Permissions and matrix already match. Replace policy with `HasPermission` per resource; read-only first (e.g. PosCatalogRead → product.view / category.view / modifier.view), then manage endpoints. |
| **3** | **InventoryManage → inventory.manage** | Single controller; one policy type. Already uses `InventoryAdjust` on one method. Straight swap. |
| **4** | **InventoryDelete** | Keep as legacy policy (Option B) or add `inventory.delete` (Option A) in a separate PR; do not mix with other inventory changes. |
| **5** | **AuditAdmin → audit.cleanup / audit.export** | Methods already use HasPermission for cleanup/export; remove remaining AuditAdmin usage. |
| **6** | **PosSales (class-level) → sale.create / payment.take** | Method-level already permission-based; align class-level with one (or both) permissions. Verify Cashier/Manager still have access. |
| **7** | **PosTableOrder (class-level) → order.create / table.manage** | Same idea; method-level already uses OrderUpdate/OrderCancel. |
| **8** | **PosCatalogRead → product.view, category.view, modifier.view** | Read-only; can be done with or just after CatalogManage. |
| **9** | **CashRegisterManage → cashregister.manage** | Single controller/method; permission exists and matrix is clear. |
| **Last** | **PosTse, PosTseDiagnostics, SystemCritical** | Leave on role policy until TSE/system permission design is agreed and documented. |

---

## 5. Risks

| Risk | Mitigation |
|------|------------|
| **Access widening** | Replacing a role policy with a permission can grant more roles (e.g. ReportViewer + report.view). Intended only where documented (e.g. read-only). For manage/delete, ensure RolePermissionMatrix grants the permission only to the same roles as the legacy policy. |
| **Access narrowing** | If a permission is granted to fewer roles than the legacy policy, existing users may get 403. Before each migration, compare policy role list (e.g. from RoleGroups) with matrix roles for that permission. |
| **TSE / fiscal** | PosTse, PosTseDiagnostics, SystemCritical touch TSE/fiscal flows. Do not migrate until permission design is explicit and compliance (e.g. RKSV) is confirmed. |
| **Matrix vs policy mismatch** | After any new permission or matrix change, run authorization tests (RolePermissionMatrixTests, UserManagementAuthorizationPolicyTests, PermissionAuthorizationHandlerTests) and smoke-test with Cashier, Manager, Admin. |
| **inventory.delete** | If we add `inventory.delete` later, grant only to Admin/SuperAdmin to match current InventoryDelete policy; otherwise keep delete on legacy policy. |

---

## 6. Summary table: legacy policy → permission (this design)

| Legacy policy | Permission(s) | Catalog change? | Migration phase |
|---------------|----------------|-----------------|------------------|
| CatalogManage | product.manage, category.manage, modifier.manage | No | 2 |
| InventoryManage | inventory.manage | No | 3 |
| InventoryDelete | inventory.manage (decision) or **proposal** inventory.delete | Optional (proposal) | 4 |
| AuditAdmin | audit.cleanup, audit.export | No | 5 |
| BackofficeSettings | settings.view, settings.manage | No | 1 |
| PosSales | sale.create, payment.take | No | 6 |
| PosTableOrder | order.create, order.update, table.manage | No | 7 |
| PosCatalogRead | product.view, category.view, modifier.view | No | 8 |
| CashRegisterManage | cashregister.manage | No | 9 |
| PosTse | — | Proposal later: receipt.sign / tse.sign | Last |
| PosTseDiagnostics | — | Proposal later: tse.diagnostics | Last |
| SystemCritical | — | Proposal later: system.critical | Last |

No changes to **AppPermissions.cs**, **PermissionCatalog.cs**, **RolePermissionMatrix.cs**, **HasPermissionAttribute.cs**, or **AuthorizationExtensions.cs** (other than eventual removal of legacy policy registrations when no longer used) are required for this design. The design only clarifies current state, gaps, proposals, migration order, and risks.
