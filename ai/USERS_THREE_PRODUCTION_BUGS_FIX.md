# Users Feature – 3 Production Bug Fix Özeti

## 1. Root cause özeti

### Bug 1: Last login list ve detail’de görünmüyor

- **Sebep:** Login sonrası `ApplicationUser.LastLoginAt` hiç güncellenmiyordu. `AuthController.Login` sadece token üretip dönüyordu; DB’de alan hep `null` kaldığı için liste ve detail boş görünüyordu.
- **Ek:** Backend list/GET-by-id DTO’da `LastLoginAt` zaten vardı; frontend `lastLoginAt` okuyordu. Eksik olan tek nokta login sırasında bu alanın yazılmasıydı.
- **Nullable/format:** Veri yoksa frontend "—" gösteriyor; varsa `toLocaleString('de-DE')` ile formatlanıyor.

### Bug 2: Edit drawer açıldığında alanlar boş

- **Sebep:** Edit için sadece **liste satırı** (list DTO) kullanılıyordu. List projection’da **Notes** yoktu; ayrıca drawer `destroyOnClose` + useEffect ile açıldığında bazen `user` henüz gelmeden `resetFields()` çalışıp form boş kalıyordu.
- **Çözüm:** Edit tıklanınca **GET /api/UserManagement/{id}** ile tam kullanıcı çekiliyor; forma sadece bu tam DTO ile `setFieldsValue` yapılıyor. Sadece create modunda `resetFields()` çağrılıyor; edit’te veri gelene kadar loading gösteriliyor.

### Bug 3: Reset password 401 Unauthorized

- **Sebep:** 401 = authentication hatası (token yok veya geçersiz). İstekte Authorization header’ının gitmemesi veya JWT’nin geçersiz/expired olması; backend 401’de body olmadan dönünce frontend anlamlı mesaj da gösteremiyordu.
- **Çözüm:** Gateway’de reset password çağrısında token yoksa hemen reject; token varsa **Authorization: Bearer** ve Content-Type header’ları açıkça ekleniyor. Backend’de JWT **OnChallenge** ile 401’de JSON body dönülüyor. Frontend’de 401/403/404 için ayrı fallback mesajlar kullanılıyor.

---

## 2. Yapılan değişiklikler

### Last login

- **Backend:** `AuthController.Login` içinde şifre doğrulandıktan sonra `user.LastLoginAt = DateTime.UtcNow`, `user.LoginCount++`, `_userManager.UpdateAsync(user)`. Hata olsa bile login başarılı dönüyor (audit alanı login’i bloke etmiyor).
- **Frontend:** Detail panelde last login yokken "—" (liste ile tutarlı).

### Edit drawer

- **Gateway:** `getUserById(id)` ve `getUserByIdQueryKey(id)` eklendi; generated `getApiUserManagementId(id)` kullanılıyor.
- **Page:** Edit için `editUser?.id` ile `useQuery(getUserById)`; dönen `editUserFull` edit drawer’a `user` olarak veriliyor; `initialLoading={!!editUserId && editUserLoading}` ile loading state geçiliyor.
- **UserFormDrawer:** `initialLoading` prop; sadece `mode === 'create'` iken `resetFields()`; edit’te sadece `user` varken `setFieldsValue`; edit + loading iken "Laden…" spinner.

### Reset password

- **Gateway:** `resetPassword` içinde token yoksa anlamlı reject; token varsa `putApiUserManagementIdResetPassword(id, data, { headers: { 'Content-Type': 'application/json', Authorization: \`Bearer ${token}\` } })`.
- **Backend:** JWT `OnChallenge`: `HandleResponse()`, 401 status, `application/json`, body `{ "message": "...", "code": "UNAUTHORIZED" }`.
- **Frontend:** Reset password mutation `onError`: 401 → `sessionExpiredOrUnauthorized`, 403 → `errorResetPasswordForbidden`, 404 → `errorResetPasswordUserNotFound`; 400’de sunucu mesajı + NewPassword form hatası.

---

## 3. Değişen dosyalar

| Dosya | Değişiklik |
|-------|------------|
| **backend/Controllers/AuthController.cs** | Login başarılı olunca `LastLoginAt`, `LoginCount` güncellemesi ve `UpdateAsync`. |
| **backend/Program.cs** | JWT `OnChallenge`: 401 için JSON body, `HandleResponse()`. |
| **frontend-admin/src/features/users/api/usersGateway.ts** | `getApiUserManagementId` import; `getUserById`, `getUserByIdQueryKey`; `authStorage` import; `resetPassword` içinde token kontrolü ve açık Authorization/Content-Type header. |
| **frontend-admin/src/app/(protected)/users/page.tsx** | `useQuery` import; `getUserById`, `getUserByIdQueryKey`; `editUserId` + `editUserFull` / `editUserLoading`; edit drawer’a `user={editUserFull ?? undefined}`, `initialLoading`; reset password `onError` içinde 401/403/404 fallback. |
| **frontend-admin/src/features/users/components/UserFormDrawer.tsx** | `initialLoading` prop; edit’te sadece `user` varken `setFieldsValue`, sadece create’te `resetFields()`; edit + `initialLoading` iken Spin. |
| **frontend-admin/src/features/users/components/UserDetailDrawer.tsx** | Last login yokken "—" (NA yerine). |
| **frontend-admin/src/features/users/constants/copy.ts** | `sessionExpiredOrUnauthorized`, `errorResetPasswordUserNotFound`, `errorResetPasswordForbidden` metinleri. |
| **backend/KasseAPI_Final.Tests/UserManagementControllerUserLifecycleTests.cs** | ResetPassword için 4 test: UserNotFound 404, UserInactive 404, PasswordTooShort 400, RequestNull 400. |

---

## 4. Test / doğrulama adımları

### Last login

1. Backend’i çalıştır; admin panelden bir kullanıcı ile login ol.
2. Users sayfasında listede "Letzter Login" kolonunda o kullanıcı için tarih/saat görünmeli (de-DE formatında).
3. Başka bir kullanıcıda hiç login yoksa "—" görünmeli.
4. Aynı kullanıcıya View/Details ile gir; detail panelde "Letzter Login" aynı şekilde dolu veya "—" olmalı.

### Edit drawer

1. Users listesinde bir kullanıcıda "Bearbeiten" (Edit) tıkla.
2. Kısa süre "Laden…" görünüp ardından drawer’da firstName, lastName, email, employeeNumber, role, taxNumber, notes alanları mevcut verilerle dolu olmalı.
3. Create (Neuer Benutzer) açıp kapatıp tekrar Edit açınca yine aynı kullanıcı verisi dolu gelmeli (stale state olmamalı).

### Reset password

1. Admin ile giriş yap; Users’ta başka bir kullanıcı için "Passwort zurücksetzen" tıkla.
2. Yeni şifre gir (min 6 karakter); Speichern.
3. Başarı: "Passwort wurde zurückgesetzt. Sitzungen des Benutzers wurden ungültig." toast; modal kapanır.
4. Token’ı silip (localStorage’dan `rk_admin_access_token` kaldır) aynı işlemi dene: "Sitzung abgelaufen oder nicht angemeldet. Bitte erneut anmelden." (veya benzeri) mesajı görünmeli (401).
5. Kendi kullanıcında reset dene: 400 ile "Cannot force-reset your own password..." (veya backend’in döndüğü mesaj) görünmeli.
6. Olmayan bir id ile API’yi doğrudan çağırırsan 404; yetkisiz rol ile 403 davranışı doğrulanabilir.

### Backend unit testler

```bash
cd backend
dotnet test --filter "FullyQualifiedName~UserManagementControllerUserLifecycleTests"
```

- Tüm testler (ResetPassword dahil) geçmeli.
- ResetPassword için: WhenTargetIsSelf_ReturnsBadRequest, WhenAdminResetsSuperAdmin_Returns403, WhenUserNotFound_Returns404, WhenUserInactive_Returns404, WhenPasswordTooShort_ReturnsBadRequest, WhenRequestNull_ReturnsBadRequest.

---

## Özet tablo

| Bug | Root cause | Ana fix |
|-----|------------|--------|
| Last login boş | Login’de LastLoginAt yazılmıyordu | AuthController.Login’de LastLoginAt + LoginCount update |
| Edit alanları boş | Liste DTO + timing; Notes yok | GET-by-id ile tam user; forma sadece bu veri ile setFieldsValue; sadece create’te resetFields |
| Reset 401 | Token gitmiyor / 401 body yok | Gateway’de token zorunlu + header; OnChallenge’da 401 JSON; FE’de 401/403/404 mesajları |

Endpoint, policy ve DTO’lar değiştirilmedi; mevcut mimari ve convention’a uyum korundu.
