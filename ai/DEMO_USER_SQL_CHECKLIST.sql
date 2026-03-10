-- =============================================================================
-- Demo-state / Cashier drift doğrulama checklist (PostgreSQL)
-- Kullanım: :user_id veya :user_email yerine sabit değer yazın (psql \set veya manuel).
-- Tablo/kolon isimleri repo EF snapshot ile uyumlu: AspNetUsers.role, is_demo;
-- payment_details.created_by, CashierId (PascalCase kolon adı).
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 0) Parametreler — tek kullanıcı odaklı kontrol
-- -----------------------------------------------------------------------------
-- Örnek (psql):
--   \set user_id 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
--   \set user_email 'demo@demo.com'
-- Sonra sorgularda :user_id / :user_email kullanın; veya aşağıdaki WHERE'leri elle doldurun.

-- -----------------------------------------------------------------------------
-- 1) İlgili user — AspNetUsers.Role + is_demo + kimlik
-- -----------------------------------------------------------------------------
-- Identity tablosu: custom kolonlar snake_case (role, is_demo). Id/Email Identity default.
SELECT
    u."Id"          AS user_id,
    u."Email"       AS email,
    u."UserName"    AS user_name,
    u.role          AS aspnetusers_role,
    u.is_demo       AS aspnetusers_is_demo,
    u.account_type  AS account_type,
    u.is_active     AS is_active
FROM "AspNetUsers" u
WHERE u."Id" = 'REPLACE_WITH_USER_ID'
   OR u."Email" = 'REPLACE_WITH_EMAIL';

-- -----------------------------------------------------------------------------
-- 2) Aynı user — AspNetUserRoles + AspNetRoles.Name (tek satırda rolliste)
-- -----------------------------------------------------------------------------
SELECT
    u."Id"   AS user_id,
    u."Email",
    u.role   AS role_column,
    u.is_demo,
    COALESCE(string_agg(r."Name", ', ' ORDER BY r."Name"), '(no roles)') AS aspnet_roles_names
FROM "AspNetUsers" u
LEFT JOIN "AspNetUserRoles" ur ON ur."UserId" = u."Id"
LEFT JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
WHERE u."Id" = 'REPLACE_WITH_USER_ID'
   OR u."Email" = 'REPLACE_WITH_EMAIL'
GROUP BY u."Id", u."Email", u.role, u.is_demo;

-- Detaylı satır satır role atamaları
SELECT
    ur."UserId",
    r."Name" AS role_name,
    r."Id"   AS role_id
FROM "AspNetUserRoles" ur
JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
WHERE ur."UserId" = 'REPLACE_WITH_USER_ID';

-- -----------------------------------------------------------------------------
-- 3) Demo residual data kontrolü
-- -----------------------------------------------------------------------------
-- 3a) Demo rolü hâlâ var mı? (migration sonrası olmamalı)
SELECT "Id", "Name", "NormalizedName"
FROM "AspNetRoles"
WHERE "Name" ILIKE '%demo%' OR "NormalizedName" ILIKE '%DEMO%';

-- 3b) AspNetUserRoles içinde Demo roleId ile kayıt kaldı mı?
SELECT ur.*
FROM "AspNetUserRoles" ur
JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
WHERE r."Name" = 'Demo';

-- 3c) role kolonu hâlâ 'Demo' string mi? (migration sonrası olmamalı)
SELECT "Id", "Email", role, is_demo
FROM "AspNetUsers"
WHERE role = 'Demo' OR role ILIKE '%demo%';

-- 3d) is_demo = true tüm kullanıcılar (drift adayları: Cashier + is_demo true)
SELECT "Id", "Email", role, is_demo, account_type
FROM "AspNetUsers"
WHERE is_demo = true
ORDER BY "Email";

-- -----------------------------------------------------------------------------
-- 4) Cashier assignment doğrulaması
-- -----------------------------------------------------------------------------
-- 4a) Cashier rolü DB'de var mı?
SELECT "Id", "Name" FROM "AspNetRoles" WHERE "Name" = 'Cashier';

-- 4b) Hedef user Cashier'a atanmış mı?
SELECT u."Id", u."Email", u.role, u.is_demo, r."Name" AS assigned_role
FROM "AspNetUsers" u
JOIN "AspNetUserRoles" ur ON ur."UserId" = u."Id"
JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
WHERE u."Id" = 'REPLACE_WITH_USER_ID'
  AND r."Name" = 'Cashier';

-- 4c) role kolonu Cashier ama AspNetUserRoles'ta Cashier yok → drift
SELECT u."Id", u."Email", u.role,
       (SELECT COUNT(*) FROM "AspNetUserRoles" ur2
        JOIN "AspNetRoles" r2 ON r2."Id" = ur2."RoleId"
        WHERE ur2."UserId" = u."Id" AND r2."Name" = 'Cashier') AS cashier_role_rows
FROM "AspNetUsers" u
WHERE u.role = 'Cashier'
  AND NOT EXISTS (
    SELECT 1 FROM "AspNetUserRoles" ur
    JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
    WHERE ur."UserId" = u."Id" AND r."Name" = 'Cashier'
  );

-- -----------------------------------------------------------------------------
-- 5) Auth user vs payload cashierId — DB tarafı kontrol önerisi
-- -----------------------------------------------------------------------------
-- Backend artık Payment oluştururken CashierId = authenticated userId yazar.
-- Eski kayıtlarda created_by ile CashierId farklı olabilir; yeni kayıtta eşit olmalı.
-- JWT sub = user_id; son ödemelerde created_by / CashierId aynı olmalı.

-- 5a) Belirli user'ın son N ödemesi: created_by vs CashierId
SELECT
    p.id,
    p.created_at,
    p.created_by,
    p."CashierId",
    (p.created_by IS NOT DISTINCT FROM p."CashierId") AS ids_match
FROM payment_details p
WHERE p.created_by = 'REPLACE_WITH_USER_ID'
   OR p."CashierId" = 'REPLACE_WITH_USER_ID'
ORDER BY p.created_at DESC
LIMIT 20;

-- 5b) created_by <> CashierId olan son kayıtlar (audit / mismatch izi)
SELECT p.id, p.created_at, p.created_by, p."CashierId"
FROM payment_details p
WHERE p.created_by IS NOT NULL
  AND p."CashierId" IS NOT NULL
  AND p.created_by <> p."CashierId"
ORDER BY p.created_at DESC
LIMIT 50;

-- Not: Payload cashierId uyuşmazlığı artık API 403 CASHIER_ID_MISMATCH; DB'ye yazılmadan reddedilir.
-- Canlı teşhis: API response diagnosticCode + log alanları (authenticatedUserId vs payloadCashierId).

-- =============================================================================
-- 6) Expected vs broken state (referans tablo — SQL çıktısı değil)
-- =============================================================================
-- | Durum                          | role column | is_demo | AspNetUserRoles    | Payment POST      |
-- |--------------------------------|------------|--------|--------------------|-------------------|
-- | Beklenen: gerçek kasiyer       | Cashier    | false  | Cashier            | OK                |
-- | Beklenen: demo kasiyer (seed)  | Cashier    | true   | Cashier            | 400 DEMO_BY_FLAG  |
-- | Kırık: UI Cashier, ödeme red   | Cashier    | true   | Cashier            | 400 DEMO_BY_FLAG  |
-- | Kırık: rol kolonu drift        | Demo       | *      | Cashier            | DEMO_BY_ROLE veya |
-- | Kırık: role tablosu senkron değil | Cashier | *   | (Cashier yok)      | yetki/rol hatası   |
-- | Kırık: residual Demo rol       | *          | *      | Demo satırı var    | migration eksik   |
-- Çözüm: is_demo false (PUT/Patch isDemo) veya migration tamamlandı mı + AspNetRoles'ta Demo yok mu.
-- =============================================================================
