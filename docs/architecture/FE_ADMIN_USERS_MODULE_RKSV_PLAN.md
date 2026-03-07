# FE-Admin Users Module – RKSV/Avusturya Uyum Odaklı Uygulama Planı

**Amaç:** Kasiyer/garson (Kellner) gibi POS kullanıcılarını güvenli, denetlenebilir ve mevzuata uyumlu şekilde yönetmek.

**Önemli not (hukuki):** Bu doküman teknik uyum çerçevesi sunar; resmi hukuki danışmanlık değildir. Üretime çıkmadan önce Avusturya vergi danışmanı (Steuerberater) ve hukuk danışmanı ile doğrulama yapılmalıdır.

---

## 0) Kısa Uyum Çerçevesi (Teknik Tasarım Girdisi)

| Gereksinim | Teknik karşılık |
|------------|-----------------|
| **RKSV / Registrierkassenpflicht** | İşlem kayıtlarının sonradan izlenebilirliği; manipülasyona karşı dayanıklılık. |
| **Belegausgabepflicht / işlem izi** | İşlemi yapan kullanıcı, zaman, terminal bilgisi (audit + receipt cashier_id). |
| **BAO saklama yükümlülüğü** | İşlem kayıtları uzun süre (uygulamada çoğunlukla 7 yıl+) saklanır. |
| **DSGVO (GDPR)** | Veri minimizasyonu, amaç sınırlaması, erişim kontrolü, audit trail, güvenli saklama. |

**Mimari prensip:** Kullanıcıyı fiziksel olarak silme yerine **deactivate + immutable audit**; geçmiş fiş/fatura kayıtlarındaki kullanıcı referansları asla bozulmamalı; “Who did what, when” her kritik işlemde zorunlu.

---

## 1) Mevcut Durum Özeti (İnceleme Sonucu)

### Backend
- **ApplicationUser:** `Role` (string), `IsActive`, `CreatedAt`, `UpdatedAt`; Receipt.CashierId ile fiş izi mevcut.
- **UserManagementController:** `[Authorize(Roles = "Administrator")]`; GET sadece `IsActive == true`; DELETE soft-delete (IsActive = false). **Audit log kullanılmıyor.**
- **AuditLogService:** `LogUserActivityAsync` ve `LogEntityChangeAsync` var; `AuditLogActions.USER_*` tanımlı; controller’da user lifecycle için çağrı yok.
- **AuthController:** Sadece JWT; refresh token uygulanmamış; rol “Administrator” bekleniyor, seed’de “Admin” var (uyumsuzluk riski).
- **Rol yapısı:** Seed: Admin, Cashier, Demo, Manager. İstek: SuperAdmin, BranchManager, Cashier, Kellner, Auditor.

### Frontend-Admin
- **Users sayfası:** `useGetApiUserManagement` kullanıyor; tip olarak `UserResponse` ve alanlar `fullName`, `branchName` bekleniyor; API aslında `UserInfo[]` (id, userName, firstName, lastName, …) dönüyor → **sayfa hatalı/eksik.**
- **Generated API:** user-management ve audit-log client’ları mevcut; user için audit log endpoint’i: `GET api/AuditLog/user/{userId}`.

---

## 2) Fazlı Uygulama Planı

### Faz 1 – Backend: Denetlenebilir kullanıcı yaşam döngüsü
**Dosyalar:** `UserManagementController.cs`, `AuditLogService.cs` (veya interface), `AuditLog.cs` (sabitler), `ApplicationUser.cs` (opsiyonel alanlar)

- Soft-delete’e **neden + aktör + zaman** ekle: deaktivasyon nedeni (reason), kim yaptı (actorUserId), ne zaman (DeactivatedAt).
- **Seçenek A (minimal):** Sadece audit log’da reason/actor tut; ApplicationUser’a `DeactivatedAt`, `DeactivatedBy`, `DeactivationReason` kolonları eklemek için migration.
- User lifecycle için **audit log** zorunlu: Create, Update, RoleChange, PasswordReset, Deactivate, Reactivate. Her kayıtta: actorUserId, targetUserId, correlationId, ip, userAgent (mevcut AuditLog alanları kullanılır).
- `AuditLogService`: User hedefli log için `EntityId` = hedef kullanıcı ID (string’i Guid’e çeviremiyorsak EntityType=User ve EntityName=userId kullanılır; AuditLog.EntityId nullable Guid). Mevcut yapıda EntityId Guid; User Id string. Bu yüzden user audit’te EntityType=User, EntityName=userId, EntityId=null bırakılabilir veya ek alan TargetUserId eklenir. **Pratik çözüm:** EntityType=User, EntityName=targetUserId, EntityId=null; RequestData içinde targetUserId ve reason.
- **UserManagementController:** Her create/update/role-change/password-reset/deactivate/reactivate sonrası `IAuditLogService.LogUserActivityAsync` veya `LogEntityChangeAsync` çağrısı (actor = HttpContext.User, target = ilgili user, correlationId = Guid.NewGuid().ToString()).

**Migration (Faz 1):**  
- `ApplicationUser`: `DeactivatedAt` (DateTime?), `DeactivatedBy` (string?), `DeactivationReason` (string?, MaxLength 500).  
- Rollback: Kolonları kaldıran migration.

---

### Faz 2 – Backend: Rol modeli ve yetki matrisi
**Dosyalar:** `RoleSeedData.cs`, `UserService.cs` (HasPermission), yeni `Constants/Roles.cs` veya `Policies/UserManagementPolicies.cs`

- **Roller:** SuperAdmin, BranchManager, Cashier, Kellner, Auditor (read-only). Mevcut Admin → SuperAdmin/Administrator ile eşle (geriye uyum için “Administrator” rol adı korunabilir).
- **Permission matrix:** Merkezi sabitler; backend’de policy/attribute ile doğrulanır. Örnek: User listesi = Administrator, BranchManager; User create/edit = Administrator, BranchManager; Deactivate/Reactivate = Administrator; Audit log görüntüleme = Administrator, Manager, Auditor.
- **RoleSeedData:** Administrator, Manager, Cashier, Kellner, Auditor, Demo ekle; SuperAdmin = Administrator alias olarak kullanılabilir.
- **UserManagementController:** `[Authorize(Roles = "Administrator")]` kalır (Administrator = en yüksek yetkili admin).

---

### Faz 3 – Backend: Liste filtreleri ve reactivate
**Dosyalar:** `UserManagementController.cs`

- **GET api/UserManagement:** Query parametreleri: `role`, `isActive` (bool?), `query` (arama). Mevcut GetUsers’ı genişlet veya search ile birleştir. İstemci: role ve status (active/inactive) filtreleri.
- **PUT api/UserManagement/{id}/reactivate:** Body: `{ "reason": "optional" }`. IsActive = true, DeactivatedAt/By/Reason temizle. Audit: USER_REACTIVATE.
- **Deactivate:** Body’de `reason` zorunlu; RequestData’da sakla; audit’te USER_DEACTIVATE.

---

### Faz 4 – Session güvenliği (JWT rotation / force logout)
**Kapsam:** İsteğe bağlı; ayrı task’ta yapılabilir.

- Refresh token tablosu ve revocation; kritik olaylarda (rol düşürme, deaktivasyon, şifre sıfırlama) token’ları iptal et veya “force logout” bayrağı.
- Mevcut AuthController sadece JWT üretiyor; refresh yok. Bu faz için: RefreshToken entity, AuthController’da refresh endpoint, UserManagement/Auth’da deactivate/reset sonrası ilgili refresh token’ları silmek.

---

### Faz 5 – FE-Admin: Users listesi ve filtreler
**Dosyalar:** `app/(protected)/users/page.tsx`, yeni `features/users/` (hooks, components)

- Users listesini **UserInfo** ile düzelt: fullName = `${firstName} ${lastName}`. branchName backend’de yok; kaldır veya “—” göster.
- Filtreler: Role (dropdown), Status (Active / Inactive / All). API’de GET list’e `role` ve `isActive` query parametreleri eklenmiş olmalı; FE’de `useGetApiUserManagementSearch` veya yeni endpoint kullan.
- Tablo: User (avatar + fullName + email), Role (Tag), Status (Active/Inactive Tag), LastLoginAt, Actions (Edit, Deactivate/Reactivate).

---

### Faz 6 – FE-Admin: Create / Edit / Deactivate / Reactivate
**Dosyalar:** `features/users/` (modal/drawer formları, confirm modals)

- **Create user:** Modal/drawer; form alanları API ile uyumlu (CreateUserRequest). Submit: `usePostApiUserManagement`; başarıda listeyi invalidate et.
- **Edit user:** Aynı form; `usePutApiUserManagementId`; rol değişikliği uyarısı (session sonlandırma bilgisi).
- **Deactivate:** Onay modal’ı + **reason** (textarea, zorunlu). Submit: DELETE veya PUT …/deactivate (backend’de DELETE yerine PUT deactivate + reason tercih edilebilir). Başarıda listeyi güncelle.
- **Reactivate:** Onay modal’ı + isteğe bağlı reason. PUT …/reactivate. Backend’de reactivate endpoint eklenecek.

---

### Faz 7 – FE-Admin: Kullanıcı aktivite zaman çizelgesi
**Dosyalar:** `features/users/UserActivityTimeline.tsx`, users detail veya drawer

- Kullanıcı detayında veya “Activity” sekmesinde: `GET api/AuditLog/user/{userId}` ile audit log listesi; tarih, action, description, status. Mevcut audit-log API’si kullanılır.

---

### Faz 8 – Dokümantasyon ve test planı
- **Compliance checklist:** RKSV/BAO/DSGVO eşlemesi (bu dokümanın sonundaki tablo).
- **Test planı:** Unit: UserService permission; Integration: UserManagementController create/update/deactivate/reactivate + audit kaydı; E2E: FE’de login → users → filtrele → create → edit → deactivate → reactivate → activity görüntüleme.

---

## 3) Dosya Bazlı Değişiklik Listesi

| Bölüm | Dosya | Değişiklik |
|-------|--------|------------|
| Backend | `Models/ApplicationUser.cs` | DeactivatedAt, DeactivatedBy, DeactivationReason (opsiyonel; audit’te de tutulabilir) |
| Backend | `Migrations/` | Yeni migration: user deactivation alanları |
| Backend | `Models/AuditLog.cs` | USER_DEACTIVATE, USER_REACTIVATE sabitleri |
| Backend | `Services/AuditLogService.cs` | LogUserLifecycleAsync (actorUserId, targetUserId, action, reason, correlationId) – veya mevcut LogUserActivityAsync + RequestData |
| Backend | `Controllers/UserManagementController.cs` | Audit çağrıları; GET filters (role, isActive); PUT reactivate; deactivate’te reason body |
| Backend | `Data/RoleSeedData.cs` | Administrator, Kellner, Auditor ekle |
| FE-Admin | `app/(protected)/users/page.tsx` | UserInfo kullanımı, fullName, filtreler, aksiyonlar |
| FE-Admin | `features/users/` | useUsers (filters), UserFormModal, DeactivateConfirmModal, ReactivateConfirmModal, UserActivityTimeline |
| FE-Admin | API | Orval yeniden generate (backend OpenAPI değişince) |

---

## 4) Uyumluluk Kontrol Listesi (Eşleme)

| Gereksinim | Karşılık |
|------------|----------|
| Kullanıcı hard-delete yok (fiş referansı korunur) | Soft-delete (IsActive=false); Receipt.CashierId değiştirilmez. |
| Deaktivasyon nedeni ve aktör | DeactivationReason, DeactivatedBy, DeactivatedAt veya sadece audit’te. |
| Create/Update/RoleChange/PasswordReset/Deactivate/Reactivate audit | Her işlemde AuditLog; actorUserId, targetUserId, correlationId, ip, userAgent. |
| Rol matrisi merkezi ve backend’de doğrulanır | RoleSeedData + [Authorize(Roles = "...")]; ileride policy ile genişletilebilir. |
| Session / force logout (kritik olaylar) | Faz 4’te refresh token revocation veya “next login’de geçersiz” bilgisi. |
| Tarihsel kullanıcı kimliği (fişte) | Receipt’te CashierId; gerekiyorsa ileride Receipt’e cashier_display_name snapshot eklenebilir. |
| Veri saklama / anonymization policy | Audit log ve fişler yasal süre saklanır; PII için policy hook’ları ayrı dokümanda. |

---

## 5) Migration ve Rollback Notları

- **Migration:** Sadece `ApplicationUser` tablosuna yeni kolonlar; mevcut veri etkilenmez. Varsayılan NULL.
- **Rollback:** Migration geri alınır; controller’da DeactivatedBy/Reason kullanımı opsiyonel olduğu için eski API davranışı korunabilir.
- **Risk:** UserManagement’ta audit log’a bağımlılık; AuditLogService fail ederse işlem exception fırlatır (tercih: audit başarısız olursa log yaz, işlemi yine de tamamla değil – compliance için audit zorunlu kalsın).

Bu plan, mevcut proje yapısına ve `.cursor/rules` (RKSV, Do-Not-Touch, Task Execution Mode) ile uyumludur.

---

## 6) Test Planı (Özet)

| Seviye | Kapsam |
|--------|--------|
| **Unit** | UserService.HasPermissionAsync rol matrisi; AuditLogService.LogUserLifecycleAsync kayıt oluşturur. |
| **Integration** | UserManagementController: GET (role, isActive filtreleri); POST create + audit; PUT update/role-change + audit; PUT deactivate (reason zorunlu) + audit; PUT reactivate + audit; DELETE soft-delete + audit. |
| **E2E** | FE-Admin: Login → Users → filtrele (Rolle, Status) → Benutzer anlegen → Bearbeiten → Deaktivieren (Grund eingeben) → Reaktivieren → Aktivitätsverlauf öffnen. |

---

## 7) Uyumluluk Kontrol Listesi (Eşleme) – Teknik

| Gereksinim | Uygulama |
|------------|----------|
| Kullanıcı hard-delete yok | Soft-delete (IsActive=false); Receipt.CashierId değiştirilmez. |
| Deaktivasyon nedeni ve aktör | DeactivatedAt, DeactivatedBy, DeactivationReason (ApplicationUser) + AuditLog USER_DEACTIVATE. |
| Create/Update/RoleChange/PasswordReset/Deactivate/Reactivate audit | AuditLogService.LogUserLifecycleAsync / LogUserActivityAsync; actorUserId, targetUserId, correlationId, ip, userAgent. |
| Rol matrisi backend’de doğrulanır | [Authorize(Roles = "Administrator")]; RoleSeedData: Administrator, Kellner, Auditor, … |
| FE tehlikeli aksiyonlarda onay + neden | Deaktivieren-Modal: Grund (reason) zwingend; Reaktivieren mit Bestätigung. |
| Kullanıcı aktivite zaman çizelgesi | GET api/AuditLog/user/{userId}; UserActivityTimeline in Users-Seite. |
| Session / Force-Logout (kritik olaylar) | Faz 4 (opsiyonel): Refresh-Token-Revocation; aktuell nur JWT. |
| Tarihsel kullanıcı kimliği (fişte) | Receipt.CashierId unverändert; ggf. später cashier_display_name Snapshot. |
