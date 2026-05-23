# Changelog: tenant management & license display (FA)

Chronological summary of notable changes tied to Super Admin tenant UX, mandant license display, and dev tenant switching. Dates from git history on `main`/feature branches (2026-05).

---

## 2026-05-21 — Tenant switcher synchronized with database

**Summary:** Dev header switcher loads tenants from the API instead of static presets only; Super Admin sees all DB tenants; other users see membership-scoped rows.

**Changes:**

- New endpoint `GET /api/tenants/switcher` — `backend/Controllers/TenantsController.cs`
- `AdminTenantService.ListForSwitcherAsync` — Super Admin: full list; others: filter by `user_tenant_memberships`
- FA hook `useTenantListForSwitcher` → `getApiAdminTenants` (`frontend-admin/src/features/tenancy/api/getApiAdminTenants.ts`)
- `HeaderDevTenantSwitch`: search, sort, status emoji, admin line, mandant license tag, no-admin flow
- Utilities: `tenantHeaderSwitcher.ts` (+ unit tests)

**Commits (representative):** `cfac9ee`, `f32462e`

---

## 2026-05-20 — License display confusion fixed (Mandant vs deployment)

**Summary:** Separated **Mandantenlizenz** (tenant row) from **Server-Lizenz** (On-Premise `/admin/license`). Super Admin platform mode no longer shows misleading deployment expiry in header.

**Changes:**

- `LicenseStatusIndicator` — Manager + tenant context only; documents mandant SaaS license
- `useHeaderTenantLicense`, `mandantLicenseBadge.ts`, `resolveTenantLicenseLabel`
- `TenantBadge` / tooltips clarify Super Admin mode vs active company
- `LicenseExpiryBanner` — Manager mandant only; `suppressLicenseWarnings` for Super Admin
- i18n: `license.badge.tenant.*`, `adminShell.tenant.devSwitcher.licenseHintTooltip`
- Dev switcher license column labeled *Mandantenlizenz (Unternehmen)*

**Commits (representative):** `8be578f`, `04a0018`

---

## 2026-05-20 — Tenant provisioning service added

**Summary:** Atomic onboarding on tenant create: register, category, demo products, owner admin user, optional 30-day trial on tenant row.

**Changes:**

- `TenantProvisioningService` + `ITenantProvisioningService` — `backend/Services/AdminTenants/`
- Wired in `AdminTenantService.CreateAsync` inside transaction (rollback on provision failure)
- `CreateTenantModal` / `CreateTenantSuccessModal` — surfaces one-time password and asset ids
- Default admin email `admin@{slug}.regkasse.at`; `GenerateCompliantPassword()` when password omitted
- `grantTrialLicense` default `true` on `CreateAdminTenantRequest`

**Commits (representative):** `04a0018`

---

## 2026-05-21 — User management for tenants added

**Summary:** Super Admin can manage tenant memberships from detail UI: create user, add existing user, roles, owner, remove, reset password. (Invite-by-email flow removed 2026-05-22 — see `CHANGELOG_RECENT.md`.)

**Changes:**

- FA: `TenantDetailUsersTab`, `TenantUserTable`, `CreateUserModal`, `AddExistingUserModal`, `ResetPasswordModal`
- API client: `tenantUsers.ts` → `/api/admin/tenants/{tenantId}/users/*`
- Backend: `TenantUserService` (create, membership CRUD, password reset)
- List column `ownerAdminEmail` for admin visibility and switcher 🟡/🟢 indicators
- Cash registers tab and hard-delete tenant (same release wave)

**Commits (representative):** `cfac9ee`, `f32462e`

---

## 2026-05-19 — Super Admin platform routing & tenant gates (precursor)

**Summary:** Platform host without mandant requires tenant selection before most routes; home selector and banners.

**Changes:**

- `useSuperAdminTenantMode`, `SuperAdminModeBanner`, `SuperAdminTenantSelector`
- `TenantChangeListener` / layout gates
- Impersonation documentation updates

**Commits (representative):** `fa8b383`, `55a5838`

---

## 2026-05-20 — Slug availability on create (UX)

**Summary:** Live slug validation during tenant create.

**Changes:** `CheckSlugAvailabilityAsync`, `TenantSlugFieldExtras`, `tenantSlug.ts` validation

**Commit:** `edf5236`

---

## 2026-05-21 — Customer onboarding wizard & welcome email

**Summary:** Foolproof create flow with progress UI, rollback on failure, SMTP welcome mail.

**Changes:**

- `TenantOnboardingService` — transactional create + audit correlation
- `CreateTenantWizard`, `OnboardingErrorModal`, `OnboardingSuccessModal`
- `WelcomeEmailService` + `Email:Smtp` configuration
- Slug APIs: `slug-availability`, `slug-suggestions`

---

## Documentation added (2026-05-21)

- `docs/TENANT_MANAGEMENT.md`
- `docs/CUSTOMER_ONBOARDING.md`
- `docs/USER_MANAGEMENT.md`
- `docs/CASH_REGISTER_LIFECYCLE.md`
- `docs/LICENSE_SYSTEM.md`
- `docs/CHANGELOG_TENANT_MANAGEMENT.md` (this file)
- `docs/CHANGELOG_RECENT.md` — tenant wave summary
- `docs/MULTI_TENANT.md` — sections: Super Admin UI, license by role, switcher, JWT vs dev header
- `frontend-admin/README.md` — Super Admin, switching, license display
- Screenshot placeholders: `docs/images/tenant-management/`, `onboarding/`, `cash-registers/`, `user-management/`
