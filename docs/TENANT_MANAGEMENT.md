# Tenant management (Frontend Admin & API)

> **Audience:** Super Admin operators, FA maintainers, backend integrators.  
> **UI language:** Operator-facing copy is **German (de-AT)**. Technical identifiers and code are **English**.

## Terminology (German ↔ English)

| German (UI / operator) | English (code / API) | Meaning |
|------------------------|----------------------|---------|
| **Mandant** / **Firma** | `tenant` | SaaS customer company; `tenants` row + subdomain slug |
| **Mandantenlizenz** | tenant license | `tenants.license_key`, `tenants.license_valid_until_utc` — company subscription |
| **Server-Lizenz** / **On-Premise** | deployment license | `LicenseService`, `/api/admin/license/*` — API host / POS machine entitlement |
| **Als Super Admin anmelden** | impersonate | `POST /api/admin/tenants/{id}/impersonate` |
| **Firma wechseln** (Dev) | tenant switch | `X-Tenant-Id` + `localStorage.dev_tenant_id` reload |
| **Plattform-Admin** | platform admin host | `admin.regkasse.at` — no mandant context until impersonation |

Related: [`MULTI_TENANT.md`](MULTI_TENANT.md), [`LICENSE_SYSTEM.md`](LICENSE_SYSTEM.md), [`CUSTOMER_ONBOARDING.md`](CUSTOMER_ONBOARDING.md), [`USER_MANAGEMENT.md`](USER_MANAGEMENT.md), [`IMPERSONATION_FLOW.md`](IMPERSONATION_FLOW.md).

---

## What is a tenant (Mandant)?

A **Mandant** is one SaaS customer company in Regkasse:

- **Row:** `tenants` (UUID `id`, unique `slug`, contact fields, status, Mandantenlizenz columns)
- **URL:** `https://{slug}.regkasse.at` (POS + tenant admin)
- **Data:** All business tables with `tenant_id` (products, registers, receipts, …) are isolated via EF global filters

**Super Admin** works on `admin.regkasse.at` without mandant business context until **impersonation** or (dev only) header switcher selects a slug.

---

## Tenant status meanings

| Status (API) | German (UI) | `isActive` | Operator effect |
|--------------|-------------|------------|-----------------|
| `active` | Aktiv | `true` | Normal operation; impersonation allowed |
| `suspended` | Gesperrt | usually `false` | Login/impersonation blocked; data retained |
| `deleted` | Gelöscht | `false` | Soft-delete; hidden from default lists; optional hard-delete later |

**List filter:** *Gelöschte anzeigen* → `includeDeleted=true` on `GET /api/admin/tenants`.

---

## Super Admin: tenant lifecycle

### Access

- **Route:** `/admin/tenants` — `frontend-admin/src/app/(protected)/admin/tenants/page.tsx`
- **Detail:** `/admin/tenants/[tenantId]` — tabs: overview, users, cash registers, license, settings
- **Role:** `SuperAdmin` or permission `system.critical`
- **API module:** `frontend-admin/src/features/super-admin/api/adminTenants.ts` → `GET|POST|PUT|DELETE /api/admin/tenants`

![Tenant list (placeholder)](images/tenant-management/fa-tenant-list.png)

### Tenant detail page (tabs)

| Tab (DE) | Content |
|----------|---------|
| **Übersicht** | Name, slug, status, contact, impersonate |
| **Benutzer** | Invite, roles, owner, reset password — see [`USER_MANAGEMENT.md`](USER_MANAGEMENT.md) |
| **Kassen** | Registers, decommission — see [`CASH_REGISTER_LIFECYCLE.md`](CASH_REGISTER_LIFECYCLE.md) |
| **Lizenz** | Mandantenlizenz (`LicenseManager`) — see [`LICENSE_SYSTEM.md`](LICENSE_SYSTEM.md) |
| **Einstellungen** | Address, status, danger zone (hard-delete mandant) |

Route: `/admin/tenants/[tenantId]` — query `?tab=users|registers|license|settings`.

![Tenant detail overview (placeholder)](images/tenant-management/fa-tenant-detail-overview.png)

### Create tenant (onboarding wizard)

**UI:** `CreateTenantWizard` (exported alias `CreateTenantModal`) — foolproof flow documented in [`CUSTOMER_ONBOARDING.md`](CUSTOMER_ONBOARDING.md)

- Live slug normalization (`normalizeTenantSlugInput`) and availability check (`CheckSlugAvailabilityAsync` on backend).
- Optional **trial license** checkbox maps to `grantTrialLicense` (default **true** in API DTO).
- On success, `OnboardingSuccessModal` shows one-time **provisioning** payload (admin password, register id, demo product ids).

**Backend:** `AdminTenantService.CreateAsync` runs provisioning inside a DB transaction:

```247:254:backend/Services/AdminTenants/AdminTenantService.cs
            var (provisioning, provisionError) = await _provisioningService
                .ProvisionAsync(
                    tenant,
                    request.AdminEmail,
                    request.AdminPassword,
                    request.GrantTrialLicense,
                    cancellationToken)
```

See [Auto-provisioning](#auto-provisioning-tenantprovisioningservice).

### Edit tenant

- Inline edit modal on list page (name, email, phone, address, status).
- Full settings on detail → **Settings** tab (`TenantDetailSettingsTab`).

### Suspend / reactivate

- List actions: suspend → `status: suspended`; reactivate → `status: active`.
- Impersonation is **disabled** for suspended, deleted, or inactive tenants (API `400`).

### Delete

- **Soft delete** from list: `DELETE /api/admin/tenants/{id}` — sets `status=deleted`, `isActive=false`.
- **Hard delete** on detail (guarded): `hardDeleteAdminTenant` — only when safety checks pass (see backend tests).
- Legacy default tenant cannot be deleted.

### Impersonate (“Login as”)

- List, detail header, `SuperAdminTenantSelector`, and dev switcher no-admin flow.
- **Production:** redirect to `https://{slug}.regkasse.at/impersonate-callback#impersonate_token=…`
- **Development:** same-origin token + `dev_tenant_id` reload.

`ImpersonationRedirectOverlay` — `frontend-admin/src/features/super-admin/components/ImpersonationRedirectOverlay.tsx`

---

## How to create users for a tenant

1. Open tenant → tab **Benutzer** (or `/admin/tenants/{id}/users`).
2. Click **Benutzer anlegen**.
3. Enter **E-Mail**, optional name, **Rolle** (`Manager` / `Cashier` / `Accountant`, …), optionally **Mandanten-Administrator (Owner)**.
4. Confirm — backend creates Identity user + membership; returns a **one-time password** in the UI (no invitation email).

**Bestehenden Benutzer hinzufügen** — attach an existing account (`AddExistingUserModal` → assign membership only; no new login).

Full role and password semantics: [`USER_MANAGEMENT.md`](USER_MANAGEMENT.md).

## Tenant user management (reference)

**UI:** `TenantDetailUsersTab` — `frontend-admin/src/features/super-admin/components/TenantDetailUsersTab.tsx`  
**API:** `frontend-admin/src/features/super-admin/api/tenantUsers.ts`

| Operator action (DE) | API | Component |
|----------------------|-----|-----------|
| Benutzer anlegen | `POST …/users` | `CreateUserModal` |
| Bestehenden Benutzer hinzufügen | `POST …/users/assign` | `AddExistingUserModal` |
| Rolle ändern | `PUT …/users/{userId}/role` | `TenantUserTable` |
| Inhaber setzen | `PUT …/users/{userId}` `{ isOwner: true }` | table action |
| Passwort zurücksetzen | reset endpoint via modal | `ResetPasswordModal` |
| Mitglied entfernen | `DELETE …/users/{userId}` | confirm in table |

Roles for create: `Manager`, `Cashier`, `Accountant`, … (`TENANT_CREATE_ROLES`).

**Owner admin email** on tenant list comes from `user_tenant_memberships` where `is_owner=true`, joined in `AdminTenantService.ListAsync` → DTO field `ownerAdminEmail`. This drives header switcher status (🟢/🟡) and “Kein Admin” warnings.

![Tenant users tab (placeholder)](images/tenant-management/fa-tenant-detail-users.png)

---

## License display: deployment vs tenant (Mandant)

FA shows **two separate license concepts**. Mixing them caused operator confusion; the UI now keeps them apart.

### 1. Deployment / server license (On-Premise)

- **Route:** `/admin/license` — page title *„Server-Lizenz (On-Premise)“* (`license.page.title`); subtitle clarifies this is **not** the header Mandantenlizenz.
- **API:** `/api/admin/license/*`, `/api/license/status` — `frontend-admin/src/api/manual/adminLicense.ts`
- **Scope:** API **host** / machine fingerprint / issued JWT rows (`issued_licenses`).
- **Super Admin on `admin.*`:** may open this route without mandant context (`SUPER_ADMIN_PLATFORM_ALLOWED_PREFIXES`).
- **Not shown** in shell header for Super Admin platform mode (`suppressLicenseWarnings`).

### 2. Tenant / Mandantenlizenz (SaaS row)

- **Stored on:** `tenants.license_key`, `tenants.license_valid_until_utc`.
- **Super Admin management:** tenant detail → **License** tab — `LicenseManager` + `/api/admin/tenants/{id}/license/*` (`adminTenantLicense.ts`).
- **Manager header badge:** `LicenseStatusIndicator` → `useHeaderTenantLicense` — only when `showTenantLicenseInHeader` (Manager + real tenant slug, not Super Admin). Shows **Mandantenlizenz** from `GET /api/tenants/switcher` (via `useCurrentTenant`), never deployment license.
- **Dev switcher rows:** `getTenantSwitcherLicenseBadge` — explicit hint *„Mandantenlizenz (Unternehmen)“*.

Resolution logic (shared):

```10:43:frontend-admin/src/features/super-admin/utils/tenantLicenseLabel.ts
export function resolveTenantLicenseLabel(
    licenseValidUntilUtc: string | null | undefined,
    licenseKey: string | null | undefined,
    now = Date.now(),
): TenantLicenseLabel {
    // … expired / trial (no key or ≤31 days) / valid …
}
```

Badge mapping for header pill (`headerLicenseStatus.ts`):

| Condition | CSS class | German label |
|-----------|-----------|--------------|
| No `license_valid_until_utc` | red | Keine Mandantenlizenz |
| Expired | red | Lizenz abgelaufen |
| ≤7 days left | orange | Lizenz läuft bald ab |
| >7 days left | green | Lizenziert |

Dev switcher / tenant list still use `mandantLicenseBadge.ts` → `mapTenantLicenseLabelToBadge`.

**Expiry banner (Manager only):** `LicenseExpiryBanner` — warning if ≤15 days, error if expired. Super Admin never sees it.

---

## How to switch between tenants (Super Admin)

| Environment | Mechanism |
|-------------|-----------|
| **Production** | **Als Mandant anmelden** (impersonate) → redirect `https://{slug}.regkasse.at/impersonate-callback#impersonate_token=…` |
| **Development** | Header **Firma wechseln** (`HeaderDevTenantSwitch`) → `GET /api/tenants/switcher` → `localStorage.dev_tenant_id` + reload |
| **Platform home** | `/admin` — `SuperAdminTenantSelector` search + impersonate |

Super Admin without mandant context sees **Super Admin Modus** in `TenantBadge`; most business routes require tenant selection or impersonation (`useSuperAdminTenantMode`).

If a tenant has **no owner admin** (🟡), switching triggers `TenantSwitcherNoAdminFlow` (create admin or impersonate).

## Tenant switcher behavior

### Development header switcher

**Component:** `HeaderDevTenantSwitch` — `frontend-admin/src/features/auth/components/HeaderDevTenantSwitch.tsx`  
**Visible when:** `NODE_ENV === 'development'` only (not production subdomain UI).

**Data source (DB-synchronized):**

```14:19:frontend-admin/src/features/tenancy/api/getApiAdminTenants.ts
/** GET /api/tenants/switcher — SuperAdmin sees all; others see active memberships only. */
export async function getApiAdminTenants(includeDeleted = false): Promise<AdminTenantListItem[]> {
    const { data } = await AXIOS_INSTANCE.get<AdminTenantListItem[]>('/api/tenants/switcher', {
```

- **Super Admin:** all non-deleted tenants from database.
- **Other users:** `ListForSwitcherAsync` filters to active `user_tenant_memberships`.

**UX features:**

| Feature | Implementation |
|---------|----------------|
| Search | `filterTenantsForHeaderSearch` — name, slug, admin email, contact email |
| Sort | Active tenants first, then `de` locale name; suspended last |
| Status icons | 🟢 active + owner admin; 🟡 active, no owner; 🔴 suspended; ⚫ deleted |
| Admin line | `ownerAdminEmail` under title |
| License tag | Mandant badge from `licenseValidUntilUtc` / `licenseKey` |
| No-admin guard | Super Admin switching to tenant without owner → `TenantSwitcherNoAdminFlow` (impersonate or create admin) |
| Persist | `persistTenantSlugAndRefresh` → `localStorage.dev_tenant_id` + full reload |

Utilities: `tenantHeaderSwitcher.ts` — `getTenantStatusIcon`, `getTenantHeaderTitle`, `sortTenantsForHeaderSwitcher`.

![Header tenant switcher (placeholder)](images/tenant-management/fa-header-tenant-switcher.png)

### Super Admin platform mode (no mandant selected)

On `admin.*` without impersonation / dev override:

- `TenantBadge` → *„Super Admin Modus“* (links to `/admin/tenants`).
- `SuperAdminModeBanner` + home `SuperAdminTenantSelector` (searchable select + table, impersonate).
- `requiresTenantSelection` from `useSuperAdminTenantMode` — most routes gated until tenant context exists.

### Shell header layout

Order in `AdminShellHeader` (`frontend-admin/src/components/layout/Header.tsx`):

`EnvironmentBadge` → `TenantBadge` → `HeaderDevTenantSwitch` (dev) → `LicenseStatusIndicator` (Manager **Mandantenlizenz** via `useHeaderTenantLicense`) → language → user menu.

---

## Auto-provisioning (`TenantProvisioningService`)

**File:** `backend/Services/AdminTenants/TenantProvisioningService.cs`

When create succeeds, provisioning (same transaction) typically creates:

| Asset | Default |
|-------|---------|
| Cash register | `KASSE-001`, location *Hauptkasse* |
| Category | *Allgemein*, 20% VAT |
| Demo products | 3 RKSV demo articles |
| Admin user | `admin@{slug}.regkasse.at` or `adminEmail`; password auto-generated if omitted |
| Membership | Owner + `Manager` role |
| Trial license | If `grantTrialLicense` and no `licenseValidUntilUtc` → **+30 days** UTC |

```145:151:backend/Services/AdminTenants/TenantProvisioningService.cs
        if (grantTrialLicense && !tenant.LicenseValidUntilUtc.HasValue)
        {
            trialUntil = now.AddDays(30);
            tenant.LicenseValidUntilUtc = trialUntil;
            tenant.UpdatedAt = now;
        }
```

Response includes `provisioning` DTO (one-time password, register id, product ids) for `OnboardingSuccessModal`.

---

## Key file index

| Area | Path |
|------|------|
| Tenant CRUD page | `frontend-admin/src/app/(protected)/admin/tenants/page.tsx` |
| Tenant detail | `frontend-admin/src/app/(protected)/admin/tenants/[tenantId]/page.tsx` |
| Super Admin API | `frontend-admin/src/features/super-admin/api/adminTenants.ts` |
| Tenant users API | `frontend-admin/src/features/super-admin/api/tenantUsers.ts` |
| Mandant license API | `frontend-admin/src/features/super-admin/api/adminTenantLicense.ts` |
| Switcher API hook | `frontend-admin/src/features/tenancy/api/getApiAdminTenants.ts` |
| Current tenant context | `frontend-admin/src/features/tenancy/hooks/useCurrentTenant.ts` |
| Header license hook | `frontend-admin/src/features/tenant/hooks/useHeaderTenantLicense.ts` |
| Backend provisioning | `backend/Services/AdminTenants/TenantProvisioningService.cs` |
| Backend tenant service | `backend/Services/AdminTenants/AdminTenantService.cs` |
| Switcher endpoint | `backend/Controllers/TenantsController.cs` |
| i18n (DE) | `frontend-admin/src/i18n/locales/de/admin-shell.json`, `license.json` |

---

## Tests

- `frontend-admin/src/features/super-admin/utils/__tests__/tenantHeaderSwitcher.test.ts`
- `frontend-admin/src/features/super-admin/utils/__tests__/tenantLicenseLabel.test.ts`
- `backend/KasseAPI_Final.Tests/TenantProvisioningServiceTests.cs`
- `backend/KasseAPI_Final.Tests/AdminTenantsControllerTests.cs` (`ListForSwitcherAsync`, create/provision)
