# `/api/UserManagement` → `/api/admin/users` Geçiş Planı

Kontrollü API migration: FE-Admin Users ekranını `/api/UserManagement` bağımlılığından `/api/admin/users` standardına taşımak. Kırmadan, etaplı geçiş.

**Hedef backend:** `AdminUsersController` (mevcut). **Not:** List pagination, `query` araması ve `roles` endpoint’leri şu an AdminUsers’ta yok; plan bu boşlukları kapatmayı veya adapter ile yönetmeyi içerir.

---

## 1) Endpoint eşleme tablosu (old → new)

| # | Eski (UserManagement) | Yeni (admin/users) | Durum |
|---|------------------------|--------------------|--------|
| 1 | `GET /api/UserManagement?page&pageSize&role&isActive&query` | `GET /api/admin/users?role&isActive` | **Gap:** Yeni tarafta pagination ve `query` yok. |
| 2 | `GET /api/UserManagement/{id}` | `GET /api/admin/users/{id}` | ✅ 1:1 |
| 3 | `POST /api/UserManagement` | `POST /api/admin/users` | ✅ Body farkı var (bkz. Contract). |
| 4 | `PUT /api/UserManagement/{id}` | `PATCH /api/admin/users/{id}` | ⚠️ Method + body (full vs partial). |
| 5 | `PUT /api/UserManagement/{id}/reset-password` | `POST /api/admin/users/{id}/force-password-reset` | ⚠️ Method + body alan adı. |
| 6 | `PUT /api/UserManagement/{id}/deactivate` | `POST /api/admin/users/{id}/deactivate` | ⚠️ Method farkı (PUT→POST). |
| 7 | `PUT /api/UserManagement/{id}/reactivate` | `POST /api/admin/users/{id}/reactivate` | ⚠️ Method farkı (PUT→POST). |
| 8 | `GET /api/UserManagement/roles` | — | **Gap:** AdminUsers’ta yok. |
| 9 | `POST /api/UserManagement/roles` | — | **Gap:** AdminUsers’ta yok. |
| — | `PUT /api/UserManagement/me/password` | (değişmez) | Kendi şifre değişimi; migration kapsamı dışı (settings sayfası). |

**Policy farkı:**  
- Eski: `UsersView` (SuperAdmin, Admin, Administrator, **BranchManager**, **Auditor**), `UsersManage` (…, **BranchManager**).  
- Yeni: `AdminUsers` (sadece **SuperAdmin**, **Admin**, **Administrator**).  
Geçiş sonrası BranchManager/Auditor bu API’ye erişemez; ya policy genişletilir ya da bilinçli kısıtlama kabul edilir.

---

## 2) Contract farkları (request / response / error)

### 2.1 Liste

| Alan | Eski (UserManagement) | Yeni (AdminUsers) |
|------|------------------------|-------------------|
| **Request** | `page`, `pageSize`, `role`, `isActive`, `query` | Sadece `role`, `isActive` (pagination/query yok) |
| **Response** | `{ items: UserInfo[], pagination: { page, pageSize, totalCount, totalPages } }` | `AdminUserDto[]` (düz dizi, sayfalama yok) |

### 2.2 Tekil kullanıcı (GET by id)

| Alan | UserInfo (eski) | AdminUserDto (yeni) |
|------|------------------|----------------------|
| Id, UserName, Email, FirstName, LastName, EmployeeNumber, Role, TaxNumber, Notes, IsActive, CreatedAt, LastLoginAt | ✅ | ✅ |
| UpdatedAt, DeactivatedAt, **Etag** | ❌ | ✅ (Etag = ConcurrencyStamp, PATCH If-Match için) |

### 2.3 Create user

| Alan | CreateUserRequest (eski) | AdminCreateUserRequest (yeni) |
|------|---------------------------|------------------------------|
| UserName, Password, FirstName, LastName, Role | ✅ | ✅ |
| Email, EmployeeNumber, TaxNumber, Notes | ✅ | ✅ |
| MaxLength / validasyon | Farklı (örn. Email 100 vs 256) | Yeni daha geniş sınırlar |

- **Response:** Eski `UserInfo` (201), yeni `AdminUserDto` (201). Alanlar büyük ölçüde aynı; yeni tarafta UpdatedAt, DeactivatedAt, Etag ek.

### 2.4 Update user

| | Eski | Yeni |
|---|------|------|
| **Method** | PUT | PATCH |
| **Body** | Full: FirstName, LastName, Email, EmployeeNumber, Role, TaxNumber, Notes (hepsi zorunlu değil ama tam güncelleme) | Partial: sadece gönderilen alanlar güncellenir |
| **Concurrency** | Yok | `If-Match: "{etag}"` (412 dönebilir) |
| **Response** | `{ message: "User updated successfully" }` | `AdminUserDto` |

### 2.5 Reset password (force)

| | Eski | Yeni |
|---|------|------|
| **Endpoint** | PUT `/{id}/reset-password` | POST `/{id}/force-password-reset` |
| **Body** | `{ newPassword: string }` | `{ newPassword: string }` (aynı) |
| **Success** | 200 + `{ message }` | 204 No Content |

### 2.6 Deactivate / Reactivate

| | Eski | Yeni |
|---|------|------|
| **Method** | PUT | POST |
| **Deactivate body** | `{ reason: string }` | `{ reason: string }` (aynı) |
| **Reactivate body** | `{ reason?: string }` | `{ reason?: string }` (aynı) |
| **Success** | 200 + `{ message }` | 200 + `AdminUserDto` (deactivate/reactivate) |

### 2.7 Hata formatı (error)

| | Eski (UserManagement) | Yeni (AdminUsers) |
|---|------------------------|-------------------|
| **400** | `{ message: string }` veya `{ message, errors }` | `ApiError`: `{ type, title, status, detail?, errors? }` |
| **403** | `{ code, reason, requiredPolicy?, correlationId? }` | `ApiError` (Forbidden) |
| **404** | `{ message: "User not found" }` | `ApiError.NotFound` (title, detail) |
| **409** | Nadir | `ApiError.Conflict` |
| **412** | Yok | `ApiError.ConcurrencyConflict` (PATCH If-Match) |

FE’deki `normalizeError()` hem `message` hem `ApiError.title/detail/errors` okuyacak şekilde genişletilmeli.

---

## 3) Compatibility adapter tasarımı

### 3.1 Amaç

- Tek bir “users API” katmanı; arkasında **eski** veya **yeni** base URL + davranış seçilebilsin.
- FE bileşenleri (page, hooks, gateway) mümkün olduğunca aynı kalsın; fark adapter’da toplansın.

### 3.2 Önerilen yapı (frontend)

```
features/users/api/
  usersGateway.ts        → Şu anki gibi: createUser, updateUser, getUsersList, ...
  usersApi.ts            → Low-level: URL + method seçimi (old vs new)
  adminUsersApi.ts       → (Yeni) /api/admin/users doğrudan çağrıları, AdminUserDto tipleri
  usersApiAdapter.ts     → (Yeni) useNewUsersApi flag’e göre old vs new çağrı; response/request dönüşümü
```

### 3.3 Adapter davranışı (özet)

| İşlem | Eski path kullanılırken | Yeni path kullanılırken |
|-------|-------------------------|---------------------------|
| **List** | `GET /api/UserManagement` (params aynen) | `GET /api/admin/users` + (backend pagination/query yoksa) **client-side** page/query filtreleme veya backend’e pagination eklenene kadar eski endpoint’e fallback. |
| **Get by id** | GET old | GET new; response `AdminUserDto` → `UserInfo` (Etag/UpdatedAt/DeactivatedAt opsiyonel map). |
| **Create** | POST old, body aynen | POST new; body aynı (AdminCreateUserRequest ≈ CreateUserRequest); response AdminUserDto → UserInfo. |
| **Update** | PUT old, full body | PATCH new, partial body; isteğe If-Match(etag); response AdminUserDto → UserInfo. |
| **Reset password** | PUT `/{id}/reset-password`, `{ newPassword }` | POST `/{id}/force-password-reset`, `{ newPassword }`; 204 → başarı. |
| **Deactivate** | PUT `/{id}/deactivate`, `{ reason }` | POST `/{id}/deactivate`, `{ reason }`; 200 body’yi UserInfo’ya map et veya yok say. |
| **Reactivate** | PUT `/{id}/reactivate`, `{ reason? }` | POST `/{id}/reactivate`, `{ reason? }`. |
| **Roles** | GET/POST old | Backend’e GET/POST roles eklenene kadar **eski** endpoint’e yönlendir (veya ayrı feature-flag). |

### 3.4 Backend tarafında kapatılacak boşluklar (tercih edilirse)

- **GET /api/admin/users:** `page`, `pageSize`, `query` parametreleri; response `{ items: AdminUserDto[], pagination: { page, pageSize, totalCount, totalPages } }`.
- **GET /api/admin/users/roles** ve **POST /api/admin/users/roles:** Mevcut UserManagement roles sözleşmesiyle uyumlu (string[] ve `{ name }`).

Bu yapılırsa adapter sadece URL + method + hafif isim/etag eşlemesi yapar; list için client-side pagination gerekmez.

---

## 4) Feature-flag veya aşamalı rollout planı

### 4.1 Feature-flag (önerilen)

- **Flag adı (ör.):** `USE_ADMIN_USERS_API` (env veya config: `NEXT_PUBLIC_USE_ADMIN_USERS_API=true`).
- **Yer:** `usersApiAdapter.ts` veya `usersApi.ts`; flag’e göre base URL ve fonksiyon seti seçilir (old vs new).
- **Varsayılan:** `false` (eski API); tüm testler ve prod önce eski ile doğrulanır.

### 4.2 Aşamalı rollout

| Aşama | Ne yapılır | Kim / nerede |
|-------|------------|--------------|
| 1 | Backend: AdminUsers’a pagination + query + roles (isteğe bağlı) ekle; testler. | Backend |
| 2 | FE: Adapter + flag ekle; flag=false ile mevcut davranış aynen. | Frontend |
| 3 | FE: Flag=true ile sadece **read** (list + get by id) yeni API’ye geçir; smoke test. | Frontend + QA |
| 4 | FE: Create/Update/ResetPassword/Deactivate/Reactivate’i yeni API’ye geçir; hata mesajı ve 412 (etag) işle. | Frontend |
| 5 | FE: Roles’u yeni API’ye geçir (veya backend roles eklediyse). | Frontend |
| 6 | Prod’da flag’i rollout: önce iç kullanıcılar, sonra %10 → %50 → 100%. | Ops / Release |
| 7 | Flag’i varsayılan true yap; eski endpoint’i deprecate et. | Release |
| 8 | Eski UserManagement endpoint’lerini kaldırma (ayrı release). | Backend |

### 4.3 Geri dönüş (rollback)

- **Anında:** `USE_ADMIN_USERS_API=false` (veya rollout yüzdesini 0’a çek). Tüm trafik tekrar `/api/UserManagement`’a gider.
- **Veri:** Yeni API sadece mevcut kullanıcı/rol verisi üzerinde işlem yaptığı için veri geri dönüşü gerekmez; sadece istek yönü değişir.
- **Süre:** Flag değişikliği deploy/restart ile dakikalar mertebesinde.

---

## 5) Geri dönüş (rollback) stratejisi

| Risk | Önlem | Rollback |
|------|--------|----------|
| Yeni API 5xx / timeout | Circuit breaker veya timeout + fallback: istek eski API’ye düşer (flag’e ek “fallbackOnError”). | Flag=false veya fallback=true. |
| Policy: 403 artışı | AdminUsers policy’si daha dar; BranchManager/Auditor 403 alır. | Ya policy’yi UsersView/UsersManage ile hizala ya da dokümante et; gerekirse flag ile eski API’ye dön. |
| Pagination/query eksik | List performansı veya arama bozulur. | List için geçici olarak eski endpoint kullan (sadece list’i flag’den bağımsız “old” yap) veya backend’e pagination ekleyene kadar yeni list’i açma. |
| Hata formatı | FE `normalizeError` yeni formata göre güncellenmezse kullanıcı yanlış mesaj görür. | normalizeError hem `message` hem `title/detail/errors` okur; rollback = flag false. |
| Concurrency (412) | PATCH’te If-Match yoksa veya etag yanlışsa 412; FE işlemezse kullanıcı “güncelleme başarısız” görür. | 412’yi “Lütfen sayfayı yenileyip tekrar dene” gibi mesajla göster; gerekirse flag false. |

Rollback kararı: Hata oranı veya 4xx/5xx artışı metrik eşiği aşılıyorsa (örn. 5 dakika içinde %5’ten fazla) otomatik veya manuel flag=false.

---

## 6) Ölçülebilir başarı metrikleri

| Metrik | Hedef | Nasıl ölçülür |
|--------|--------|----------------|
| **Liste başarı oranı** | 2xx ≥ %99.5 | FE veya gateway: `GET` list çağrıları sonucu 2xx / toplam. |
| **Mutation başarı oranı** | Create/Update/Reset/Deactivate/Reactivate 2xx ≥ %99 | Her mutation tipi için 2xx / toplam. |
| **Liste gecikmesi (p95)** | Eski API ile aynı veya daha iyi | Backend veya API gateway: GET list p95 latency. |
| **4xx oranı** | Eski ile kıyaslanabilir veya düşük | 403/404/400/412 sayıları; özellikle 403 (policy) artışı izlenir. |
| **5xx oranı** | %0.1’in altında | Backend log / APM. |
| **Rollback sayısı** | 0 (hedef) | Flag’in false’a alınma sayısı (release sonrası 7 gün). |
| **E2E / smoke test** | Tüm kritik akışlar geçer | List, create, edit, deactivate, reactivate, reset password; flag=true ile per release. |

**Kabul kriteri (go/no-go):** Aşama 6’da (prod rollout) 24 saat boyunca yukarıdaki hedefler sağlanıyorsa, flag varsayılan true yapılabilir ve eski endpoint deprecate edilir.

---

## Referanslar

- Backend: `backend/Controllers/UserManagementController.cs`, `backend/Controllers/AdminUsersController.cs`
- Policy: `backend/Program.cs` (UsersView, UsersManage, AdminUsers)
- FE API: `frontend-admin/src/features/users/api/usersApi.ts`, `usersGateway.ts`
- Do-not-touch: `ai/07_DO_NOT_TOUCH.md` (TSE, receipt, daily closing, FinanzOnline, audit – bu migration bunlara dokunmaz)
