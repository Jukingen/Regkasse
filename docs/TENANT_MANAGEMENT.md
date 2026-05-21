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

Related: [`MULTI_TENANT.md`](MULTI_TENANT.md), [`LICENSE_SYSTEM.md`](LICENSE_SYSTEM.md), [`IMPERSONATION_FLOW.md`](IMPERSONATION_FLOW.md).

---

## Super Admin: tenant lifecycle

### Access

- **Route:** `/admin/tenants` — `frontend-admin/src/app/(protected)/admin/tenants/page.tsx`
- **Detail:** `/admin/tenants/[tenantId]` — tabs: overview, users, cash registers, license, settings
- **Role:** `SuperAdmin` or permission `system.critical`
- **API module:** `frontend-admin/src/features/super-admin/api/adminTenants.ts` → `GET|POST|PUT|DELETE /api/admin/tenants`

![Tenant list (placeholder)](images/tenant-management/fa-tenant-list.png)

### Create tenant

**UI:** `CreateTenantModal` — `frontend-admin/src/features/super-admin/components/CreateTenantModal.tsx`

- Live slug normalization (`normalizeTenantSlugInput`) and availability check (`CheckSlugAvailabilityAsync` on backend).
- Optional **trial license** checkbox maps to `grantTrialLicense` (default **true** in API DTO).
- On success, `CreateTenantSuccessModal` shows one-time **provisioning** payload (admin password, register id, demo product ids).

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

## Tenant user management

**UI:** `TenantDetailUsersTab` — `frontend-admin/src/features/super-admin/components/TenantDetailUsersTab.tsx`  
**API:** `frontend-admin/src/features/super-admin/api/tenantUsers.ts`

| Operator action (DE) | API | Component |
|----------------------|-----|-----------|
| Benutzer einladen | `POST …/users/invite` | `InviteUserModal` |
| Bestehenden Benutzer hinzufügen | `POST …/users` | `AddExistingUserModal` |
| Rolle ändern | `PUT …/users/{userId}/role` | `TenantUserTable` |
| Inhaber setzen | `PUT …/users/{userId}` `{ isOwner: true }` | table action |
| Passwort zurücksetzen | reset endpoint via modal | `ResetPasswordModal` |
| Mitglied entfernen | `DELETE …/users/{userId}` | confirm in table |

Roles for invite: `Manager`, `Cashier`, `Accountant` (`INVITE_TENANT_ROLES`).

**Owner admin email** on tenant list comes from `user_tenant_memberships` where `is_owner=true`, joined in `AdminTenantService.ListAsync` → DTO field `ownerAdminEmail`. This drives header switcher status (🟢/🟡) and “Kein Admin” warnings.

![Tenant users tab (placeholder)](images/tenant-management/fa-tenant-detail-users.png)

---

## License display: deployment vs tenant (Mandant)

FA shows **two separate license concepts**. Mixing them caused operator confusion; the UI now keeps them apart.

### 1. Deployment / server license (On-Premise)

- **Route:** `/admin/license` — page title *„Lizenz (On-Premise)“* (`license.page.title`).
- **API:** `/api/admin/license/*`, `/api/license/status` — `frontend-admin/src/api/manual/adminLicense.ts`
- **Scope:** API **host** / machine fingerprint / issued JWT rows (`issued_licenses`).
- **Super Admin on `admin.*`:** may open this route without mandant context (`SUPER_ADMIN_PLATFORM_ALLOWED_PREFIXES`).
- **Not shown** in shell header for Super Admin platform mode (`suppressLicenseWarnings`).

### 2. Tenant / Mandantenlizenz (SaaS row)

- **Stored on:** `tenants.license_key`, `tenants.license_valid_until_utc`.
- **Super Admin management:** tenant detail → **License** tab — `LicenseManager` + `/api/admin/tenants/{id}/license/*` (`adminTenantLicense.ts`).
- **Manager header badge:** `LicenseStatusIndicator` — only when `showTenantLicenseInHeader` (Manager + real tenant slug, not Super Admin).
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

Badge colors for i18n labels: `mandantLicenseBadge.ts` → `mapTenantLicenseLabelToBadge`.

| Kind | Tag color (Ant Design) | German label pattern |
|------|------------------------|----------------------|
| `expired` | `red` | Mandanten-Lizenz: ABGELAUFEN |
| `trial` / ≤31 days | `orange` if ≤7 days, else `blue` | TESTVERSION / N Tage |
| `valid` (paid) | `green` | LIZENZIERT |

**Expiry banner (Manager only):** `LicenseExpiryBanner` — warning if ≤15 days, error if expired. Super Admin never sees it.

---

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
| No-admin guard | Super Admin switching to tenant without owner → `TenantSwitcherNoAdminFlow` (impersonate or invite) |
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

`EnvironmentBadge` → `TenantBadge` → `HeaderDevTenantSwitch` (dev) → `LicenseStatusIndicator` (Manager mandant) → language → user menu.

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

Response includes `provisioning` DTO (one-time password, register id, product ids) for `CreateTenantSuccessModal`.

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
