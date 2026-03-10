# Payment identity / demo rejection — validation package

## 1. Exact root cause summary

### Eski bug neydi?
- **Demo kararı** zaten yalnızca **JWT’deki kullanıcı** (`GetCurrentUserId` → `GetUserByIdAsync(userId)`) üzerindeydi; payload `cashierId` ile başka user yüklenmiyordu.
- Buna rağmen **sıra yanlıştı**: önce demo gate, **sonra** cashierId doğrulaması. Body’de **yanlış `cashierId`** varken bile önce demo bloğuna giriliyor, `DiagnosticCode` bazen **`DEMO_BY_FLAG_CASHIER_ID_MISMATCH`** gibi **birleşik** kodlara gidiyordu.
- **Persist tarafı** daha önce `CashierId = request.CashierId` olabiliyordu (auth ile drift); sonra `CashierId = userId` ile tek kaynağa çekildi — bu paketin odak noktası sıra + net 403.

### Neden demo mesajı misleading olabiliyordu?
- Client **farklı user id** gönderdiğinde bile **aynı mesaj** (“Demo users cannot…”) veya **karma kod** görülebiliyordu; asıl problem **kimlik uyumsuzluğu** iken operatör “demo kullanıcı” diye yorumluyordu.
- UI’da rol Cashier görünüp DB’de `is_demo = true` kaldığında zaten demo reddi doğru; ama **cashierId mismatch** ayrı bir hataydı ve demo ile **aynı çatı altında** sunulmamalıydı.

### Yeni akış bunu nasıl düzeltti?
1. **Önce** payload `cashierId` (boş değilse ve placeholder değilse) **auth `userId` ile karşılaştırılıyor** → uyumsuzsa **hemen** `CASHIER_ID_MISMATCH`, **403**, demo mesajı yok.
2. **Sonra** tek resolved user ile demo gate → `DiagnosticCode` yalnızca **`DEMO_BY_FLAG` / `DEMO_BY_ROLE`** (suffix yok).
3. **Controller** mismatch için `details` içinde **`code: "CASHIER_ID_MISMATCH"`** + `diagnosticCode` ile paste/debug kolay.

---

## 2. Changed files summary

### `backend/Services/PaymentService.cs`
- **CreatePaymentAsync** başında **identity-first**: placeholder set dışında `cashierId ≠ userId` → `PaymentResult` + `DiagnosticCode = "CASHIER_ID_MISMATCH"` (demo öncesi).
- Demo reddi **sadece** `GetUserByIdAsync(userId)` sonrası; log tek `RejectionCode` (DEMO_BY_FLAG / DEMO_BY_ROLE).
- Ödeme kaydında **CashierId** auth ile tutarlı kalacak şekilde **`userId`** ile set edilmeye devam (mevcut satır akışı).

### `backend/Controllers/PaymentController.cs`
- **CreatePayment** hata cevabında `DiagnosticCode == "CASHIER_ID_MISMATCH"` → **403** + `details`: `{ code, errors, diagnosticCode }` — client’ın sabit stringle branch etmesi kolay.

---

## 3. Manual QA checklist

| # | Senaryo | Beklenen |
|---|--------|----------|
| 1 | **Valid cashier** — auth id = A, body `cashierId` = A (veya placeholder) | Demo değilse ödeme başarılı; demo ise 400 + demo mesajı + `DEMO_BY_FLAG` / `DEMO_BY_ROLE`. |
| 2 | **Cashier mismatch** — auth id = A, body `cashierId` = B | **403**; mesaj cashier eşleşmesi; `code` + `diagnosticCode` = `CASHIER_ID_MISMATCH`; demo mesajı **yok**. |
| 3 | **Demo user** — `is_demo` true veya legacy `role = Demo`, cashierId doğru/placeholder | **400**; “Demo users cannot create real payments”; kod sadece DEMO_BY_* . |
| 4 | **Non-demo user** — `is_demo` false, rol Cashier vb., cashierId doğru/placeholder | Ödeme akışı devam (müşteri/TSE vb. sonraki kontroller). |
| 5 | **Missing / placeholder cashierId** — `null`, `""`, `UNKNOWN`, `current-user` | Mismatch **değil**; auth user ile demo gate ve devam (eski mobil client uyumu). |

Ek: Başarılı ödeme sonrası DB’de `payment_details` **CashierId** / **created_by** alanlarının **auth user id** ile aynı olduğunu doğrula.

---

## 4. Regression risks

| Risk | Not |
|------|-----|
| **Auth context user missing** | Controller zaten `userId` boşsa **401**; service’a gelmez. |
| **Mobile / old client payloads** | Yanlış `cashierId` gönderen client artık **403** alır (önceden 400 demo veya karma kod). Client’ı **auth user id** gönderecek veya placeholder kullanacak şekilde güncellemek gerekir. |
| **Placeholder cashierId** | `""`, `UNKNOWN`, `current-user` **bilinçli** whitelist — davranış korunur. |
| **payment_details persistence** | CashierId **userId** ile yazılıyor; request body’deki id **artık tek başına persist edilmez** — audit tutarlılığı artar. Eski kayıtlarda created_by ≠ CashierId satırları tarihsel olabilir. |

---

## 5. PR-ready note (paste-ready)

**Title:** Payment POST: identity-first cashierId check + clear 403 CASHIER_ID_MISMATCH

**Summary:**  
Create payment now validates body `cashierId` against the authenticated user **before** the demo gate. Mismatch returns **403** with `code` / `diagnosticCode` **`CASHIER_ID_MISMATCH`** only—no misleading demo message. Demo rejection stays on the **single** user resolved from JWT (`userId`); diagnostic codes are **`DEMO_BY_FLAG`** / **`DEMO_BY_ROLE`** only. Placeholder cashierIds unchanged for backward compatibility.

**Files:**  
- `backend/Services/PaymentService.cs` — reorder + simplify demo diagnostic  
- `backend/Controllers/PaymentController.cs` — 403 details include `code: CASHIER_ID_MISMATCH`

**Risk:** Clients sending a cashierId that doesn’t match the signed-in user will get 403 instead of 400/demo; update client to send JWT user id or omit/use placeholder.
