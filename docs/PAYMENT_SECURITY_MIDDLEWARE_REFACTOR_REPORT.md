# PaymentSecurityMiddleware Refactor Report

## 1. Yeni enforcement modeli

- **Rol listesi yok:** `allowedRoles` veya benzeri hiçbir role listesi kullanılmıyor. Erişim **yalnızca JWT permission claim’leri** ile belirleniyor.
- **Path/action → permission eşlemesi:** Her güvenlik endpoint’i için gerekli permission sabitleri `AppPermissions` üzerinden tanımlı:

| Path / aksiyon      | Gerekli permission (en az biri) |
|---------------------|----------------------------------|
| `/refund`           | `RefundCreate`                   |
| `/cancel`           | `PaymentCancel`                 |
| `/modify`           | `PaymentCancel`                 |
| `/void`             | `PaymentCancel`                 |
| `/reverse`          | `PaymentCancel`                 |
| `/update-status`    | `PaymentTake`                   |
| (eşleşmeyen path)   | Fallback: `PaymentTake` \| `PaymentCancel` \| `RefundCreate` |

- **Kaynak:** Tüm string’ler `AppPermissions.*` sabitlerinden geliyor; hard-coded rol veya permission string’i yok.
- **Fail-closed:** Authenticated değilse, permission claim yoksa veya ilgili path için gerekli permission yoksa → **403**.

---

## 2. Admin lockout nasıl kapandı

- **RolePermissionMatrix:** Admin rolü `PaymentTake`, `PaymentCancel`, `RefundCreate` hepsine sahip (SystemCritical hariç tüm permissions). Token üretilirken bu permission’lar JWT’e yazılıyor.
- **Middleware:** Sadece permission claim’lere bakıyor; rol adı kontrolü yok. Admin’in token’ında bu üç permission olduğu için refund, cancel, modify, void, reverse, update-status hepsine erişebiliyor.
- **Sonuç:** Canonical Admin lockout riski yok; rol listesinde “Admin” unutulma ihtimali de kaldırıldı çünkü artık rol listesi kullanılmıyor.

---

## 3. Hangi payment aksiyonu hangi permission’a bağlandı

| Endpoint / aksiyon   | Permission        | Açıklama                                      |
|----------------------|-------------------|-----------------------------------------------|
| Refund               | `refund.create`   | İade işlemi                                   |
| Cancel               | `payment.cancel`  | Ödeme iptali                                  |
| Modify               | `payment.cancel`  | Ödeme değişikliği (iptal benzeri yetki)       |
| Void                 | `payment.cancel`  | Void işlemi                                   |
| Reverse              | `payment.cancel`  | Reverse işlemi                                 |
| Update-status        | `payment.take`    | Ödeme alma / durum güncelleme                 |

`sale.create` ve `cart.manage` middleware’de kullanılmıyor; payment güvenlik endpoint’leri yalnızca yukarıdaki permission’larla korunuyor.

---

## 4. Waiter erişimi (repo gerçekliği)

- **RolePermissionMatrix’e göre Waiter:** Sadece `PaymentView`, `PaymentTake` var. `PaymentCancel` ve `RefundCreate` **yok**.
- **Sonuç:**
  - **İzin verilir:** `/update-status` (gerekli: `PaymentTake`).
  - **İzin verilmez:** `/refund`, `/cancel`, `/modify`, `/void`, `/reverse` (hepsi `RefundCreate` veya `PaymentCancel` gerektiriyor).

Yani Waiter sadece “ödeme alma / durum güncelleme” tarafına erişebilir; iade ve iptal tarafına erişemez. Bu, mevcut rol matrisi ile uyumlu ve kasada garsonun sadece ödeme alıp durum güncellemesi, iade/iptalin yetkili rollere bırakılması beklentisiyle örtüşüyor.

---

## 5. Hangi testler eklendi / güncellendi

- **InvokeAsync_RefundEndpoint_WhenUserHasRefundCreate_AllowsRequest** — Refund path’inde sadece `RefundCreate` ile geçiş.
- **InvokeAsync_RefundEndpoint_WhenUserHasOnlyPaymentTake_Returns403** — Refund path’inde sadece `PaymentTake` ile 403 (path bazlı enforcement).
- **InvokeAsync_UpdateStatusEndpoint_WhenUserHasPaymentTake_AllowsRequest** — Update-status path’inde `PaymentTake` ile geçiş.
- **InvokeAsync_CancelEndpoint_WhenUserHasPaymentCancel_AllowsRequest** — Cancel path’inde `PaymentCancel` ile geçiş.
- **InvokeAsync_WhenUserHasAllPaymentPermissions_AllowsRefundRequest** — Admin benzeri (üç permission) ile refund’a erişim.
- **InvokeAsync_WhenUserHasNoPaymentPermission_Returns403** — İlgili permission’lar yoksa 403.
- **InvokeAsync_WhenUserHasNoPermissionClaims_Returns403** — Permission claim yoksa 403 (fail-closed).
- **InvokeAsync_WhenUserHasNoPaymentPermission_Returns403** — İlgili permission yok, 403.
- **InvokeAsync_WhenUserHasNoPermissionClaims_Returns403** — Permission claim yok, 403 (fail-closed).
- **InvokeAsync_WhenNotAuthenticated_Returns403** — Unauthenticated 403.

Toplam 8 test. Çalıştırma:

```bash
cd backend
dotnet test --filter "PaymentSecurityMiddlewareTests"
```

---

## Değişen dosyalar

- `backend/Middleware/PaymentSecurityMiddleware.cs` — Rol listesi kaldırıldı; path → permission map eklendi; sadece `AppPermissions` ve JWT permission claim kullanılıyor.
- `backend/KasseAPI_Final.Tests/PaymentSecurityMiddlewareTests.cs` — Path ve permission’a göre senaryolar eklendi/güncellendi.
