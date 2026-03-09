# Users, Inventory, Reports, Catalog – Permission-First Migration Report

## 1. Taşınan endpoint’ler (yapılan değişiklikler)

| Controller | Endpoint / Action | Önceki | Sonraki |
|------------|-------------------|--------|--------|
| **ReportsController** | GET export/sales | Class ReportView only | Action: [HasPermission(ReportExport)] |
| **ProductController** | POST (Create) | Class ProductView | + [HasPermission(ProductManage)] |
| **ProductController** | PUT {id} (Update) | Class ProductView | + [HasPermission(ProductManage)] |
| **ProductController** | PUT stock/{id} | Class ProductView | + [HasPermission(ProductManage)] |
| **ProductController** | POST {id}/modifier-groups | Class ProductView | + [HasPermission(ProductManage)] |
| **UserManagementController** | (comment) | "e.g. Admin, BranchManager" | "e.g. Admin, Manager" (legacy role kaldırıldı) |

UserManagementController ve InventoryController zaten endpoint bazında HasPermission kullanıyordu; ek attribute değişikliği yapılmadı (sadece yorum düzeltmesi).

---

## 2. Kalan “role-policy” / sadece [Authorize] endpoint’ler

| Controller | Endpoint | Mevcut koruma | Neden permission yok |
|------------|----------|----------------|------------------------|
| **UserManagementController** | PUT me/password | [Authorize] | Kendi şifre değiştirme (self-service); tüm authenticated kullanıcılar yapabilsin diye bilerek permission konmadı. |

Class-level [Authorize] sadece “authenticated” anlamında; rol veya policy adı yok. Tüm diğer User/Inventory/Reports/Catalog endpoint’leri artık açıkça HasPermission ile korunuyor.

---

## 3. Kalan nedenler

- **me/password:** Self-service; Cashier/Waiter gibi UserView olmayan rollerin kendi şifresini değiştirebilmesi için permission zorunlu değil, yalnızca [Authorize] kullanıldı.
- **BranchManager / Auditor:** Kodda policy veya rol listesi olarak kullanılmıyordu; sadece bir XML yorumunda “BranchManager” geçiyordu, “Manager” olarak güncellendi.

---

## 4. Endpoint → permission özeti (ilgili alanlar)

### Users (UserManagementController)

| Endpoint | Permission |
|----------|------------|
| GET list, GetUser, search, roles list | user.view |
| Create, Update, Deactivate, Reactivate, ResetPassword, … | user.manage |
| PUT me/password | — (sadece [Authorize], self-service) |

### Inventory (InventoryController)

| Endpoint | Permission |
|----------|------------|
| GET list, GetById, … | inventory.view |
| Create, Update, adjust | inventory.manage / inventory.adjust |
| Delete | inventory.delete |

### Reports (ReportsController)

| Endpoint | Permission |
|----------|------------|
| GET sales, products, customers, inventory, payments | report.view (class) |
| GET export/sales | report.export |

### Catalog

| Controller | Read | Write / Manage |
|------------|------|-----------------|
| ProductController | product.view (class) | product.manage (Create, Update, UpdateStock, SetProductModifierGroups) |
| AdminProductsController | — | product.manage (class) |
| CategoriesController | category.view (class) | category.manage (action) |
| ModifierGroupsController | modifier.view (class) | modifier.manage (action) |

---

## 5. Test etkisi

- **UserManagementController:** Mevcut testler UserView/UserManage ile principal hazırlıyor; me/password [Authorize] kaldığı için davranış değişmedi.
- **ReportsController:** Export endpoint’i artık ReportExport gerektiriyor. Report export çağıran testlerde principal’a ReportExport permission’ı eklenmeli; aksi halde 403 beklenir.
- **ProductController:** Create, Update, UpdateProductStock, SetProductModifierGroups artık ProductManage gerektiriyor. Bu action’ları çağıran integration/API testlerinde principal’da ProductManage olmalı; yoksa 403.
- **InventoryController / CategoriesController / ModifierGroupsController:** Zaten permission-first; ek değişiklik yok, mevcut testler geçer.

Öneri: ReportsController ve ProductController için permission’lı (ReportExport, ProductManage) principal ile birer authorization veya integration testi eklenebilir; 403 senaryoları için ayrı test yazılabilir.
