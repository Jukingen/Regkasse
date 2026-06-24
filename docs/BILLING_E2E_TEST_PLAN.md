# Billing System E2E Test Plan

> **Scope:** Super Admin billing sales + Mandant SaaS license UI in Frontend Admin.  
> **Related:** [`BILLING_TENANT_LICENSE.md`](BILLING_TENANT_LICENSE.md), [`BILLING_TESTING.md`](BILLING_TESTING.md)

**Last updated:** 2026-06-24

---

## Test Setup

| Component | URL / requirement |
|-----------|-------------------|
| Backend API | `http://localhost:5184` |
| Frontend Admin | `http://localhost:3000` |
| Actor (Scenarios 1–6) | **Super Admin** — role `SuperAdmin` or permission `system.critical` |
| Actor (Scenario 7) | **Manager** — role `Manager` with `license.manage`, tenant JWT context (not platform mode) |
| Dev tenant header | Optional: `X-Tenant-Id` or `?tenant={slug}` when testing tenant-scoped flows locally |

### Start services

```bash
# Terminal 1 — API
cd backend && dotnet run --project KasseAPI_Final.csproj

# Terminal 2 — Admin UI
cd frontend-admin && npm run dev
```

### Preconditions

- At least one **active** tenant exists (e.g. `test_cafe`) without a blocking license state for new-sale tests.
- Super Admin can access all `/admin/billing/*` routes (guarded by `SYSTEM_CRITICAL`).
- PostgreSQL migrated with billing tables (`license_sales`, `invoice_sequences`, `billing_audit_log`, …).

---

## Test Scenarios

### Scenario 1: Create New License Sale

**Route:** `/admin/billing/sales/new`

1. Navigate to `/admin/billing/sales/new`
2. Select a tenant from dropdown (**Mandant**)
3. Choose license plan: **1 Jahr** (`12_months`)
4. Enter price: **€299.00** (field: **Preis (Netto)**)
5. Click **Vorschau erstellen**
6. Verify preview panel shows:
   - License key (`REGK-…`)
   - Invoice number (`RE…`)
   - Valid-from / valid-until dates
   - Net / VAT / gross amounts (299 / 59.80 / 358.80 at 20% VAT)
7. Click **PDF-Vorschau anzeigen**
8. Verify PDF modal opens with embedded preview
9. Click **Verkauf abschließen** (enabled only after successful preview)
10. Verify redirect to `/admin/billing/sales/{id}`
11. Verify invoice number is displayed on detail page and matches preview

**API checks (optional):**

- `POST /api/admin/billing/license-sales/preview` → 200
- `POST /api/admin/billing/license-sales` → 201
- Tenant row updated: `license_key`, `license_valid_until_utc`, `current_license_sale_id`

---

### Scenario 2: View License Overview

**Route:** `/admin/billing`

1. Navigate to `/admin/billing`
2. Verify statistics cards are displayed (revenue, active licenses, expiring soon, total sales, …)
3. Verify **bald ablaufende Lizenzen** table is displayed (≤30 days threshold)
4. Optional: click tenant link in expiring table → `/admin/tenants/{tenantId}`

---

### Scenario 3: View All Sales

**Route:** `/admin/billing/sales`

1. Navigate to `/admin/billing/sales`
2. Verify sales table is displayed (invoice, tenant, plan, amount, status, sold date)
3. Test search by tenant name, slug, license key, or invoice number
4. Test filter by status (**Alle Status** / **Aktiv** / **Storniert** / **Rückerstattet**)
5. Test date range filter (sold-date range)
6. Optional: click row **Details** → sale detail page

---

### Scenario 4: View Sale Detail

**Route:** `/admin/billing/sales/{id}`

1. Open a sale from the sales list (or use ID from Scenario 1)
2. Verify all details are displayed (tenant, plan, license key, validity, pricing, status, sold-by)
3. Click **PDF Herunterladen**
4. Verify PDF file downloads (`{invoiceNumber}.pdf`)

---

### Scenario 5: Cancel a Sale

**Route:** `/admin/billing/sales/{id}` (active sale)

1. Open an **active** sale detail page
2. Click **Stornieren**
3. Confirm cancellation in modal; enter **Stornierungsgrund** (min. 10 characters)
4. Verify status changes to **Storniert**
5. Verify tenant license is removed when this sale was the current one:
   - `tenants.license_key` cleared
   - `tenants.license_valid_until_utc` cleared
   - `tenants.current_license_sale_id` cleared
6. Optional: verify `billing_audit_log` contains `SALE_CANCELLED`

---

### Scenario 6: Tenant License Display (Super Admin)

**Route:** `/admin/license`

1. Navigate to `/admin/license`
2. Switch to **Mandantenlizenzen** tab (outer tab; visible when Super Admin has deployment + tenant overview)
3. Verify inner tab **Alle Mandantenlizenzen** shows sales/license table with KPI cards
4. Click a **tenant name** link in the table
5. Verify redirect to `/admin/tenants/{tenantId}` (tenant detail)
6. Optional: switch inner tab **Lizenzstatus** for overview-style list (`TenantLicenseOverview`)

> **Note:** Super Admins without deployment section may see `TenantLicenseTabs` directly (no outer Server-Lizenz tab).

---

### Scenario 7: Manager License View

**Route:** `/admin/license`  
**Actor:** Manager (tenant-scoped session, not `admin.regkasse.at` platform mode)

1. Log in as **Manager** for a single tenant
2. Navigate to `/admin/license`
3. Verify **Mandantenlizenz** section is displayed (`TenantLicenseSection` — own tenant status, key, extend UI)
4. Verify **Server-Lizenz (On-Premise)** tab is **not** visible
5. Verify **Mandantenlizenzen** overview tab (all-tenant sales table) is **not** visible
6. Verify own tenant license status is displayed (valid until, active/expired indicator)

> **Implementation note:** Managers see only their mandant license block, not Super Admin billing overview or deployment license tabs (`licenseAccess.test.ts`).

---

## Pass / fail checklist

| # | Scenario | Pass |
|---|----------|------|
| 1 | Create new license sale (preview → PDF → complete) | ☐ |
| 2 | Billing overview KPIs + expiring table | ☐ |
| 3 | Sales list filters (search, status, date) | ☐ |
| 4 | Sale detail + PDF download | ☐ |
| 5 | Cancel sale + tenant license cleared | ☐ |
| 6 | Super Admin tenant license tab + tenant link | ☐ |
| 7 | Manager sees only own mandant license | ☐ |

---

## Known limitations / manual follow-ups

- Billing reminder emails are not yet sent in production flow (rows marked `sent` in DB only).
- Automated browser E2E (Playwright/Cypress) is **not** wired in CI for billing yet — this document is the manual QA script.
- Manager extend still uses legacy `POST /api/admin/license/mandant/extend` in some UI paths; billing canonical extend is `POST /api/admin/license/extend`.

---

## Related documentation

| Document | Topic |
|----------|--------|
| [`BILLING_TENANT_LICENSE.md`](BILLING_TENANT_LICENSE.md) | API, services, DB |
| [`BILLING_TESTING.md`](BILLING_TESTING.md) | Unit/integration test commands |
| [`LICENSE_SYSTEM.md`](LICENSE_SYSTEM.md) | Three license layers |
| [`TENANT_MANAGEMENT.md`](TENANT_MANAGEMENT.md) | Super Admin tenant license tab |
