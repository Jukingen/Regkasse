# Prompt B — POS User Lifecycle: Domain Model & Migration Plan

**Amaç:** UserStatus, soft-delete, security, session governance ve audit iyileştirmeleri için alan tasarımı, migration adımları, backfill, rollback ve edge case’ler.

---

## 1) Domain Model (ER-Style)

### ApplicationUser (AspNetUsers) — Genişletilmiş

| Alan | Tip | Açıklama |
|------|-----|----------|
| *(mevcut Identity + custom)* | | Id, Email, UserName, PasswordHash, … |
| **UserStatus** | enum (int) | Active = 0, Suspended = 1, Deactivated = 2. IsActive ile geriye uyum: Active ⇒ true, diğerleri ⇒ false. |
| **DeactivatedAt** | DateTime? | Zaten var. |
| **DeactivatedByUserId** | string? (450) | Kim deaktive etti (DeactivatedBy ile aynı; isim netleştirme). |
| **DeactivationReason** | string? (500) | Zaten var. |
| **FailedLoginCount** | int | Son başarılı girişten sonra ardışık başarısız deneme. |
| **LockedUntil** | DateTimeOffset? | Identity LockoutEnd ile çakışmamak için ayrı alan (opsiyonel; Identity’de LockoutEnd zaten var). |
| **MustChangePasswordAtNextLogin** | bool | İlk giriş / admin şifre sıfırlama sonrası zorunlu değişiklik. |
| **LastSuccessfulLoginAt** | DateTime? | FailedLoginCount sıfırlama zamanı. |

Not: Identity’de zaten `LockoutEnabled`, `LockoutEnd`, `AccessFailedCount` var. İhtiyaç varsa **FailedLoginCount** = AccessFailedCount ile eşleştirilip tek kaynak kullanılabilir; **LockedUntil** = LockoutEnd. Bu durumda sadece **MustChangePasswordAtNextLogin** ve **UserStatus** eklenir.

### RefreshToken (yeni tablo) — Session governance

| Alan | Tip | Açıklama |
|------|-----|----------|
| Id | Guid (PK) | |
| UserId | string (450, FK → AspNetUsers) | Token sahibi. |
| TokenHash | string (256) | Token’ın hash’i (saklama güvenliği). |
| DeviceId / ClientId | string? (100) | Cihaz/browser tanımı. |
| IssuedAt | DateTimeOffset | |
| ExpiresAt | DateTimeOffset | |
| RevokedAt | DateTimeOffset? | İptal zamanı. |
| ReplacedByTokenId | Guid? | Rotation: yeni token. |
| FamilyId | Guid? | Refresh family (reuse tespiti). |

İndeksler: UserId, ExpiresAt, (UserId, RevokedAt).

### AuditLog — İyileştirmeler

| Alan | Tip | Açıklama |
|------|-----|----------|
| *(mevcut)* | | UserId (actor), EntityType, EntityId, EntityName, … |
| **TargetUserId** | string? (450) | Hedef kullanıcı (USER_DEACTIVATE, USER_ROLE_CHANGE vb.). EntityType=User iken EntityName yerine/ek olarak. |

İlişkiler: UserId → ApplicationUser (actor). TargetUserId FK **yapılmaz** (deaktive/silinen kullanıcı kalabilir); sadece denetim için string saklanır.

### Receipt / PaymentDetails — Tarihsel bütünlük

- **CashierId** (string) aynen kalır; **asla** FK yapılmaz. Deaktive kullanıcı fişte görünmeye devam eder.
- **Opsiyonel:** `CashierDisplayNameSnapshot` (string?, 200) — fiş basıldığı anda “Ad Soyad” (RKSV’de zorunlu değil; okunabilirlik için).

---

## 2) Relationship Summary (ER)

```
AspNetUsers (ApplicationUser)
  ├── 1:N Cart (UserId)           [Cascade – dikkat]
  ├── 1:1 UserSettings (UserId)   [Cascade – dikkat]
  ├── 1:N AuditLog (UserId)       [actor]
  ├── 1:N AuditLog (TargetUserId) [string, no FK]
  ├── 1:N CashRegisterTransaction (UserId)
  ├── 1:N TableOrder (UserId)
  ├── 1:N DailyClosing (UserId)
  ├── N:1 CashRegister (CurrentUserId)
  └── 1:N RefreshToken (UserId)   [yeni]

Receipt ──► CashierId (string, no FK)
PaymentDetails ──► CashierId (string, no FK)
```

---

## 3) Concrete Migration Steps

**Migration 1: UserStatus and security (ApplicationUser)**

1. AspNetUsers’a kolon ekle:
   - `user_status` int NOT NULL DEFAULT 0 (0=Active, 1=Suspended, 2=Deactivated).
   - `failed_login_count` int NOT NULL DEFAULT 0.
   - `last_successful_login_at` timestamp with time zone NULL.
   - `must_change_password_at_next_login` boolean NOT NULL DEFAULT false.
2. (Opsiyonel) `deactivated_by` → `deactivated_by_user_id` rename; veya sadece yorum/API’da “DeactivatedByUserId” kullan.
3. Mevcut veri: `is_active = false` ⇒ `user_status = 2`, `is_active = true` ⇒ `user_status = 0`. Diğer alanlar default.

**Migration 2: RefreshToken table**

1. `refresh_tokens` tablosu: id (uuid), user_id (varchar 450), token_hash (varchar 256), device_id (varchar 100), issued_at, expires_at, revoked_at, replaced_by_token_id (uuid), family_id (uuid). FK: user_id → AspNetUsers(Id) ON DELETE CASCADE (kullanıcı silinirse token’lar gider; soft-delete kullandığımız için pratikte silinmez).
2. İndeks: user_id, expires_at, (user_id, revoked_at).

**Migration 3: AuditLog TargetUserId**

1. `audit_logs` tablosuna `target_user_id` varchar(450) NULL ekle.
2. Backfill: EntityType = 'User' ve EntityName dolu ise EntityName’i target_user_id’ye kopyala (tek seferlik script).

**Migration 4 (opsiyonel): Receipt snapshot**

1. `receipts` tablosuna `cashier_display_name_snapshot` varchar(200) NULL ekle.
2. Backfill: boş bırakılır; yeni fişlerde doldurulur.

---

## 4) Data Backfill Strategy

- **UserStatus:** UPDATE AspNetUsers SET user_status = CASE WHEN is_active = false THEN 2 ELSE 0 END WHERE user_status IS NULL (veya default 0 atanmışsa sadece is_active=false olanları 2 yap).
- **FailedLoginCount / LastSuccessfulLoginAt:** 0 ve NULL bırak; yeni login’den itibaren dolar.
- **MustChangePasswordAtNextLogin:** false; admin şifre sıfırladığında true yapılır.
- **AuditLog.TargetUserId:** UPDATE audit_logs SET target_user_id = entity_name WHERE entity_type = 'User' AND entity_name IS NOT NULL AND target_user_id IS NULL.
- **RefreshToken:** Yeni tablo; backfill yok.
- **Receipt.CashierDisplayNameSnapshot:** NULL; sadece yeni fişlerde set edilir.

---

## 5) Rollback Strategy

- **Migration 1 rollback:** user_status, failed_login_count, last_successful_login_at, must_change_password_at_next_login kolonlarını DROP. Uygulama kodu IsActive’e geri döner.
- **Migration 2 rollback:** refresh_tokens tablosunu DROP. Auth yine sadece JWT.
- **Migration 3 rollback:** audit_logs.target_user_id DROP. EntityName ile target okumaya geri dönülür.
- **Migration 4 rollback:** receipts.cashier_display_name_snapshot DROP.

Tüm migration’lar için Down() metodunda aynı adımlar ters sırada yazılır.

---

## 6) Edge Cases

| Edge case | Öneri |
|-----------|--------|
| **Re-activate old user** | UserStatus = Active, DeactivatedAt/By/Reason = NULL. MustChangePasswordAtNextLogin = true (opsiyonel). Refresh token’ları RevokedAt set edilmiş kalır; yeni login’de yeni token. |
| **Branch transfer** | Şu an branch/tenant alanı yok. İleride User’a BranchId eklenirse: transfer sırasında rol/branch güncelle; audit’te USER_TRANSFER; mevcut oturumları iptal et (refresh token revoke). |
| **Role downgrade** | Audit: USER_ROLE_CHANGE. İlgili kullanıcının tüm refresh token’larını revoke et (force re-login). JWT’de rol bir sonraki token’da güncellenir. |
| **Suspended vs Deactivated** | Suspended: geçici (örn. disiplin); Deactivated: işten çıkış. Login’de ikisi de “hesap kullanılamıyor” ama audit’te ayrı aksiyon (USER_SUSPEND / USER_DEACTIVATE). Re-activate sadece Admin; Suspend’ten çıkış da ayrı audit. |
| **LockedUntil + Identity LockoutEnd** | Tek kaynak kullan: Identity LockoutEnd ve AccessFailedCount. Uygulama Login’de bunları okuyup “Hesap kilitli” döner; FailedLoginCount/LastSuccessfulLoginAt sadece sayacı sıfırlamak için kullanılabilir veya Identity’e bırakılır. |
| **Receipt’te CashierId deaktive user** | FK olmadığı için referans bozulmaz. Listeleme/raporlarda “Deaktive kullanıcı” diye gösterilebilir; CashierDisplayNameSnapshot varsa o kullanılır. |

---

## 7) Kısa Uygulama Sırası

1. Migration 1 (UserStatus + security) + ApplicationUser model güncellemesi.
2. AuthController: Login’de UserStatus kontrolü, FailedLoginCount/LastSuccessfulLoginAt (veya Identity AccessFailedCount), MustChangePasswordAtNextLogin redirect.
3. Migration 2 (RefreshToken) + AuthController refresh endpoint + revoke on deactivate/role-change/password-reset.
4. Migration 3 (AuditLog.TargetUserId) + AuditLogService’te target yazımı.
5. Migration 4 (opsiyonel Receipt snapshot) + fiş oluşturma servisinde set.
6. UserManagementController: UserStatus kullanımı, Suspend/Reactivate endpoint’leri (Deactivate’ten ayrı).

Bu doküman, Prompt A analizi ve mevcut FE_ADMIN_USERS_MODULE_RKSV_PLAN ile uyumludur; migration’lar adım adım uygulanabilir.
