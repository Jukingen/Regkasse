# License system (Regkasse)

> Distinguishes **deployment (server) license** from **tenant (Mandant) SaaS license**, and documents how Frontend Admin (FA) surfaces each role.  
> For issuance JWT design and POS renewal, see also [`LICENSE_MANAGEMENT_DESIGN.md`](LICENSE_MANAGEMENT_DESIGN.md).

## Terminology (German ↔ English)

| German (operator UI) | English (technical) | Storage / API |
|----------------------|----------------------|---------------|
| Server-Lizenz / On-Premise | **deployment license** | `LicenseService`, encrypted file store, `activated_licenses`, `issued_licenses` |
| Mandantenlizenz | **tenant license** | `tenants.license_key`, `tenants.license_valid_until_utc` |
| Testversion | trial | No key or ≤31 days remaining (UI heuristic) |
| LIZENZIERT | valid / paid | `license_key` set and not in trial window |
| Maschinen-Fingerprint | machine hash | SHA-256 hex in deployment status / JWT binding |

---

## Two license layers

```mermaid
flowchart TB
    subgraph deployment [Deployment license]
        LS[LicenseService singleton]
        FILE[Encrypted local store]
        ISS[issued_licenses audit]
        API1["/api/admin/license/*"]
        API2["/api/license/status"]
    end

    subgraph tenant [Tenant Mandantenlizenz]
        ROW[tenants table columns]
        API3["/api/admin/tenants/{id}/license/*"]
    end

    subgraph fa [Frontend Admin]
        PAGE["/admin/license On-Premise"]
        HDR[LicenseStatusIndicator]
        TAB[LicenseManager on tenant detail]
    end

    LS --> FILE
    LS --> ISS
    PAGE --> API1
    HDR --> ROW
    TAB --> API3
```

### Deployment license

- **Purpose:** Entitle the **API host** (and features like `admin_license_manage`, RKSV tooling) and POS devices via activation JWT.
- **Not tenant-scoped:** `activated_licenses` is machine/deployment-local; singleton `LicenseService` uses `IServiceScopeFactory` for DB (see `MULTI_TENANT.md` § Background services).
- **FA route:** `/admin/license` — German title *„Lizenz (On-Premise)“*.
- **Client:** `frontend-admin/src/api/manual/adminLicense.ts`, `LicenseReportsCard`, generation/activation cards.

### Tenant (Mandant) license

- **Purpose:** SaaS subscription for a **company** (tenant row); shown in Super Admin tenant list/detail and Manager header when on tenant host.
- **Provisioning default:** new tenant create with `grantTrialLicense: true` → `license_valid_until_utc = now + 30 days` if not explicitly set (`TenantProvisioningService`).
- **Super Admin ops:** extend key/date, activate trial, set tier — `LicenseManager` + `adminTenantLicense.ts`.
- **Does not replace** deployment license: FA shell explicitly states the admin UI itself is not license-gated (`license.badge.tenant.baseTooltip`).

---

## FA display by role

| Role / context | Deployment license UI | Tenant license UI |
|----------------|----------------------|-------------------|
| **Super Admin** on `admin.*` (platform mode) | `/admin/license` allowed; header **no** deployment badge; `TenantBadge` = *Super Admin Modus* | Tenant list/detail columns; **no** `LicenseExpiryBanner` / `LicenseStatusIndicator` (`suppressLicenseWarnings`) |
| **Super Admin** impersonating tenant | Same as tenant context for data APIs; badge purple (impersonation) | Mandant data via impersonation JWT |
| **Manager** on `{slug}.*` | `/admin/license` if `settings.view` | `LicenseStatusIndicator` + `LicenseExpiryBanner` |
| **Dev** any role | `HeaderDevTenantSwitch` shows mandant license tag per row | Uses `GET /api/tenants/switcher` |

### Header components

| Component | File | When visible |
|-----------|------|--------------|
| `TenantBadge` | `components/admin-layout/TenantBadge.tsx` | Any authenticated user; platform vs mandant labels |
| `LicenseStatusIndicator` | `components/admin-layout/LicenseStatusIndicator.tsx` | `useHeaderTenantLicense` → `mode === 'tenant'` |
| `LicenseExpiryBanner` | `components/admin-layout/LicenseExpiryBanner.tsx` | Manager mandant; ≤15 days warning, expired error |

`useHeaderTenantLicense` loads mandant fields by matching `ctx.tenantSlug` against `GET /api/admin/tenants` (or switcher list) — **not** `/api/admin/license/status`.

```21:36:frontend-admin/src/features/tenant/hooks/useHeaderTenantLicense.ts
    const mode: HeaderLicenseMode = useMemo(() => {
        if (
            !ctx.hasAuthToken ||
            ctx.isSuperAdminPlatformMode ||
            ctx.suppressLicenseWarnings ||
            !ctx.showTenantLicenseInHeader
        ) {
            return 'hidden';
        }
        return 'tenant';
    }, [/* … */]);
```

`showTenantLicenseInHeader` is true for **Manager** on a real tenant slug (`useCurrentTenant.ts`).

---

## Expiry warnings (colors & tooltips)

### Header tag (`LicenseStatusIndicator`)

Mapped in `mandantLicenseBadge.ts`:

| State | Ant Design `color` | German label (i18n key) |
|-------|-------------------|-------------------------|
| Expired | `red` | `license.badge.tenant.expired.label` |
| Trial / ≤31 days, >7 days | `blue` | `license.badge.tenant.trial.label` |
| Trial / ≤7 days | `orange` | same trial label |
| Paid / valid | `green` | `license.badge.tenant.licensed.label` |

Tooltips append `license.badge.tenant.baseTooltip` (*Mandant*, not server).

### Content banner (`LicenseExpiryBanner`)

| Condition | Alert `type` | Threshold |
|-----------|--------------|-----------|
| `license.kind === 'expired'` or `daysRemaining < 0` | `error` | always |
| `0 < daysRemaining ≤ 15` | `warning` | `WARNING_THRESHOLD_DAYS = 15` |

Messages: `license.banner.expired.*`, `license.banner.warning.*` in `frontend-admin/src/i18n/locales/de/license.json`.

### Tenant list / switcher

- List page uses `resolveTenantLicenseLabel` → compact DE table cell (e.g. `Demo (12 T.)`, `Abgelaufen`).
- Switcher uses full i18n badge via `getTenantSwitcherLicenseBadge`.

---

## Trial auto-provisioning

**Create tenant API:** `CreateAdminTenantRequest.GrantTrialLicense` defaults to `true` (`AdminTenantDtos.cs`).

**Provisioning:**

1. If request already sets `licenseValidUntilUtc`, trial skip in provisioning unless business rules override in `CreateAsync` body.
2. Else if `grantTrialLicense && !tenant.LicenseValidUntilUtc` → set **30 days** UTC on tenant row before commit.

**UI:** `CreateTenantModal` checkbox bound to `grantTrialLicense`; success modal shows `trialLicenseValidUntilUtc` from provisioning DTO when present.

**Post-create:** Super Admin can extend via `LicenseManager` → *Testversion aktivieren* (`POST …/license/trial`) or manual key/date.

---

## Super Admin tenant license tab

`TenantDetailLicenseTab` → `LicenseManager` — `frontend-admin/src/features/super-admin/components/LicenseManager.tsx`

- Loads `GET /api/admin/tenants/{tenantId}/license`
- Actions: trial activation, extend (`licenseKey`, `validUntilUtc`), tier `basic|standard|premium`
- History table from API `history[]`
- Link to deployment license page for issued JWT workflow: `/admin/license`

![Tenant license tab (placeholder)](images/tenant-management/fa-tenant-detail-license.png)

---

## Deployment license page (reference)

![On-Premise license page (placeholder)](images/tenant-management/fa-deployment-license.png)

- Status card uses `/api/admin/license/status` (server truth).
- POS parity panel may show `/api/license/status`.
- Feature gating: `deploymentLicenseAllows` in `shared/licenseDeploymentFeatures.ts` for export/RKSV admin features.

---

## Related backend services

| Service | Scope |
|---------|--------|
| `LicenseService` | Deployment; startup snapshot; anonymous health/status |
| `AdminTenantService` | Tenant CRUD; `ownerAdminEmail` on list |
| `TenantProvisioningService` | Create-time assets + trial date |
| Tenant license endpoints | Controllers under `/api/admin/tenants/{id}/license` (see OpenAPI) |

---

## Related documentation

| Document | Topic |
|----------|--------|
| [`TENANT_MANAGEMENT.md`](TENANT_MANAGEMENT.md) | CRUD, switcher, user management |
| [`MULTI_TENANT.md`](MULTI_TENANT.md) | Isolation, impersonation, switcher API |
| [`LICENSE_MANAGEMENT_DESIGN.md`](LICENSE_MANAGEMENT_DESIGN.md) | Target JWT/renewal architecture |
| [`CHANGELOG_TENANT_MANAGEMENT.md`](CHANGELOG_TENANT_MANAGEMENT.md) | Dated FA/backend changes |
