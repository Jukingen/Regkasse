# Authorization Refactor – Örnek Patch Özeti

**Tarih:** 2025-03-09  
**Hedef:** Seçili controller’larda permission-based guard eklemek; mevcut role-based policy’leri kaldırmadan. Administrator legacy alias ile uyum korunur (RolePermissionMatrix’te Admin ile aynı set).

---

## Genel kurallar (uygulanan)

- Mevcut davranış bozulmadı: `[Authorize(Policy = "...")]` aynen kaldı.
- Role-based yapı tamamen kaldırılmadı; yeni `[HasPermission(AppPermissions.X)]` eklendi (AND ile birlikte geçerli).
- Administrator → Admin permission set (backend RolePermissionMatrix) sayesinde mevcut role kontrolleriyle uyum devam ediyor.
- Kod minimal diff: sadece attribute + gerekli `using` + kısa TODO’lar.

---

## 1. PaymentController

**Dosya:** `backend/Controllers/PaymentController.cs`

| Action | Before | After | Rationale |
|--------|--------|--------|------------|
| Class | `[Authorize(Policy = "PosSales")]` | Aynı (değişmedi) | Legacy role policy korunuyor. |
| CreatePayment (POST) | Zaten `[HasPermission(AppPermissions.PaymentTake)]` | Aynı | Önceden eklenmişti. |
| CancelPayment (POST …/cancel) | Sadece class-level PosSales | + `[HasPermission(AppPermissions.PaymentCancel)]` | Ödeme iptali için açık permission; Cashier/Manager ayrımı ileride permission ile yapılabilir. |
| RefundPayment (POST …/refund) | Sadece class-level PosSales | + `[HasPermission(AppPermissions.RefundCreate)]` | İade için açık permission. |

**TODO eklendi:** CancelPayment içinde `// TODO: scope check – ensure user can cancel this payment (e.g. same branch/cash register or manager).`

**Kod (sadece eklenen satırlar):**

```csharp
[HttpPost("{id}/cancel")]
[HasPermission(AppPermissions.PaymentCancel)]
public async Task<IActionResult> CancelPayment(...)

[HttpPost("{id}/refund")]
[HasPermission(AppPermissions.RefundCreate)]
public async Task<IActionResult> RefundPayment(...)
```

---

## 2. OrdersController

**Dosya:** `backend/Controllers/OrdersController.cs`

| Action | Before | After | Rationale |
|--------|--------|--------|------------|
| Class | `[Authorize(Policy = "PosTableOrder")]` | Aynı | Legacy policy korunuyor. |
| UpdateOrderStatus (PUT …/status) | Zaten `[HasPermission(AppPermissions.OrderUpdate)]` | Aynı | Önceden eklenmişti. |
| DeleteOrder (DELETE …/id) | Sadece class-level PosTableOrder | + `[HasPermission(AppPermissions.OrderCancel)]` | Sipariş iptal/silme için açık permission. |

**TODO eklendi:** DeleteOrder içinde `// TODO: scope check – branch/ownership restriction (e.g. waiter own order, manager any in branch).`

**Kod:**

```csharp
[HttpDelete("{id}")]
[HasPermission(AppPermissions.OrderCancel)]
public async Task<IActionResult> DeleteOrder(Guid id)
```

---

## 3. CompanySettingsController

**Dosya:** `backend/Controllers/CompanySettingsController.cs`

| Action | Before | After | Rationale |
|--------|--------|--------|------------|
| Class | `[Authorize]` | Aynı | Değişmedi. |
| UpdateCompanySettings (PUT) | `[Authorize(Policy = "BackofficeSettings")]` | + `[HasPermission(AppPermissions.SettingsManage)]` | Ayar güncelleme permission ile de korunuyor. |
| UpdateBusinessHours (PUT business-hours) | BackofficeSettings | + HasPermission(SettingsManage) | Aynı mantık. |
| UpdateBankingInfo (PUT banking) | BackofficeSettings | + HasPermission(SettingsManage) | Aynı mantık. |
| UpdateLocalizationSettings (PUT localization) | BackofficeSettings | + HasPermission(SettingsManage) | Aynı mantık. |
| UpdateBillingSettings (PUT billing) | BackofficeSettings | + HasPermission(SettingsManage) | Aynı mantık. |
| ExportCompanySettings (GET export) | BackofficeSettings | + HasPermission(SettingsManage) | Export da ayar kapsamında. |

**Eklenen:** `using KasseAPI_Final.Authorization;`  
**TODO:** UpdateCompanySettings içinde FinanzOnline alanları güncellenirken `// TODO: optional – tenant/branch restriction if multi-tenant; FinanzOnline config is typically tenant-wide.`

---

## 4. AuditLogController

**Dosya:** `backend/Controllers/AuditLogController.cs`

| Action | Before | After | Rationale |
|--------|--------|--------|------------|
| Cleanup (DELETE cleanup) | Zaten `[HasPermission(AppPermissions.AuditCleanup)]` | Aynı + TODO | Önceden vardı. |
| Export (GET export) | `[Authorize(Policy = "AuditAdmin")]` | + `[HasPermission(AppPermissions.AuditExport)]` | Export için açık permission; AuditAdmin policy duruyor. |

**TODO:** Cleanup action üstünde `// TODO: optional – manager approval or audit retention window; branch restriction if multi-tenant.`

**Kod:**

```csharp
[HttpGet("export")]
[Authorize(Policy = "AuditAdmin")]
[HasPermission(AppPermissions.AuditExport)]
public async Task<ActionResult> ExportAuditLogs(...)
```

---

## 5. FinanzOnlineController

**Dosya:** `backend/Controllers/FinanzOnlineController.cs`

| Action | Before | After | Rationale |
|--------|--------|--------|------------|
| Class | `[Authorize(Policy = "BackofficeSettings")]` | Aynı | Legacy policy korunuyor. |
| GetConfig (GET config) | Sadece class | + `[HasPermission(AppPermissions.FinanzOnlineView)]` | Okuma için view permission. |
| UpdateConfig (PUT config) | Sadece class | + `[HasPermission(AppPermissions.FinanzOnlineManage)]` | Config değiştirme için manage. |
| SubmitInvoice (POST submit-invoice) | Sadece class | + `[HasPermission(AppPermissions.FinanzOnlineSubmit)]` | Fatura gönderimi için submit. |

**Eklenen:** `using KasseAPI_Final.Authorization;`  
**TODO:** SubmitInvoice içinde `// TODO: scope – tenant/branch if multi-tenant; submit is typically tenant-scoped.`

---

## 6. InventoryController

**Dosya:** `backend/Controllers/InventoryController.cs`

| Action | Before | After | Rationale |
|--------|--------|--------|------------|
| AdjustInventory (POST …/adjust) | `[Authorize(Policy = "InventoryManage")]` | + `[HasPermission(AppPermissions.InventoryAdjust)]` | Stok düzeltme için özel permission; InventoryManage policy duruyor. |

**Eklenen:** `using KasseAPI_Final.Authorization;`  
**TODO:** Adjust sonrası `// TODO: optional – manager approval for large negative adjustments; branch restriction if multi-tenant.`

**Kod:**

```csharp
[HttpPost("{id}/adjust")]
[Authorize(Policy = "InventoryManage")]
[HasPermission(AppPermissions.InventoryAdjust)]
public async Task<IActionResult> AdjustInventory(Guid id, [FromBody] AdjustInventoryRequest request)
```

---

## Özet tablo

| Controller | Eklenen HasPermission | Korunan policy/policies | TODO konusu |
|------------|----------------------|--------------------------|--------------|
| Payment | PaymentCancel, RefundCreate | PosSales | Cancel: scope (branch/register) |
| Orders | OrderCancel | PosTableOrder | DeleteOrder: scope (branch/ownership) |
| CompanySettings | SettingsManage (tüm PUT + export) | BackofficeSettings | Tenant/branch (FinanzOnline) |
| AuditLog | AuditExport | AuditAdmin, AuditCleanup (mevcut) | Cleanup: manager approval / retention / branch |
| FinanzOnline | FinanzOnlineView, FinanzOnlineManage, FinanzOnlineSubmit | BackofficeSettings | Submit: tenant/branch |
| Inventory | InventoryAdjust | InventoryManage | Adjust: manager approval / branch |

---

## Sonraki adımlar (opsiyonel)

- Scope check: `IScopeCheckService` (tenant_id / branch_id) kullanılarak CancelPayment, DeleteOrder, SubmitInvoice vb. için resource-level kontrol.
- Manager onayı: Büyük negatif inventory adjustment veya audit cleanup için ek onay akışı.
- Branch restriction: Multi-tenant/branch senaryoda ilgili endpoint’lerde branch_id eşlemesi.

Bu refactor, mevcut role’leri bozmadan permission tabanlı geçişe zemin hazırlar; backend’de JWT’ye `permission` claim’leri eklendiğinde aynı attribute’lar hem role hem permission ile çalışır (PermissionAuthorizationHandler + RolePermissionMatrix).
