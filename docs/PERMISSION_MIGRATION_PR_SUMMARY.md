# Permission-First Migration — PR Summary

**Current model:** [architecture/FINAL_AUTHORIZATION_MODEL.md](architecture/FINAL_AUTHORIZATION_MODEL.md). Permission-first migration is **complete**; all controllers use `[HasPermission]`; legacy policies are **not** registered. Single admin role is **Admin** (no Administrator).

**Scope (historical):** Permission migration prep; ReportsController and others now use HasPermission. Full endpoint map: [ENDPOINT_PERMISSION_MAP_FINAL.md](ENDPOINT_PERMISSION_MAP_FINAL.md).

---

## 1) Mevcut permission altyapısı (kısa özet)

- **AddPermissionPolicies** (`AuthorizationExtensions.cs`): Her `PermissionCatalog.All` elemanı için `Permission:{permission}` policy’si kayıtlı.
- **HasPermissionAttribute:** `[HasPermission(AppPermissions.X)]` → `Policy = "Permission:X"`.
- **PermissionAuthorizationHandler:** JWT permission claim’leri + RolePermissionMatrix ile değerlendirme.
- **RolePermissionMatrix:** Rol → permission eşlemesi; SuperAdmin ve Admin tüm permission’lara sahip. Yeni permission eklenmedi; catalog bu PR’da büyük değişiklik görmedi.
- **Legacy role policies:** Artık kayıtlı değil. Tüm koruma `[HasPermission]` ile; tek admin rolü **Admin**.

---

## 2) Kategori bazlı legacy policy → candidate permission map

| Kategori | Legacy policy | Candidate permission (örnek) |
|----------|----------------|------------------------------|
| Users | AdminUsers, UsersView, UsersManage | `user.view`, `user.manage` |
| POS | PosSales, PosTableOrder | `sale.create`, `payment.take`, `order.create`, `table.manage`, `cart.manage` |
| Catalog | PosCatalogRead, CatalogManage | `product.view`, `category.view`, `modifier.view`; `*.manage` |
| Inventory | InventoryManage, InventoryDelete | `inventory.manage`, `inventory.view` |
| Audit | AuditView, AuditViewWithCashier, AuditAdmin | `audit.view`, `audit.export`, `audit.cleanup` |
| TSE | PosTse, PosTseDiagnostics | Rol policy kalsın veya ileride `tse.*` |
| Settings / Reports | BackofficeManagement, BackofficeSettings | `report.view` (ReportsController’da uygulandı), `settings.view` / `settings.manage`, `cashregister.manage` |

Detay: `docs/architecture/PERMISSION_MIGRATION_PREPARATION.md`.

---

## 3) Düşük riskli ilk migration önerisi (uygulandı)

- **Controller:** ReportsController.
- **Eski:** `[Authorize(Policy = "BackofficeManagement")]` (SuperAdmin, Admin, Manager).
- **Yeni:** `[HasPermission(AppPermissions.ReportView)]`.
- **Gerekçe:** `report.view` RolePermissionMatrix’te ReportViewer, Manager, Admin, SuperAdmin’de var. Erişim bilinçli olarak ReportViewer’a açıldı (read-only); risk düşük.

---

## 4) ReportsController migrate edilince davranış etkisi

- **Önce:** Sadece SuperAdmin, Admin, Manager report endpoint’lerine erişebiliyordu.
- **Sonra:** ReportViewer da aynı endpoint’lere erişebiliyor (Permission:report.view). Manager, Admin, SuperAdmin erişimi aynı.
- **Değişmeyen:** Route, DTO, response, domain logic. Sadece class-level authorization attribute değişti.

---

## 5) Hangi testler bunu güvence altına alıyor

| Garanti | Test(ler) |
|---------|-----------|
| **Token role ve permission** | RoleCanonicalizationTests (trim/identity); RolePermissionMatrixTests (Admin full set). |
| **Policy’de Admin erişimi** | UserManagementAuthorizationPolicyTests (Admin/SuperAdmin pass UsersView/UsersManage). |
| **Payment middleware / Admin** | RolePermissionMatrixTests (`RoleHasPermission_Admin_Has_PaymentTake_So_PaymentMiddleware_Allows_Admin`); PermissionAuthorizationHandlerTests (Admin has PaymentTake). |
| **ReportView / ReportsController** | RolePermissionMatrixTests (ReportViewer has ReportView); PermissionAuthorizationHandlerTests (permission policy evaluation). |

**Komut:**  
`dotnet test --filter "FullyQualifiedName~RolePermissionMatrixTests|FullyQualifiedName~UserManagementAuthorizationPolicyTests|FullyQualifiedName~PermissionAuthorizationHandlerTests|FullyQualifiedName~RoleCanonicalizationTests"`

**Son çalıştırma:** 44 passed, 0 failed, 0 skipped.

---

## 6) Bu PR’da yapılan değişiklikler

| Alan | Değişiklik |
|------|------------|
| **Kod** | ReportsController zaten `[HasPermission(AppPermissions.ReportView)]`; bu PR’da ek controller migration yok. |
| **PERMISSION_MIGRATION_PREPARATION.md** | Altyapı özeti eklendi; Settings tablosunda ReportsController satırı "Permission:report.view (migrated)" olarak güncellendi; Örnek migration bölümü "applied" + davranış etkisi + hangi testler; Phase 1 işaretlendi. |
| **AUTHORIZATION_REGRESSION_TEST_REPORT.md** | Refactor safety tablosu "Guarantee" odaklı yeniden yazıldı; "Auth test coverage (guarantees)" bölümü eklendi (canonicalization, Admin access, payment middleware). |
| **PERMISSION_MIGRATION_PR_SUMMARY.md** | Bu özet doküman eklendi. |

Permission catalog ve AppPermissions bu PR’da değiştirilmedi. Minimal diff; test odaklı.
