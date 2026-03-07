# Prompt A — Mevcut User/Auth/Audit Mimarisi Analizi

**Odak:** `backend/` (Controllers, Services, Models, DbContext, Migrations), `frontend-admin/src/app/(protected)/users`, `frontend-admin/src/api/generated`.

---

## 1) Existing Entities and Relations (User, Receipts, Orders, Payments, Audit Logs)

### User (ApplicationUser)
- **Table:** `AspNetUsers` (Identity).
- **Fields (relevant):** Id (string), FirstName, LastName, EmployeeNumber, Role (string), TaxNumber, Notes, **IsActive**, LastLogin, LastLoginAt, LoginCount, CreatedAt, UpdatedAt, **DeactivatedAt, DeactivatedBy, DeactivationReason**, AccountType, IsDemo, CashRegisterId.
- **Relations (navigation):** CashRegisters, Orders, Carts, Transactions (CashRegisterTransaction), InventoryTransactions. Identity: AspNetUserRoles, AspNetUserClaims, AspNetUserLogins.

### Receipts
- **Table:** `receipts`.
- **User linkage:** `CashierId` (string, nullable) — **no FK** to AspNetUsers. Referans sadece string ID; silinen/deaktif kullanıcı için fişte ID kalır (tarihsel bütünlük için uygun).

### Orders
- **Table:** `orders`.
- **User linkage:** **Yok.** Sadece `WaiterName` (string), `CustomerName`, `CustomerId`. Order doğrudan ApplicationUser’a bağlı değil.

### Payments
- **PaymentDetails:** `payment_details`. **CashierId** (string, required) — **no FK** to AspNetUsers. Tarihsel bütünlük korunur.
- **PaymentSession:** `UserId` (string).
- **PaymentLogEntry:** `UserId` (string).

### Audit Logs
- **Table:** `audit_logs` (BaseEntity: Id, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsActive).
- **Fields:** SessionId, **UserId** (actor), UserRole, Action, EntityType, EntityId (Guid?), EntityName, OldValues, NewValues, RequestData, ResponseData, Status, Timestamp, Description, Notes, IpAddress, UserAgent, Endpoint, HttpMethod, HttpStatusCode, CorrelationId, TransactionId, Amount, PaymentMethod, TseSignature, ErrorDetails, ProcessingTimeMs.
- **Relation:** `[ForeignKey("UserId")]` → ApplicationUser. Hedef kullanıcı (target) için EntityType=User, EntityName=targetUserId kullanılıyor; **TargetUserId** ayrı kolon yok.

### Diğer user referansları
| Entity | User alanı | FK / Cascade |
|--------|------------|---------------|
| Cart | UserId | FK → ApplicationUser, **OnDelete(Cascade)** |
| TableOrder | UserId | FK → ApplicationUser |
| DailyClosing | UserId | FK → ApplicationUser (no explicit Cascade) |
| CashRegisterTransaction | UserId | FK → ApplicationUser |
| CashRegister | CurrentUserId | optional |
| UserSettings | UserId | FK → ApplicationUser, **OnDelete(Cascade)** |

---

## 2) Current Delete Behaviour Risks

- **UserManagementController:** Sadece **soft-delete** (IsActive = false, DeactivatedAt/By/Reason set). Hard-delete **yok**; fiş/ödeme referansları bozulmaz.
- **Risk 1 – Cascade:** Cart ve UserSettings, ApplicationUser’a **Cascade** ile bağlı. İleride yanlışlıkla `UserManager.DeleteAsync` veya EF `Remove(user)` kullanılırsa bu kayıtlar da silinir; RKSV/BAO için fiş/ödeme tarafı Receipt/PaymentDetails’ta olduğu için asıl fiscal veri korunur, ancak sepet ve kullanıcı ayarları kaybı + audit’te UserId orphan kalabilir.
- **Risk 2 – Orphan references:** AuditLog.UserId FK; kullanıcı hard-delete edilirse (şu an yapılmıyor) constraint hatası veya Restrict davranışı beklenir. Identity default’u genelde Restrict.
- **Risk 3:** Logout’ta sadece cart cleanup; JWT geçersiz kılma / refresh token revocation yok. Deaktive veya şifre sıırlama sonrası mevcut token ile bir süre daha istek atılabilir.

---

## 3) Missing Policy Checks

- **Merkezi yetki matrisi yok:** Rol kontrolleri `[Authorize(Roles = "Administrator")]` vb. ile dağınık; permission sabitleri veya policy isimleri tek yerde toplanmamış.
- **Auditor (read-only):** Rol seed’te Auditor var; hangi endpoint’lerin salt okunur erişime açılacağı net tanımlı değil (örn. AuditLog, Reports).
- **Branch/tenant scope yok:** BranchManager veya şube bazlı filtreleme (branchId) yok; tüm kullanıcılar tek listede.
- **Typo / tutarsız rol:** PaymentController’da `"Administrator,Kasiyer"` (Kasiyer) kullanılıyor; diğer yerlerde "Cashier". Seed’te "Cashier" var; bu endpoint’te rol eşleşmeyebilir.
- **Resource-level check yok:** “Kendi kaydını düzenle” vs “başka şubedekini düzenle” gibi kaynak bazlı yetki yok.

---

## 4) Gaps for Austrian POS Traceability and GDPR

| Gereksinim | Mevcut | Eksik |
|------------|--------|--------|
| Belegausgabepflicht (işlem izi) | Receipt.CashierId, PaymentDetails.CashierId, AuditLog | Receipt’te sadece ID; ihtiyaç halinde cashier display name snapshot yok. |
| RKSV / manipülasyona dayanıklılık | TSE imza, fiş numarası, audit | Kullanıcı tarafında lockout / failed attempt sayacı yok. |
| BAO saklama | Kayıtlar kalıcı; soft-delete | Veri minimizasyonu / anonymization policy (7 yıl sonrası) tanımlı değil. |
| DSGVO – erişim kontrolü | Role-based [Authorize] | Merkezi policy, Auditor scope, branch scope eksik. |
| DSGVO – audit trail | AuditLog (actor, action, entity, ip, userAgent) | Hedef kullanıcı için ayrı TargetUserId kolonu yok (EntityName ile id tutuluyor). |
| Session governance | JWT only | Refresh token yok; deactivate/password reset sonrası anında token iptali yok. |
| User lifecycle states | IsActive (bool) | Suspended, “must change password” gibi ara durumlar yok. |
| Security (lockout) | Identity LockoutEnd | failedLoginCount, lockedUntil, mustChangePasswordAtNextLogin alanları yok (Identity’de kısmen var; kullanılmıyor). |

---

## 5) Exact Files to Modify First (Ordered)

1. **backend/Models/ApplicationUser.cs** — UserStatus enum kullanımı, güvenlik alanları (failedLoginCount, lockedUntil, mustChangePasswordAtNextLogin), soft-delete alan adı tutarlılığı (DeactivatedBy → DeactivatedByUserId isteğe bağlı).
2. **backend/Models/AuditLog.cs** — İsteğe bağlı TargetUserId; AuditLogActions sabitleri (zaten USER_DEACTIVATE vb. var).
3. **backend/Services/AuditLogService.cs** — LogUserLifecycleAsync target/actor/context (mevcut; gerekirse TargetUserId yazımı).
4. **backend/Controllers/UserManagementController.cs** — Policy/role tutarlılığı, Kasiyer→Cashier düzeltmesi başka controller’da.
5. **backend/Controllers/AuthController.cs** — Login’de failed attempt / lockout, mustChangePassword; refresh token (ayrı task).
6. **backend/Data/AppDbContext.cs** — ApplicationUser yeni alanlar, RefreshToken tablosu (varsa), AuditLog TargetUserId (varsa); Cart/UserSettings cascade’in dokümantasyonu veya Restrict değerlendirmesi.
7. **backend/Migrations/** — Yeni migration: UserStatus/security/session alanları.
8. **backend/Data/RoleSeedData.cs** — Roller tutarlı (Administrator, Cashier, Kellner, Auditor, …).
9. **backend/Program.cs** — Policy kayıtları (ör. Auditor read-only), CORS/auth ayarları.
10. **PaymentController.cs** — "Kasiyer" → "Cashier" rol adı düzeltmesi.
11. **frontend-admin** — Users sayfası (zaten UserInfo, filtreler, deactivate/reactivate, activity timeline); audit-logs’ta userId filtresi (varsa).

---

**Özet:** Kullanıcı tarafı soft-delete ve audit ile RKSV’ye temel uyumlu; fiş/ödeme referansları string ID ile korunuyor. Eksikler: merkezi policy, lockout/must-change-password, refresh token ile session governance, AuditLog’da açık TargetUserId ve opsiyonel cashier display name snapshot. İlk değişiklik sırası: ApplicationUser modeli ve migration, ardından Auth/UserManagement ve policy’ler.
