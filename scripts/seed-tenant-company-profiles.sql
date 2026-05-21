-- =============================================================================
-- Per-tenant company_settings seed (replaces removed appsettings CompanyProfile)
-- =============================================================================
-- Idempotent: inserts one row per tenant when missing (unique index on tenant_id).
-- Run after migrations AddTenantsAndSettingsTenantId and SeedDemoTenantAdmins.
-- Receipt footer text is stored in CompanyDescription; DefaultTseDeviceId holds DefaultKassenId.
-- =============================================================================

WITH defaults AS (
    SELECT
        'Es bediente Sie unser Team. Danke für Ihren Einkauf!'::text AS footer,
        '{"Monday":"09:00-18:00","Tuesday":"09:00-18:00","Wednesday":"09:00-18:00","Thursday":"09:00-18:00","Friday":"09:00-18:00","Saturday":"10:00-16:00","Sunday":"Closed"}'::jsonb AS business_hours
),
presets AS (
    SELECT *
    FROM (VALUES
        ('9c8f4e2b-1a3d-4f6e-8b7c-0d1e2f3a4b5c'::uuid, 'Regkasse Demo GmbH', 'Musterstraße 123, 1010 Wien', 'ATU12345678', 'FN123456', 'ATU12345678', '+43 1 234 567', 'info@regkasse.at', 'www.regkasse.at', 'https://via.placeholder.com/150', 'KASSE-001'),
        ('b0000001-0001-4001-8001-000000000001'::uuid, 'Regkasse Development GmbH', 'Devstraße 1, 1010 Wien', 'ATU11111111', 'FN111111', 'ATU11111111', '+43 1 234 567', 'admin@dev.regkasse.at', 'dev.regkasse.at', 'https://via.placeholder.com/150', 'KASSE-DEV'),
        ('b0000001-0001-4001-8001-000000000002'::uuid, 'Test Cafe GmbH', 'Cafégasse 5, 1020 Wien', 'ATU22222222', 'FN222222', 'ATU22222222', '+43 1 234 568', 'admin@cafe.regkasse.at', 'cafe.regkasse.at', 'https://via.placeholder.com/150', 'KASSE-CAFE'),
        ('b0000001-0001-4001-8001-000000000003'::uuid, 'Test Bar GmbH', 'Bargasse 9, 1030 Wien', 'ATU33333333', 'FN333333', 'ATU33333333', '+43 1 234 569', 'admin@bar.regkasse.at', 'bar.regkasse.at', 'https://via.placeholder.com/150', 'KASSE-BAR')
    ) AS t(tenant_id, company_name, company_address, tax_number, reg_number, vat_number, phone, email, website, logo, default_kassen_id)
)
INSERT INTO company_settings (
    id,
    tenant_id,
    "CompanyName",
    "CompanyAddress",
    "CompanyPhone",
    "CompanyEmail",
    "CompanyWebsite",
    "CompanyTaxNumber",
    "CompanyRegistrationNumber",
    "CompanyVatNumber",
    "CompanyLogo",
    "CompanyDescription",
    "BusinessHours",
    "ContactPerson",
    "ContactPhone",
    "ContactEmail",
    "BankName",
    "BankAccountNumber",
    "BankRoutingNumber",
    "BankSwiftCode",
    "PaymentTerms",
    "Currency",
    "Language",
    "TimeZone",
    "DateFormat",
    "TimeFormat",
    "DecimalPlaces",
    "TaxCalculationMethod",
    "InvoiceNumbering",
    "ReceiptNumbering",
    "DefaultPaymentMethod",
    "DefaultTseDeviceId",
    created_at,
    is_active
)
SELECT
    gen_random_uuid(),
    p.tenant_id,
    p.company_name,
    p.company_address,
    p.phone,
    p.email,
    p.website,
    p.tax_number,
    p.reg_number,
    p.vat_number,
    p.logo,
    d.footer,
    d.business_hours,
    'Default Contact',
    p.phone,
    p.email,
    'Default Bank',
    '0000000000',
    '000000000',
    'DEFAULT',
    'Net 30',
    'EUR',
    'de-DE',
    'Europe/Vienna',
    'dd.MM.yyyy',
    'HH:mm:ss',
    2,
    'Standard',
    'Sequential',
    'Sequential',
    'Cash',
    p.default_kassen_id,
    NOW(),
    true
FROM presets p
CROSS JOIN defaults d
WHERE NOT EXISTS (
    SELECT 1 FROM company_settings cs WHERE cs.tenant_id = p.tenant_id
);

-- Optional: any tenant with slug test_cafe (e.g. from local tests) gets demo profile if missing.
WITH defaults AS (
    SELECT
        'Es bediente Sie unser Team. Danke für Ihren Einkauf!'::text AS footer,
        '{"Monday":"09:00-18:00","Tuesday":"09:00-18:00","Wednesday":"09:00-18:00","Thursday":"09:00-18:00","Friday":"09:00-18:00","Saturday":"10:00-16:00","Sunday":"Closed"}'::jsonb AS business_hours
)
INSERT INTO company_settings (
    id, tenant_id, "CompanyName", "CompanyAddress", "CompanyPhone", "CompanyEmail", "CompanyWebsite",
    "CompanyTaxNumber", "CompanyRegistrationNumber", "CompanyVatNumber", "CompanyLogo", "CompanyDescription",
    "BusinessHours", "ContactPerson", "ContactPhone", "ContactEmail", "BankName", "BankAccountNumber",
    "BankRoutingNumber", "BankSwiftCode", "PaymentTerms", "Currency", "Language", "TimeZone", "DateFormat",
    "TimeFormat", "DecimalPlaces", "TaxCalculationMethod", "InvoiceNumbering", "ReceiptNumbering",
    "DefaultPaymentMethod", "DefaultTseDeviceId", created_at, is_active
)
SELECT
    gen_random_uuid(), t.id, 'Test Cafe (local)', 'Teststraße 1, 1010 Wien', '+43 1 234 567', 'test@cafe.local', 'test_cafe.local',
    'ATU44444444', 'FN444444', 'ATU44444444', 'https://via.placeholder.com/150', d.footer, d.business_hours,
    'Default Contact', '+43 1 234 567', 'test@cafe.local', 'Default Bank', '0000000000', '000000000', 'DEFAULT',
    'Net 30', 'EUR', 'de-DE', 'Europe/Vienna', 'dd.MM.yyyy', 'HH:mm:ss', 2,
    'Standard', 'Sequential', 'Sequential', 'Cash', 'KASSE-TEST', NOW(), true
FROM tenants t
CROSS JOIN defaults d
WHERE t."Slug" = 'test_cafe'
  AND NOT EXISTS (SELECT 1 FROM company_settings cs WHERE cs.tenant_id = t.id);
