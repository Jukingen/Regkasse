# Users Feature – İlgili Dosyalar ve Veri Akışı

## 1. Users list data source

### Akış

```
UsersPage
  → useUsersList(listParams)   [hook]
       → getUsersList(params)  [gateway → usersApi]
            → customInstance({ url: '/api/UserManagement', method: 'GET', params })
                 → AXIOS_INSTANCE (baseURL + request interceptor: Authorization Bearer token)
  ← listData: { items: UserInfo[], pagination }
  → users = listData?.items ?? []
  → Table dataSource={users} columns=[..., lastLoginAt, actions]
```

### İlgili dosyalar

| Katman | Dosya | Rol |
|--------|--------|-----|
| Sayfa | `frontend-admin/src/app/(protected)/users/page.tsx` | listParams üretir, useUsersList(listParams) çağırır, users = listData?.items, Table’a verir |
| Hook | `frontend-admin/src/features/users/hooks/useUsersList.ts` | useQuery(listQueryKey, getUsersList), listData döner |
| Gateway | `frontend-admin/src/features/users/api/usersGateway.ts` | getUsersList = usersApi.getUsersList re-export |
| API | `frontend-admin/src/features/users/api/usersApi.ts` | getUsersList: GET /api/UserManagement, query params (page, pageSize, role, isActive, query), dönüş UsersListResponse |
| HTTP | `frontend-admin/src/lib/axios.ts` | customInstance → AXIOS_INSTANCE, request interceptor’da authStorage.getToken() ile Authorization: Bearer |
| Backend | `backend/Controllers/UserManagementController.cs` | GET api/UserManagement, [Authorize(Policy = "UsersView")], UsersListResponse { Items (UserInfo[]), Pagination } |
| Backend DTO | Aynı controller içi | List projection: Id, UserName, Email, FirstName, LastName, EmployeeNumber, Role, TaxNumber, IsActive, CreatedAt, **LastLoginAt** (Notes yok) |

### Liste item şekli (UserInfo)

- Backend list Select’te **Notes yok**. JSON’da camelCase: id, userName, email, firstName, lastName, employeeNumber, role, taxNumber, isActive, createdAt, **lastLoginAt**.

---

## 2. User detail data source

### Akış

```
UsersPage: View / Activity tıklanınca setDetailUser(record)
  → detailUser = record (tablo satırı = listData.items[i])
  → UserDetailDrawer open={!!detailUser} user={detailUser}
  → UserDetailDrawer: user.lastLoginAt, user.notes vb. gösterir
```

- **Detail panel veri kaynağı:** Liste satırı (record). Ayrı GET /api/UserManagement/{id} çağrılmıyor; yani detail’da da list DTO kullanılıyor (Notes yok, lastLoginAt list’te varsa görünür).

### İlgili dosyalar

| Katman | Dosya | Rol |
|--------|--------|-----|
| Sayfa | `frontend-admin/src/app/(protected)/users/page.tsx` | detailUser state, setDetailUser(record), UserDetailDrawer’a user={detailUser} |
| Component | `frontend-admin/src/features/users/components/UserDetailDrawer.tsx` | user prop (UserInfo), lastLoginAt: user.lastLoginAt ? toLocaleString('de-DE') : '—', notes vb. |

---

## 3. Edit drawer state flow

### Akış (mevcut / fix sonrası)

```
Edit tıklanınca: setEditUser(record)
  → editUser = record (liste satırı)
  → editUserId = editUser?.id
  → useQuery(getUserByIdQueryKey(editUserId), getUserById(editUserId), enabled: !!editUserId)
       → getApiUserManagementId(id) = GET /api/UserManagement/{id}
  ← editUserFull (tam UserInfo, Notes dahil), editUserLoading
  → UserFormDrawer open={!!editUser} mode="edit"
      user={editUserFull ?? undefined}
      initialLoading={!!editUserId && editUserLoading}
  → UserFormDrawer: initialLoading ise Spin; değilse user varsa useEffect ile form.setFieldsValue(user); create ise resetFields()
```

- **Edit form veri kaynağı:** GET /api/UserManagement/{id} sonucu (editUserFull). Liste satırı sadece editUser state ve editUserId için; forma giren veri tam DTO.
- Create: open=true, mode='create', user undefined → resetFields(). Edit: open=true, user yüklenene kadar initialLoading, user gelince setFieldsValue.

### İlgili dosyalar

| Katman | Dosya | Rol |
|--------|--------|-----|
| Sayfa | `frontend-admin/src/app/(protected)/users/page.tsx` | editUser state, setEditUser(record), editUserId, useQuery(getUserById), editUserFull / editUserLoading, UserFormDrawer’a user + initialLoading |
| Gateway | `frontend-admin/src/features/users/api/usersGateway.ts` | getUserById(id) = getApiUserManagementId(id), getUserByIdQueryKey(id) |
| Generated | `frontend-admin/src/api/generated/user-management/user-management.ts` | getApiUserManagementId(id) → customInstance GET /api/UserManagement/${id} |
| Component | `frontend-admin/src/features/users/components/UserFormDrawer.tsx` | mode, user, initialLoading; useEffect(open, mode, user): edit && user → setFieldsValue, create → resetFields; edit && initialLoading → Spin |
| Backend | `backend/Controllers/UserManagementController.cs` | GET api/UserManagement/{id}, UserInfo (Notes + LastLoginAt dahil) |

---

## 4. Reset password API flow

### Akış

```
Reset password modal: Speichern
  → resetPasswordForm.validateFields()
  → resetPasswordMutation.mutate({ id: resetPasswordUser.id, data: { newPassword } })
       → gatewayResetPassword(id, data)
            → authStorage.getToken(); token yoksa Promise.reject(401-like)
            → putApiUserManagementIdResetPassword(id, data, { headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` } })
                 → customInstance({ url: `/api/UserManagement/${id}/reset-password`, method: 'PUT', data, headers })
                      → AXIOS_INSTANCE (interceptor da token ekler; gateway ekstra header ile garanti ediyor)
  → Backend: PUT /api/UserManagement/{id}/reset-password
       [Authorize] [Authorize(Policy = "UsersManage")]
       ResetPasswordRequest { NewPassword } ([JsonPropertyName("newPassword")])
       → 400 (id/self/body/şifre kuralı) / 404 (user null veya inactive) / 403 (Admin→SuperAdmin) / 200 Ok
  ← 401 ise JWT OnChallenge: 401 + JSON body { message, code: "UNAUTHORIZED" }
```

### İlgili dosyalar

| Katman | Dosya | Rol |
|--------|--------|-----|
| Sayfa | `frontend-admin/src/app/(protected)/users/page.tsx` | resetPasswordUser state, resetPasswordForm, resetPasswordMutation (gatewayResetPassword), onError 401/403/404 fallback |
| Gateway | `frontend-admin/src/features/users/api/usersGateway.ts` | resetPassword: token kontrolü, putApiUserManagementIdResetPassword(id, data, { headers: Authorization, Content-Type }) |
| Generated | `frontend-admin/src/api/generated/user-management/user-management.ts` | putApiUserManagementIdResetPassword(id, resetPasswordRequest, options) → PUT /api/UserManagement/${id}/reset-password |
| HTTP | `frontend-admin/src/lib/axios.ts` | customInstance, request interceptor (Bearer token) |
| Auth | `frontend-admin/src/features/auth/services/authStorage.ts` | getToken() = localStorage.getItem('rk_admin_access_token') |
| Backend | `backend/Controllers/UserManagementController.cs` | PUT {id}/reset-password, ResetPasswordRequest, policy UsersManage, 200/400/403/404 |
| Backend auth | `backend/Program.cs` | JwtBearerEvents OnChallenge: 401 JSON body |

---

## Özet tablo

| Veri / Akış | Birincil kaynak | İkincil / not |
|-------------|------------------|----------------|
| List | GET /api/UserManagement (params) | usersApi.getUsersList → listData.items |
| Detail panel | Liste satırı (detailUser = record) | GET by id yok |
| Edit form | GET /api/UserManagement/{id} (editUserFull) | Edit tıklanınca useQuery ile çekilir |
| Reset password | PUT /api/UserManagement/{id}/reset-password | Gateway’de token zorunlu + header açıkça eklenir |
