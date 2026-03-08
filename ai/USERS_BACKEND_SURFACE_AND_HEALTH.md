# Users Feature – Backend Surface & Health Report

## 1. Backend surface (Users feature)

Tüm endpoint'ler tek controller’da değil; Users ekranının kullandığı API’ler aşağıda toplandı.

### UserManagementController (`api/UserManagement`)

| Method | Route | Policy | Açıklama |
|--------|--------|--------|----------|
| GET | `/` | UsersView | Sayfalanmış kullanıcı listesi (query, role, isActive, page, pageSize) |
| GET | `/{id}` | UsersView | Kullanıcı detayı |
| POST | `/` | UsersManage | Kullanıcı oluştur |
| PUT | `/{id}` | UsersManage | Kullanıcı güncelle (ad, soyad, email, employee number, role, tax, notes) |
| PUT | `/{id}/password` | UsersManage | Başka kullanıcının şifresini mevcut şifre ile değiştir |
| PUT | `/{id}/reset-password` | UsersManage | Zorunlu şifre sıfırlama (admin, kendisi hariç) |
| PUT | `/{id}/deactivate` | UsersManage | Kullanıcı deaktive (reason zorunlu) |
| PUT | `/{id}/reactivate` | UsersManage | Kullanıcı tekrar aktif |
| DELETE | `/{id}` | UsersManage | Soft-delete (deactivate, reason yok) |
| GET | `me/password` | (auth) | Kendi şifresini değiştir (PUT body: current + new password) |
| GET | `roles` | UsersView | Rol listesi (AspNetRoles) |
| POST | `roles` | UsersManage | Rol oluştur |

**Not:** “Search” ayrı route değil; liste `GET /api/UserManagement?query=...&role=...&isActive=...&page=...&pageSize=...` ile yapılıyor.

### AuditLogController (Users ekranından çağrılan)

| Method | Route | Policy | Açıklama |
|--------|--------|--------|----------|
| GET | `user/{userId}` | UsersView | Kullanıcı yaşam döngüsü audit log’ları (sayfalanmış) |

### Diğer (Users ekranıyla ilgili)

| Controller | Route | Kullanım |
|------------|--------|----------|
| UserSettingsController | `api/user/settings` | GET/PUT kullanıcı ayarları (me) |
| AdminUsersController | `api/admin/users` | Alternatif admin kullanıcı listesi/ekleme (farklı surface) |

Policies: **UsersView** = SuperAdmin, Admin, Administrator, BranchManager, Auditor. **UsersManage** = SuperAdmin, Admin, Administrator, BranchManager. (Program.cs)

---

## 2. Endpoint bazlı root cause / durum

### GET /api/AuditLog/user/{id} (öncelikli 500)

- **Root cause:** `audit_logs` tablosunun veritabanında olmaması (migration uygulanmamış veya migration geçmişi farklı).
- **Ne yapıldı:**
  - Startup’ta idempotent `CREATE TABLE IF NOT EXISTS audit_logs` (+ indeksler) çalıştırılıyor (Program.cs). Tablo her ortamda garanti.
  - AppDbContext’te AuditLog → `audit_logs` ve tüm kolonlar (BaseEntity snake_case, diğerleri quoted PascalCase) migration ile hizalandı.
- **Sonuç:** Veri varsa döner, yoksa boş liste; 500 beklenmez.

### GET /api/UserManagement (liste)

- **Root cause riski:** Yok. `UserManager.Users` (AspNetUsers) + filtreler + sayfalama. Tablo Identity ile geliyor.
- **Yapılan:** Yok. Validation: page/pageSize sınırları mevcut.

### GET /api/UserManagement/{id} (user details)

- **Root cause riski:** Boş/geçersiz `id` ile `FindByIdAsync` davranışı veya hata.
- **Yapılan:** `string.IsNullOrWhiteSpace(id)` → 400 BadRequest (VALIDATION_ERROR).

### PUT /api/UserManagement/{id} (update user)

- **Root cause riski:** Email güncellenmiyordu (sadece duplicate kontrolü; alan atanmıyordu). Boş id.
- **Yapılan:**
  - Email değişiyorsa `user.Email` ve `user.NormalizedEmail` atanıyor (Identity lookup uyumlu).
  - Boş/geçersiz `id` → 400.

### PUT /api/UserManagement/{id}/reset-password

- **Root cause riski:** Boş id; aksi halde Identity + audit log (tablo artık var).
- **Yapılan:** Boş `id` → 400.

### PUT /api/UserManagement/{id}/password

- **Yapılan:** Boş `id` → 400.

### PUT /api/UserManagement/{id}/deactivate | reactivate | DELETE /api/UserManagement/{id}

- **Yapılan:** Boş `id` → 400. Audit log yazımı `audit_logs` tablosuna (startup ensure ile güvende).

### GET /api/UserManagement/roles

- **Root cause riski:** AspNetRoles tablosu Identity ile gelir; policy ile korunuyor. Ek risk yok.
- **Yapılan:** Yok.

### POST /api/UserManagement/roles

- **Root cause riski:** ModelState + duplicate role kontrolü. Yeterli.
- **Yapılan:** Yok.

### PUT /api/UserManagement/me/password

- **Root cause riski:** Mevcut kullanıcı + Identity ChangePasswordAsync. Düşük risk.
- **Yapılan:** Yok.

---

## 3. Validation / DTO / entity / migration / query hizalama

- **Validation:** Tüm `{id}` route’ları için `string.IsNullOrWhiteSpace(id)` → 400. AuditLog/user için `userId` zaten kontrol ediliyordu.
- **DTO:** CreateUserRequest, UpdateUserRequest, ResetPasswordRequest (newPassword camelCase), DeactivateUserRequest (Reason zorunlu) mevcut ve kullanılıyor.
- **Entity:** ApplicationUser (Identity + custom kolonlar) AspNetUsers ile uyumlu. AuditLog → `audit_logs`, kolon isimleri migration ve AppDbContext ile birebir.
- **Migration:** `audit_logs` hem migration (20260308175048, 20260308200033) hem startup ensure ile garanti. Diğer Users tarafı Identity default migration’larına dayanıyor.
- **Query:** GetUserAuditLogsAsync / GetUserLifecycleAuditLogsCountAsync `EntityType == USER` ve `EntityName == userId`; tablo ve kolon adları EF mapping ile uyumlu.

---

## 4. Yapılan kod değişiklikleri özeti

| Dosya | Değişiklik |
|-------|------------|
| Program.cs | (Mevcut) Startup’ta `audit_logs` CREATE TABLE IF NOT EXISTS + indeksler |
| AppDbContext.cs | (Mevcut) AuditLog BaseEntity kolonları id, created_at, updated_at, created_by, updated_by, is_active explicit |
| UserManagementController.cs | UpdateUser: request.Email değişiyorsa user.Email + NormalizedEmail atanıyor |
| UserManagementController.cs | GetUser, UpdateUser, ChangePassword, ResetPassword, DeactivateUser, ReactivateUser, DeleteUser: boş/geçersiz id → 400 |

---

## 5. Users backend kısa sağlık raporu

| Alan | Durum | Not |
|------|--------|-----|
| **GET AuditLog/user/{id}** | ✅ Düzeltildi | Tablo + mapping garanti; 500 root cause giderildi. |
| **User list / details** | ✅ Sağlam | Liste ve detay validation (id guard) eklendi. |
| **Update user** | ✅ Düzeltildi | Email + NormalizedEmail kalıcı güncelleniyor; id guard. |
| **Reset password** | ✅ Sağlam | Id guard; audit_logs yazımı güvende. |
| **Audit log (user)** | ✅ Sağlam | Aynı tablo/mapping; veri yoksa boş liste. |
| **Roles / permissions** | ✅ Sağlam | UsersView / UsersManage policy’ler tanımlı; rol listesi ve oluşturma Identity ile. |
| **Validation / DTO** | ✅ Hizalı | Id zorunluluğu ve mevcut DTO’lar tutarlı. |
| **Entity / migration** | ✅ Hizalı | AuditLog ↔ audit_logs birebir; Users tarafı Identity. |

**Özet:** Users feature backend’inde öncelikli 500 (GET AuditLog/user/{id}) root cause seviyesinde giderildi; user details, update (email dahil), reset password, audit log ve role/permission çağrıları tutarlı ve hatalı istekler 400 ile eleniyor. Genel durum: **stabil, kalıcı çözümler uygulandı.**
