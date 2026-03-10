# Role Management – Production Rollout Plan

**Scope:** System roles (matrix unchanged), custom role permissions (AspNetRoleClaims), new UI (Rollen verwalten drawer). No change to receipt/TSE/daily-closing or fiscal logic.

---

## 1) DB impact

- **Tables:** No new tables. Uses existing **AspNetRoleClaims** (Identity).
- **Schema:** `RoleId`, `ClaimType`, `ClaimValue` — permission claims use `ClaimType = "permission"`, `ClaimValue = permission key` (e.g. `sale.view`).
- **Impact:** New rows only when a SuperAdmin sets or updates permissions for a **custom** role via PUT `/api/UserManagement/roles/{roleName}/permissions`. System roles are not written here.
- **Pre-deploy:** Confirm AspNetRoleClaims exists (Identity migrations). No new EF migration required for this feature.

---

## 2) Veri migrasyonu

- **Gerek yok.** Mevcut sistem rolleri sadece RolePermissionMatrix’ten okunuyor; custom roller bugün claimsiz ise ilk kayıt UI/API ile yapılınca claims yazılır.
- **İsteğe bağlı:** Eski bir “custom role + manuel permission” senaryosu varsa ve başka bir store’da tutuluyorsa, tek seferlik bir script ile AspNetRoleClaims’e aktarılabilir; mevcut kod böyle bir kaynak beklemiyor.

---

## 3) Backward compatibility

- **JWT:** TokenClaimsService artık hem matrix (sistem rolleri) hem AspNetRoleClaims (custom roller) kullanıyor. Sistem rolü kullanan kullanıcıların token’ı davranış olarak aynı (matrix aynı).
- **Custom rol kullanan kullanıcılar:** Önceden bu roller için permission claim üretilmiyordu (matrix’te yok). Deploy sonrası custom role permission’lar claims’ten gelir; ilk kez set edilene kadar boş kalır, mevcut davranış korunur.
- **API:** Yeni endpoint’ler eklenmiş (GET catalog, GET with-permissions, PUT permissions, DELETE role). Eski client’lar bu endpoint’leri çağırmıyorsa etkilenmez.
- **Frontend:** “Rollen verwalten” butonu sadece yetkili (SuperAdmin/canManageRoles) kullanıcıda görünür. Eski bookmark’lar veya doğrudan URL ile erişim yok; sadece Users sayfasından açılıyor.

---

## 4) Feature flag önerisi

- **Seçenek A (önerilen):** Feature flag yok; deploy ile birlikte aç. Risk düşük (sadece SuperAdmin, yeni endpoint’ler).
- **Seçenek B:** Backend’de app settings ile role management API’yi kapat:
  - `"RoleManagement:Enabled": true/false` — false iken GET with-permissions / GET catalog 503 veya boş döner, PUT/DELETE 503. Frontend’de “Rollen verwalten” butonu aynı ayara göre gizlenir (admin config’den okunur veya env).
- **Seçenek C:** Sadece frontend’de flag; butonu gizle, backend açık kalsın. API doğrudan çağrılmadığı sürece kullanılmaz.

---

## 5) Rollback planı

- **Kod rollback:** Backend ve frontend’i önceki sürüme döndür (deploy pipeline ile).
- **Veri:** AspNetRoleClaims’e yazılan permission claim’ler rollback sonrası okunmaz (eski kod RoleManager’dan claim okumuyordu). Silmek zorunlu değil; kalırsa sonraki “role management” tekrar açıldığında aynı veri kullanılır.
- **İstenirse veri temizliği:** Sadece `ClaimType = 'permission'` olan satırları silen SQL (veya küçük bir script). Sistem rolleri AspNetRoles’ta kalır, sadece claim satırları gider.
- **Sıra:** Önce frontend rollback (buton kaybolur), sonra backend (API 404/405). DB’de kalacak claim’ler sorun çıkarmaz.

---

## 6) Audit logging

- **Mevcut:** ROLE_PERMISSIONS_UPDATE ve ROLE_DELETE, `LogSystemOperationAsync` ile AuditLog’a yazılıyor (EntityType = Role, actor, description, requestData).
- **Kontrol:** Deploy sonrası bir kez SuperAdmin ile rol izni güncelle + rol sil; AuditLog tablosunda Action = ROLE_PERMISSIONS_UPDATE / ROLE_DELETE ve ilgili Role/description kayıtlarını doğrula.
- **Eksik olan (opsiyonel):** Request body’deki permission listesinin tam snapshot’ı (NewValues/OldValues) şu an description/requestData ile sınırlı; tam before/after istersen RoleManagementService’te audit parametreleri genişletilebilir.

---

## 7) Security kontrolleri

- **PUT/DELETE:** Controller’da `IsCurrentUserSuperAdmin()` ile 403; service’te sistem rolü / rol yok / atanmış kullanıcı kontrolleri.
- **Deploy sonrası doğrulama:** Admin hesabı ile PUT veya DELETE çağrısı → 403 beklenir. Sadece SuperAdmin ile 200 (veya iş kuralına göre 400/404/409).
- **Permission key:** Sadece PermissionCatalogMetadata’daki key’ler kabul; geçersiz key → 400. Catalog, RolePermissionMatrix/PermissionCatalog ile uyumlu.
- **Frontend:** canEditRolePermissions / canDeleteRole sadece SuperAdmin (veya ROLE_MANAGE). “Rollen verwalten” butonu canManageRoles ile; backend yine 403 ile korur.

---

## 8) Monitoring / alerting

- **Metrikler:** PUT `/api/UserManagement/roles/*/permissions` ve DELETE `/api/UserManagement/roles/*` için istek sayısı, 4xx/5xx oranı, latency.
- **Alert:** Aynı endpoint’lerde 5xx artışı veya sürekli 403 (yetkisiz denemeler) — örneğin 1 dakikada N adet 403 = uyarı (opsiyonel).
- **Log:** 403 cevaplarında mevcut correlation id ile log; audit’te ROLE_* aksiyonlarının Success/Failed durumu takip edilebilir.
- **DB:** AspNetRoleClaims tablosunda satır sayısı (büyüme yavaş olmalı; sadece custom rol sayısı × ortalama permission sayısı).

---

## 9) Manual verification steps after deploy

1. **Login:** SuperAdmin ile giriş yap; Users sayfasında “Rollen verwalten” butonunun göründüğünü kontrol et.
2. **Drawer:** Butona tıkla; roller listesi ve sağda permission gruplarının yüklendiğini kontrol et (ilk rol seçili, sistem rolleri “System” etiketli).
3. **Sistem rolü:** Admin veya Manager seç; “Rolle löschen” disabled ve tooltip’te sistem rolü uyarısı olsun; permission checkbox’ları disabled olsun.
4. **Custom rol:** Varsa custom rol seç (yoksa “Rolle anlegen” ile yeni rol oluştur). Preset (örn. “Rapor Görüntüleme”) uygula, “Berechtigungen speichern” ile kaydet; success toast ve liste yenilenmesi.
5. **Delete:** Kullanıcısı olmayan bir custom rol seç, “Rolle löschen” → onay → success; listeden silindiğini ve sonraki rolün seçildiğini kontrol et.
6. **Yetki:** Admin ile giriş yap; “Rollen verwalten” butonunun görünmediğini kontrol et (veya görünüp PUT/DELETE’in 403 döndüğünü API ile dene).
7. **Audit:** AuditLog’da ROLE_PERMISSIONS_UPDATE ve ROLE_DELETE kayıtlarının oluştuğunu doğrula.
8. **JWT:** Custom rol atanmış bir kullanıcı ile login; token’da ilgili permission claim’lerin geldiğini kontrol et (debug veya /me benzeri endpoint ile).

---

**Doc version:** 1.0 — Role management (matrix + AspNetRoleClaims + UI) için production rollout.
