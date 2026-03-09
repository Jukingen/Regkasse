# Admin Reset Password – Tam Çözüm Özeti

## Mevcut mimari (değişiklik yapılmadan korundu)

Endpoint ve akış zaten projede vardı; sadece testler ve frontend hata mesajları tamamlandı.

---

## Backend

### Endpoint

- **Route:** `PUT /api/UserManagement/{id}/reset-password`
- **Controller:** `UserManagementController.ResetPassword(string id, [FromBody] ResetPasswordRequest request)`
- **Yetki:** `[Authorize]` (class) + `[Authorize(Policy = "UsersManage")]` → SuperAdmin, Admin, Administrator, BranchManager

### Request DTO

```json
{ "newPassword": "string" }
```

- C#: `ResetPasswordRequest` with `[JsonPropertyName("newPassword")]`, `[Required]`, `[MinLength(6)]`
- Şifre kuralları Identity’den (uppercase, lowercase, digit, non-alphanumeric, min 8; controller ek olarak min 6 kontrol ediyor)

### Response sözleşmesi

| Durum | HTTP | Body örneği |
|--------|------|-------------|
| Başarı | 200 | `{ "message": "Password reset successfully" }` |
| Id boş | 400 | `{ "message": "User id is required.", "code": "VALIDATION_ERROR" }` |
| Kendine reset | 400 | `{ "message": "Cannot force-reset your own password...", "code": "VALIDATION_ERROR", "errors": { "NewPassword": [...] } }` |
| Body/şifre boş veya &lt; 6 karakter | 400 | `{ "message": "...", "code": "VALIDATION_ERROR" }` veya `PASSWORD_RESET_FAILED` |
| Kullanıcı yok / inactive | 404 | `{ "message": "User not found" }` |
| Admin, SuperAdmin’i resetlemeye çalışıyor | 403 | `{ "code": "...", "reason": "...", "requiredPolicy": "UsersManage", ... }` |
| Token yok / geçersiz | 401 | `{ "message": "Token missing or invalid...", "code": "UNAUTHORIZED" }` (JWT OnChallenge) |

### Akış (controller içi)

1. Id / self / request / NewPassword validasyonu → 400
2. `_userManager.FindByIdAsync(id)` → null veya `!user.IsActive` → 404
3. SuperAdmin hedef + actor SuperAdmin değil → 403
4. `GeneratePasswordResetTokenAsync` + `ResetPasswordAsync` → Identity hata → 400
5. `TryLogUserLifecycleAsync(FORCE_RESET_PASSWORD, ...)` (audit)
6. `_sessionInvalidation.InvalidateSessionsForUserAsync(id)` (session invalidation)
7. `return Ok(new { message = "Password reset successfully" })`

Service ayrı bir sınıfa taşınmadı; mantık controller’da, Identity `UserManager` ile (hash, token, reset) ve mevcut `IAuditLogService` / `IUserSessionInvalidation` kullanılıyor.

---

## Frontend

### API client

- **Gateway:** `resetPassword(id, data)` in `usersGateway.ts` → token yoksa hemen reject; token varsa `putApiUserManagementIdResetPassword(id, data, { headers: { 'Content-Type': 'application/json', Authorization: Bearer … } })`
- **Generated:** `putApiUserManagementIdResetPassword` (PUT, `/api/UserManagement/${id}/reset-password`, body `ResetPasswordRequest`)

### UI

- Users sayfasında tablo aksiyonunda “Passwort zurücksetzen” → modal açılır
- Modal: hedef kullanıcı adı, güvenlik notu, “Neues Passwort” alanı, min 6 karakter validasyonu, Speichern
- Başarı: `message.success(usersCopy.successResetPassword)`, liste invalidate, modal kapanır
- Hata: `normalizeError` + status’a göre fallback mesaj, 400’de `NewPassword` alan hatası set edilir

### Status’a göre mesajlar

| Status | Copy key | Kullanım |
|--------|----------|----------|
| 401 | `sessionExpiredOrUnauthorized` | “Sitzung abgelaufen oder nicht angemeldet. Bitte erneut anmelden.” |
| 403 | `errorResetPasswordForbidden` | “Keine Berechtigung, dieses Passwort zurückzusetzen.” |
| 404 | `errorResetPasswordUserNotFound` | “Benutzer wurde nicht gefunden.” |
| 400 / diğer | `errorResetPassword` veya sunucu mesajı | Toast + gerekirse form alan hatası |

---

## Unit testler (backend)

`UserManagementControllerUserLifecycleTests` içinde:

- `ResetPassword_WhenTargetIsSelf_ReturnsBadRequest` (mevcut)
- `ResetPassword_WhenAdminResetsSuperAdmin_Returns403` (mevcut)
- `ResetPassword_WhenUserNotFound_Returns404` (yeni)
- `ResetPassword_WhenUserInactive_Returns404` (yeni)
- `ResetPassword_WhenPasswordTooShort_ReturnsBadRequest` (yeni)
- `ResetPassword_WhenRequestNull_ReturnsBadRequest` (yeni)

Çalıştırma: `dotnet test --filter "FullyQualifiedName~UserManagementControllerUserLifecycleTests"`

---

## Değiştirilen / eklenen dosyalar (bu tamamlama için)

| Dosya | Değişiklik |
|--------|------------|
| `backend/KasseAPI_Final.Tests/UserManagementControllerUserLifecycleTests.cs` | 4 yeni test: 404 (user null), 404 (inactive), 400 (short password), 400 (null request) |
| `frontend-admin/src/features/users/constants/copy.ts` | `errorResetPasswordUserNotFound`, `errorResetPasswordForbidden` eklendi |
| `frontend-admin/src/app/(protected)/users/page.tsx` | Reset password `onError`: 403 ve 404 için fallback mesaj atanıyor |

Endpoint, DTO, policy, audit ve session invalidation mevcut mimariyle uyumlu bırakıldı; ek refactor yapılmadı.
