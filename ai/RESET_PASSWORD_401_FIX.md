# Reset Password 401 Unauthorized – Fix Özeti

## 1. Root cause

401 **authentication** hatası: İstek endpoint’e ulaşıyor ama JWT doğrulaması başarısız oluyor (token yok veya geçersiz).

- **Endpoint:** `PUT /api/UserManagement/{id}/reset-password` mevcut; route `[Authorize]` + `[Authorize(Policy = "UsersManage")]` ile korunuyor.
- **Olası nedenler:**
  1. **Token hiç gönderilmiyor:** Axios interceptor sadece `config.headers`’a ekliyor; bazı merge/override senaryolarında Authorization kaybolabiliyordu.
  2. **Token süresi dolmuş / geçersiz:** Backend 401 dönüyor ama body boş olduğu için frontend anlamlı mesaj gösteremiyordu.
  3. **Client-side token yok:** `authStorage.getToken()` bazen `null` (ör. sayfa yenilenince veya farklı sekme); bu durumda istek token’sız gidiyordu.

Policy (UsersManage) hatası 403 döner; 401 sadece **kimlik doğrulama** (JWT) aşamasında oluşuyor.

---

## 2. Değiştirilen dosyalar

| Dosya | Değişiklik |
|-------|------------|
| `frontend-admin/src/features/users/api/usersGateway.ts` | `resetPassword`: Token yoksa hemen reject; token varsa isteğe `Authorization: Bearer <token>` ve `Content-Type` header’ları açıkça ekleniyor. |
| `frontend-admin/src/features/users/constants/copy.ts` | `sessionExpiredOrUnauthorized` metni eklendi (401 için kullanıcı mesajı). |
| `frontend-admin/src/app/(protected)/users/page.tsx` | Reset password mutation `onError`: 401’de `sessionExpiredOrUnauthorized` gösteriliyor; 401’de form alan hatası set edilmiyor. |
| `backend/Program.cs` | JWT `OnChallenge`: 401 yanıtı için `HandleResponse()` + JSON body (`message`, `code: "UNAUTHORIZED"`) yazılıyor. |

---

## 3. Kod fix’i (özet)

### Frontend – Gateway

- `authStorage.getToken()` ile token alınıyor.
- Token yoksa (ve `window` varsa): `Promise.reject` ile 401 benzeri hata (mesaj: "Nicht angemeldet. Bitte erneut anmelden.").
- Token varsa: `putApiUserManagementIdResetPassword(id, data, { headers: { 'Content-Type': 'application/json', Authorization: \`Bearer ${token}\` } })` ile istek atılıyor; böylece token her zaman açıkça gönderiliyor.

### Frontend – Page

- Reset password `onError` içinde `response?.status === 401` veya `normalized?.status === 401` kontrolü.
- 401 ise: `normalized.message` için fallback olarak `usersCopy.sessionExpiredOrUnauthorized` kullanılıyor; form alan hatası (NewPassword) set edilmiyor.
- 401 değilse: Önceki davranış (generic/validation mesajı + form hataları) korunuyor.

### Backend – Program.cs

- `JwtBearerEvents.OnChallenge` eklendi.
- `context.HandleResponse()` ile varsayılan challenge davranışı kapatılıyor.
- 401 status, `application/json` content-type ve body: `{ "message": "Token missing or invalid. Please sign in again.", "code": "UNAUTHORIZED" }` (veya `AuthenticateFailure?.Message` varsa o mesaj).

---

## 4. İstek örneği

```http
PUT /api/UserManagement/{userId}/reset-password HTTP/1.1
Host: localhost:5183
Content-Type: application/json
Authorization: Bearer <JWT_TOKEN>

{"newPassword":"123456"}
```

- **Method:** PUT (frontend generated client ile aynı).
- **Body:** camelCase `newPassword`; backend DTO `[JsonPropertyName("newPassword")]` ile eşleşiyor.
- **Auth:** Admin panelden giriş yapılmış kullanıcının JWT’si `Authorization: Bearer` ile gönderilmeli.

---

## 5. Beklenen response örnekleri

### Başarı (204 No Content)

Backend reset-password action’ı şu an `Ok()` veya `NoContent()` dönüyor olabilir; frontend `void` bekliyor, 2xx başarı kabul ediliyor.

### 401 Unauthorized (token yok / geçersiz)

```json
{
  "message": "Token missing or invalid. Please sign in again.",
  "code": "UNAUTHORIZED"
}
```

- Frontend: Bu mesaj veya `sessionExpiredOrUnauthorized` ile toast gösterilir.

### 403 Forbidden (yetki yok – örn. UsersManage policy)

```json
{
  "code": "...",
  "reason": "...",
  "requiredPolicy": "UsersManage",
  "missingRequirement": "Role",
  "correlationId": "..."
}
```

- 401 / 403 ayrımı: 401 = giriş yok/geçersiz token; 403 = giriş var ama bu endpoint için yetki yok.

### 400 Bad Request (validasyon / kendi şifreni resetleme)

- Örn. kendi kullanıcı id’si ile reset: `"Use PUT /api/UserManagement/me/password to change your own password"`.
- Şifre kısa: `"Password must be at least 6 characters."` vb.

---

## 6. Bu bug’ın tekrar etmemesi için

- **Admin reset password** her zaman **Bearer token** ile çağrılsın; gateway’de token yoksa istek atılmadan anlamlı hata verilsin.
- 401’de backend **JSON body** dönsün; frontend 401’i “oturum yok/oturum süresi doldu” olarak yorumlayıp kullanıcıya net mesaj göstersin.
- Reset password ile “kendi şifreni değiştir” (me/password) akışları ayrı kalsın; policy (UsersManage) ve “kendine reset yasak” kontrolleri backend’de olduğu gibi kalsın.
