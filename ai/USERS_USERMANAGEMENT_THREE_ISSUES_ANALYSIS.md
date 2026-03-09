# UserManagement: 3 Sorun – Root Cause Analizi ve Fix Planı

**Tarih:** 2025-03-08  
**Kapsam:** Users listesi / User detail / Edit drawer / Reset password akışı (frontend-admin + UserManagementController).

---

## 1. "Last Login / Letzter Login" Görünmüyor

### Trace

| Katman | Dosya | Durum |
|--------|--------|--------|
| **DB** | `ApplicationUser` | `LastLoginAt` (ve `LastLogin`) property var; migration’da `last_login_at` kolonu mevcut. |
| **Backend list** | `UserManagementController.GetUsers` | `UserInfo` projection’da `LastLoginAt = u.LastLoginAt` map ediliyor. |
| **Backend get-by-id** | `UserManagementController.GetUser(id)` | `LastLoginAt = user.LastLoginAt` map ediliyor. |
| **Backend login** | `AuthController.Login` | **LastLoginAt hiç set edilmiyor.** Token üretilip response dönülüyor; kullanıcı entity’si güncellenmiyor. |
| **Frontend list** | `page.tsx` | Kolon: `dataIndex: 'lastLoginAt'`, render: `v ? new Date(v).toLocaleString('de-DE') : '—'`. |
| **Frontend detail** | `UserDetailDrawer.tsx` | `user.lastLoginAt ? new Date(...).toLocaleString('de-DE') : NA`. |
| **FE model** | `userInfo.ts` (generated) | `lastLoginAt?: string | null`. |

### Root cause

- **LastLoginAt değeri login sırasında yazılmıyor.**  
- `AuthController.Login` içinde başarılı girişten sonra `ApplicationUser.LastLoginAt` (ve isteğe bağlı `LoginCount`) güncellenmediği için DB’de hep `null` kalıyor; liste ve detail’de de boş görünüyor.

### Düzeltilecek dosya

- `backend/Controllers/AuthController.cs`  
  - Login başarılı olduktan sonra ilgili kullanıcıyı bulup `LastLoginAt = DateTime.UtcNow` (ve varsa `LoginCount`) set edip `_userManager.UpdateAsync(user)` (veya `_context.SaveChangesAsync`) çağrılmalı.

---

## 2. Edit Drawer Açılınca Form Alanları Dolmuyor

### Trace

| Adım | Kaynak | Gözlem |
|------|--------|--------|
| Edit tıklama | `page.tsx` | `onClick={() => setEditUser(record)}` — `record` tablo satırı = `listData.items[i]`. |
| Liste verisi | `usersApi.getUsersList` | `GET /api/UserManagement` → backend `UsersListResponse { Items, Pagination }`. |
| Backend list projection | `UserManagementController` satır 154–167 | `UserInfo` için: Id, UserName, Email, FirstName, LastName, EmployeeNumber, Role, TaxNumber, IsActive, CreatedAt, **LastLoginAt**. **Notes list projection’da yok.** |
| Edit drawer | `UserFormDrawer.tsx` | `user` prop’u ile `useEffect` içinde `form.setFieldsValue({ firstName: user.firstName ?? '', ... })`. |
| Form alanları | Aynı dosya | firstName, lastName, email, employeeNumber, role, taxNumber, notes. |

### Olası root cause’lar

1. **Liste item’ında Notes yok**  
   Backend list DTO’sunda `Notes` map edilmiyor; edit form’da `notes` her zaman liste kaynaklı kullanıldığında boş kalır.

2. **useEffect / mount zamanlaması**  
   Drawer `destroyOnClose` ile her açılışta form yeniden mount oluyor. `useEffect` bir kez `open=true` ama `user` henüz null/undefined iken çalışırsa `else` dalındaki `form.resetFields()` çalışıyor; sonra `user` gelse bile dependency’ler aynı kalabileceği için tekrar set yapılmayabilir. Bu da alanların boş kalmasına yol açabilir.

3. **Tek kaynak liste**  
   Edit için sadece liste satırı kullanılıyor; GET-by-id ile tam ve güncel kullanıcı (Notes dahil) hiç çekilmiyor.

### Düzeltilecek dosyalar ve yön

- **Backend (opsiyonel, tutarlılık için):**  
  `backend/Controllers/UserManagementController.cs` — GetUsers içindeki `UserInfo` projection’a `Notes = u.Notes` eklenebilir.
- **Frontend (önerilen ana çözüm):**  
  - Edit drawer açıldığında (`editUser` set edildiğinde) **GET `/api/UserManagement/{id}`** ile tam kullanıcıyı çek.  
  - Bu veriyi `UserFormDrawer`’a `user` olarak ver veya form’u bu response ile `setFieldsValue` ile doldur.  
  - Böylece Notes dahil tüm alanlar dolar ve timing/closure kaynaklı boş form riski ortadan kalkar.
- **Frontend:**  
  `frontend-admin/src/features/users/components/UserFormDrawer.tsx` — Edit modunda `user` yokken `resetFields()` yerine set yapmamak; mümkünse edit için her zaman GET-by-id’den gelen `user` kullanılacak şekilde sayfa/drawer akışı düzenlenmeli.

---

## 3. Reset Password 401 Unauthorized

### Trace

| Nokta | Detay |
|-------|--------|
| **Endpoint** | `PUT /api/UserManagement/{id}/reset-password` |
| **Controller** | `UserManagementController.ResetPassword` — `[Authorize]` (class) + `[Authorize(Policy = "UsersManage")]`. |
| **Body** | Frontend: `{ newPassword: "123456" }`. Backend DTO: `ResetPasswordRequest` with `[JsonPropertyName("newPassword")]` → binding doğru. |
| **Auth** | 401 = authentication failure (token yok veya geçersiz). 403 = authorization (policy/role). |
| **Frontend** | `putApiUserManagementIdResetPassword(id, data)` → `customInstance` (axios). |
| **Axios** | `lib/axios.ts`: request interceptor’da `authStorage.getToken()` ile `Authorization: Bearer <token>` ekleniyor. |
| **Token** | `authStorage`: `localStorage.getItem('rk_admin_access_token')`. |

### Root cause (401)

- **401**, policy (UsersManage) hatası değil; **kimlik doğrulama** aşamasında dönüyor.  
- Olası nedenler:  
  1. **Token gönderilmiyor:** Sayfa/ortamda `getToken()` null (ör. SSR, farklı domain, storage temiz).  
  2. **Token süresi dolmuş:** JWT expire; backend 401 döner.  
  3. **Token geçersiz:** İmza/issuer/audience uyumsuzluğu (env farkı, key değişmesi).  
  4. **CORS / preflight:** OPTIONS’ta Authorization gönderilmez; asıl PUT’ta header’ın gidip gitmediği network sekmesinden kontrol edilmeli.

### Düzeltilecek / Kontrol Edilecek

- **Hemen kod değişikliği zorunlu değil:** Önce davranış netleştirilmeli.  
- **Kontrol listesi:**  
  - Reset password isteği atıldığında request header’da `Authorization: Bearer <token>` var mı?  
  - Token süresi dolmuş mu? (JWT decode ile `exp` kontrolü.)  
  - Backend JWT ayarları (SecretKey, Issuer, Audience) ile token üretilen env aynı mı?  
- **İyileştirme (opsiyonel):**  
  - 401’de token yenileme veya “oturum süresi doldu, tekrar giriş yapın” mesajı.  
  - Axios response interceptor’da 401’de login sayfasına yönlendirme (zaten var mı kontrol et).

---

## Özet: Root Cause Listesi

| # | Sorun | Root cause |
|---|--------|------------|
| 1 | Last login görünmüyor | Login sonrası `ApplicationUser.LastLoginAt` (ve varsa login sayacı) güncellenmiyor; DB’de hep null. |
| 2 | Edit drawer form boş | (a) Liste projection’da Notes yok; (b) Edit için sadece liste satırı kullanılıyor, GET-by-id yok; (c) useEffect/mount zamanlaması nedeniyle bazen `resetFields()` çalışıp set yapılmıyor olabilir. |
| 3 | Reset password 401 | Token istekle gitmiyor veya backend tarafından geçersiz sayılıyor (eksik/expired/yanlış imza veya issuer/audience). |

---

## Değiştirilecek Dosyalar (Plan)

| Öncelik | Dosya | Değişiklik |
|---------|--------|------------|
| 1 | `backend/Controllers/AuthController.cs` | Login başarılı olunca `LastLoginAt` (ve isteğe bağlı `LoginCount`) güncelle, `UpdateAsync`/SaveChanges. |
| 2 | `frontend-admin/src/app/(protected)/users/page.tsx` | Edit açıldığında `editUser?.id` ile GET `/api/UserManagement/{id}` çağır; dönen `UserInfo`’yu edit form için kullan (state’e yaz veya drawer’a ver). |
| 2 | `frontend-admin/src/features/users/components/UserFormDrawer.tsx` | Edit modunda formu sadece GET-by-id’den gelen `user` ile doldur; `user` yokken `resetFields()` tetiklememek (veya sayfa tarafında veri gelene kadar drawer’ı “loading” göstermek). |
| 3 (opsiyonel) | `backend/Controllers/UserManagementController.cs` | GetUsers list projection’a `Notes = u.Notes` ekle (liste + edit tutarlılığı). |
| 4 | 401 için | Kod yerine önce network/token ve backend JWT ayarları kontrolü; gerekirse 401’de token yenileme veya login’e yönlendirme. |

---

## Adım Adım Fix Planı

### Adım 1: Last login’i yazmak (backend)

1. `AuthController.cs` içinde login başarılı blokta (token üretmeden hemen önce veya sonra):  
   - `ApplicationUser` için `LastLoginAt = DateTime.UtcNow` (ve varsa `LoginCount += 1`) set et.  
   - `_userManager.UpdateAsync(user)` veya `_context.SaveChangesAsync()` ile kaydet.  
2. Gerekirse `AppDbContext`/`UserManager` kullanımı ve transaction davranışı (login ile aynı request içinde commit) dokümante edilir.  
3. Bir kullanıcıyla giriş yapıp liste/detail’da “Letzter Login” alanının dolduğunu manuel test et.

### Adım 2: Edit drawer’ı GET-by-id ile doldurmak (frontend)

1. **page.tsx:**  
   - `editUser` set edildiğinde (Edit tıklanınca) `editUser?.id` varsa `getApiUserManagementId(editUser.id)` (veya gateway üzerinden GET-by-id) çağır.  
   - Bu çağrıyı `useQuery` ile yap: `queryKey: ['/api/UserManagement', editUser?.id], enabled: !!editUser?.id`.  
   - Dönen tam `UserInfo`’yu (örn. `editingUser` state’i) sadece edit drawer için kullan.  
2. **UserFormDrawer:**  
   - Edit modunda prop olarak bu tam `UserInfo`’yu al (sayfa, query’den gelen veriyi geçirir).  
   - `useEffect` dependency’sini `[open, mode, user, form]` tut; sadece `mode === 'edit' && user` iken `setFieldsValue` yap; `user` yokken `resetFields()` çağırma (veya sayfa “loading” iken drawer’da formu gösterme).  
3. İsteğe bağlı: Drawer açıkken “loading” göstermek için GET-by-id `isLoading` kullanılabilir.

### Adım 3: Liste projection’a Notes eklemek (opsiyonel, backend)

1. `UserManagementController.GetUsers` içinde `.Select(u => new UserInfo { ... })` ifadesine `Notes = u.Notes` ekle.  
2. Böylece liste satırından edit’e geçerken de notes alanı dolu olur; asıl kaynak yine GET-by-id olacak şekilde bırakılabilir.

### Adım 4: Reset password 401 (teşhis + iyileştirme)

1. **Teşhis:**  
   - Browser Network’te PUT `/api/UserManagement/{id}/reset-password` isteğinde `Authorization` header’ı var mı, token decode ile `exp` kontrolü.  
   - Backend JWT ayarları (SecretKey, Issuer, Audience) ile login’de üretilen token uyumlu mu kontrol et.  
2. **İyileştirme:**  
   - 401 döndüğünde kullanıcıya “Oturum süresi doldu” vb. mesaj + login sayfasına yönlendirme.  
   - Gerekirse token yenileme (refresh) akışı ekle (AuthController’da refresh endpoint şu an tam implement değil).

---

## Kısa Dosya Referansları

- **Backend:** `backend/Controllers/UserManagementController.cs`, `backend/Controllers/AuthController.cs`, `backend/Models/ApplicationUser.cs`, `backend/Program.cs` (JWT + UsersManage policy).  
- **Frontend:** `frontend-admin/src/app/(protected)/users/page.tsx`, `frontend-admin/src/features/users/components/UserFormDrawer.tsx`, `UserDetailDrawer.tsx`, `features/users/api/usersGateway.ts`, `features/users/api/usersApi.ts`, `lib/axios.ts`, `features/auth/services/authStorage.ts`.  
- **Generated:** `frontend-admin/src/api/generated/user-management/user-management.ts`, `model/userInfo.ts`, `model/resetPasswordRequest.ts`.

Bu dokümandaki adımlar uygulandıktan sonra last login görünürlüğü, edit formunun dolması ve 401’in nedeninin netleşmesi beklenir.
