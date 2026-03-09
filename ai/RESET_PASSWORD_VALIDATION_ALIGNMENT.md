# Reset Password Validation Alignment (6 → 8)

## Root cause

- **Gerçek kural:** ASP.NET Identity `Program.cs` içinde `Password.RequiredLength = 8` (ve digit, lower, upper, non-alphanumeric).
- **Çelişki:** Controller/DTO katmanında şifre uzunluğu **6** karakter olarak kullanılıyordu; Identity ise **8** uyguluyordu.
- **Sonuç:** Kullanıcı 6–7 karakter girince frontend ve controller 6’yı kabul ediyor, `UserManager.ResetPasswordAsync` Identity validator’a gidiyor, Identity “Passwords must be at least 8 characters.” dönüyordu.

## Nereden geliyordu?

| Kaynak | Açıklama |
|--------|----------|
| **Identity options** | `Program.cs` → `options.Password.RequiredLength = 8` (tek gerçek kaynak). |
| **Controller/DTO** | `UserManagementController`, `AdminUsersController` ve DTO’larda manuel `Length < 6` ve `[MinLength(6)]` vardı (Identity ile uyumsuz). |
| **Frontend** | `validation.ts` → `PASSWORD_MIN_LENGTH = 6`, `copy.ts` → “min. 6 Zeichen” (backend’e göre yanlış bilgi). |

FluentValidation / DataAnnotations sadece DTO’da kullanılıyordu; asıl şifre politikası Identity’den geliyor.

## Yapılan değişiklikler

### Backend

- **UserManagementController.cs**
  - Reset password: `Length < 6` → `Length < 8`, mesaj “8 characters”.
  - `ResetPasswordRequest`: `MinLength(6)` → `MinLength(8)`.
  - `ChangePasswordRequest`: `MinLength(6)` → `MinLength(8)`.
  - `CreateUserRequest` (CreateUser DTO): `Password` alanı `MinLength(6)` → `MinLength(8)`.
- **AdminUsersController.cs**
  - Force password reset: `Length < 6` → `Length < 8`, mesaj “8 characters”.
  - Create user: `Password.Length < 6` → `Length < 8`.
  - `AdminForcePasswordResetRequest`: `MinLength(6)` → `MinLength(8)`.
  - `AdminCreateUserRequest.Password`: `MinLength(6)` → `MinLength(8)`.

### Frontend

- **validation.ts:** `PASSWORD_MIN_LENGTH = 6` → `8` (yorum Identity ile hizalı).
- **copy.ts:**
  - `newPassword`: “min. 6 Zeichen” → “min. 8 Zeichen”.
  - `validationPasswordMin`: “Min. 6 Zeichen.” → “Min. 8 Zeichen.”.
  - `resetPasswordSecurityNote`: “mindestens 6 Zeichen” → “mindestens 8 Zeichen”.
- **users/page.tsx (reset password onError):** Backend’ten gelen `errors` için hem `newPassword` hem `NewPassword` (camel/Pascal) okunuyor; ilk hata mesajı toast’ta ve form alanında gösteriliyor.

### Testler

- **UserManagementControllerUserLifecycleTests:** “12345” → “1234567” (7 karakter), assertion “6” → “8”.
- **AdminUsersControllerTests:** Kısa şifre “12345” → “1234567”.

## Tutarlılık

- **Backend:** Tüm şifre akışları (reset, force-reset, change, create user) artık **8** karakteri hem manuel kontrolde hem DTO’da hem de Identity’de kullanıyor.
- **Frontend:** Modal metinleri, input label, form kuralları ve validasyon **8** ile uyumlu; backend’ten gelen validation hataları (Identity dahil) toast ve alan hatası olarak doğru gösteriliyor.

## Generated API (orval)

`frontend-admin/src/api/generated/model/resetPasswordRequest.ts` (ve benzeri) orval ile `swagger.json`/OpenAPI’dan üretilir. `backend/swagger.json` içindeki password/newPassword `minLength` değerleri **8** olacak şekilde güncellendi; orval yeniden çalıştırıldığında frontend generated modeller de 8 kullanır. Runtime davranışı `validation.ts` ve copy ile zaten 8’e göre.

## Değiştirilen dosyalar (özet)

| Dosya | Değişiklik |
|--------|------------|
| `backend/Program.cs` | Referans: `RequiredLength = 8` (değişiklik yok). |
| `backend/Controllers/UserManagementController.cs` | Reset/change/create: min length 8, DTOs `MinLength(8)`. |
| `backend/Controllers/AdminUsersController.cs` | Force reset + create user: min length 8, DTOs `MinLength(8)`. |
| `backend/swagger.json` | `newPassword` ve `password` şemalarında `minLength`: 6 → 8. |
| `frontend-admin/.../validation.ts` | `PASSWORD_MIN_LENGTH = 8`, `PASSWORD_POLICY`, policy validator. |
| `frontend-admin/.../copy.ts` | Tüm password metinleri `${PASSWORD_MIN_LENGTH}`; backend hata çevirisi. |
| `frontend-admin/.../users/page.tsx` | Reset modal: 400 hata → field + Alert (DE), toast yalnızca 401/403/404. |
| `frontend-admin/.../UserFormDrawer.tsx` | `passwordPolicyMessage` context’e eklendi. |
| `backend/.../UserManagementControllerUserLifecycleTests.cs` | Kısa şifre testi 8 karaktere göre. |
| `backend/.../AdminUsersControllerTests.cs` | Kısa şifre testi 8 karaktere göre. |
