# CartController Hard-Coded Role Check — Report

**Odak:** `backend/Controllers/CartController.cs` — force-cleanup endpoint.

---

## 1) Eski risk neydi

- Force-cleanup endpoint’inde yetki kontrolü **`userRole != "Admin" && userRole != "Kasiyer"`** gibi hard-coded string’lerle yapılıyordu.
- **"Kasiyer"** canonical role değil; token ve policy tarafında rol adı **"Cashier"**.
- Bu yüzden **Cashier** rolüne sahip kullanıcılar token’da `role: "Cashier"` taşıdığı halde, kontrolde "Kasiyer" arandığı için eşleşme olmuyor ve **yanlışlıkla 403 Forbid** alıyordu.
- Admin tarafında literal "Admin" token’daki "Admin" ile tesadüfen uyumluydu; ancak sabit kullanılmadığı için ileride tutarsızlık riski vardı.

---

## 2) Yeni kontrol neden doğru

- **Canonical sabitler:** `Roles.Admin` ve `Roles.Cashier` kullanılıyor (`backend/Authorization/Roles.cs`). Değerler sırasıyla `"Admin"` ve `"Cashier"`; token’daki claim ile aynı.
- **Kontrol:** `allowedForCleanup = new[] { Roles.Admin, Roles.Cashier }` ve `Array.Exists(allowedForCleanup, r => string.Equals(r, userRole, StringComparison.OrdinalIgnoreCase))`. Token’dan gelen `userRole` ("Admin" veya "Cashier") bu liste ile case-insensitive karşılaştırılıyor.
- **Display ayrımı:** WaiterName için kullanılan `"Kasiyer"` fallback’leri yalnızca görüntü amaçlı; ilgili satırlarda "Display fallback only; not used for authorization" yorumu var. Yetki mantığından ayrı.

---

## 3) Cashier ve Admin için beklenen davranış

| Rol (token’da) | Force-cleanup (POST api/cart/force-cleanup) |
|----------------|---------------------------------------------|
| **Admin**     | İzin verilir (200 + cleanup sonucu).       |
| **Cashier**   | İzin verilir (200 + cleanup sonucu).       |
| Diğer (örn. Waiter, Manager) | 403 Forbid. |
| Rol claim yok / boş         | 403 Forbid. |

---

## 4) Rollback nasıl olur

- **Geri alınacak:** `CartController.cs` içinde force-cleanup ile ilgili authorization bloğu (satır ~1029–1035) eski hâline döndürülür.
- **Eski kod (örnek):**  
  `if (userRole != "Admin" && userRole != "Kasiyer") { ... return Forbid(); }`  
  (Not: Bu hâl Cashier kullanıcılarını hatalı reddeder.)
- **Gerekirse:** `using KasseAPI_Final.Authorization` yalnızca bu kontrol için kullanılıyorsa rollback’te kaldırılabilir; başka yerde `Roles.*` kullanımı varsa bırakılır.
- **Doğrulama:** Rollback sonrası Cashier ile force-cleanup çağrısının 403 döndüğü (regresyon) ve Admin ile 200 döndüğü manuel veya test ile kontrol edilir.

---

## Mevcut kod durumu (değişiklik uygulanmış)

- **Using:** `KasseAPI_Final.Authorization` mevcut (satır 4).
- **Force-cleanup yetkisi:** `allowedForCleanup = new[] { Roles.Admin, Roles.Cashier }` ve `Array.Exists(..., StringComparison.OrdinalIgnoreCase)` (satır 1029–1031).
- **WaiterName:** "Kasiyer" yalnızca display fallback; yetki ile ilgili değil, yorumla belirtilmiş (satır 76, 263).

Bu PR’da ek bir kod değişikliği yapılmadı; mevcut hâl hedeflenen canonical role kontrolüne uygun.
