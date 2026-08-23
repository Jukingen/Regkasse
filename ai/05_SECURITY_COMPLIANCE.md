# Security and compliance

## Legal / compliance claims (critical)

- Documentation or code comments in this repo **do not guarantee legal compliance**. In particular they do not claim BMF/FinanzOnline acceptance, TSE hardware approval, or an official DEP/RKSV declaration.
- **Fiscal export** (`GET /api/admin/fiscal-export`, `FiscalExportService`): packages carry an explicit **“not legal proof”** warning; they are for diagnostics, internal analysis, and operational handoff — not official RKSV evidence or a FinanzOnline substitute (`REGKASSE_AI_ONBOARDING.md`).
- **DEP §7 export** (`GET /api/admin/rksv/dep-export`, `RksvDepExportService`): BMF Signaturjournal format (F1–F5 complete); Prüftool verification is a goal, still without a legal-acceptance guarantee. Separate from official fiscal export; `docs/DEP_EXPORT_DEVELOPMENT.md`.

## Multi-tenant architecture

- Tenant data is separated with EF global query filters; unauthorized cross-tenant access returns **404** (no existence leak).
- Super Admin: `SuperAdmin` role + `/api/admin/tenants`; impersonation uses a short-lived token — `audit_logs.impersonated_by` / `impersonated_tenant` are populated.
- **Production (single POS UI):** No pre-auth tenant bind on reserved `pos` / `api` / `admin` hosts; after login **JWT `tenant_id`** is authoritative (`TenantContextMiddleware`). `{slug}.regkasse.at` is not the POS entry — `docs/POS_PRODUCTION_ARCHITECTURE.md`.
- **Development:** `X-Tenant-Id` / `?tenant=` only when `IsDevelopment()`; closed in Production.
- **Host / custom domain:** Host resolution continues for legacy slug hosts and customer sites (`TenantDomain` → slug); storefront `frontend-sites`.
- Offline: two separate systems (`offline_transactions` vs `offline_orders`); voucher secrets are never written to any offline queue.

Full architecture: `docs/MULTI_TENANT.md`.

## Multi-tenant security

### Tenant isolation guarantees

| Guarantee | Implementation |
|-----------|----------------|
| Database-level filtering | `AppDbContext` global query filter on all `ITenantEntity` types; API clients cannot bypass it |
| Singleton + EF | Resolving scoped `AppDbContext` from the root is forbidden; `IServiceScopeFactory` is required (`LicenseService` example) |
| Accessor null | Filters are off when `TenantId == null` — only deliberate code paths; a normal API request sets tenant first |
| Cross-tenant IDOR | **404** (not 403) — `TenantIsolationTests.AdminPayments_GetById_CrossTenant_Returns404_Not403` |
| `tenant_id` stamp | `offline_transactions.tenant_id` and `offline_orders.tenant_id` NOT NULL; stamped from ambient / register tenant on insert |
| Platform tables | Identity / `tenants` are not tenant-scoped; business tables (for example `Customer`) are `ITenantEntity` — inventory before changing this |

### Tenant spoofing prevention

| Control | Implementation |
|---------|----------------|
| Production POS/API | Reserved hosts + JWT `tenant_id`; no `X-Tenant-Id` / `?tenant=` |
| Host slug parsing | `SubdomainTenantProvider` / `TenantDomain` — legacy slug hosts and sites; not the POS production entry |
| Dev headers off in Production | `ASPNETCORE_ENVIRONMENT=Production` |
| Super Admin extra check | `[Authorize(Roles = SuperAdmin)]` + actor SuperAdmin verification on impersonation |

### Known gap (read before touching)

- **JWT `tenant_id` ↔ Host match:** No subdomain requirement on reserved `pos`/`api`/`admin` hosts; claim↔Host match on legacy slug hosts is not fully enforced in middleware yet — `docs/MULTI_TENANT.md` “Known gaps”.
- Impersonation audit columns **exist**; FA legacy `{slug}` handoff vs shared `admin.regkasse.at` target: `docs/IMPERSONATION_FLOW.md`.

## Authentication / authorization

- Auth: ASP.NET Core Identity + JWT.
- Authz: `HasPermission(...)` policy approach is the main standard; role matrix `backend/Authorization/RolePermissionMatrix.cs`.
- **Admin FA session:** Login and `/me` responses filter permissions with `AdminAppPermissionProfile` (`app_context=admin`) — Cashier whitelist, POS-terminal strip for Manager. Menu/route contract: `frontend-admin` `test:contract`, backend `RoleAdminMenuContractTests`.
- **Access & roles hub:** `/admin/access`, `/admin/users`, `/admin/access/roles`, `/admin/access/matrix` — detail `frontend-admin/docs/ACCESS_AND_ROLES_HUB.md`.
- **SuperAdmin 2FA:** TOTP; Dev bypass — `docs/AUTH_TWO_FACTOR.md`.
- **CSRF:** `Security:Csrf` + `CsrfMiddleware` (double-submit on mutations); Dev bypass possible — `AGENTS.md` § CSRF.
- **Working hours:** website/app online-order intake only; never close POS/FA APIs — `docs/WORKING_HOURS.md`.
- **Backup RBAC:** Mandanten-Admin `backup.manage` (tenant); System dump / restore Super Admin — `ai/modules/backup_permissions.md`.
- On new endpoints, assume auth unless a public requirement is explicit.

## Voucher / Gutschein

- Do **not** log plaintext voucher codes or persist them (hash + masked display).
- Never write voucher secrets into the POS offline queue (`pendingPaymentQueue.ts` + `paymentService.ts`).

## Fiscal / compliance sensitive areas

- TSE signature and verification flows; no client-flag bypass of signature/TSE.
- **Signature chain** and **`signature_chain_state` / receipt sequence** consistency
- RKSV special-receipt lifecycle (Nullbeleg, Startbeleg, Monatsbeleg, Jahresbeleg, Schlussbeleg)
- **Decommissioned** register: must not accept a new session/payment (backend guardrails are final)
- Daily closing / report close
- FinanzOnline: session SOAP path may be config-dependent; **RKSV Startbeleg/Jahresbeleg SOAP submit is not production-complete in this repo** (skeleton + Fake/Disabled defaults) — outbox/tracking tables remain sensitive.
- Audit log and legal-hold fields

## Change rule

- Narrow the scope and write risks explicitly before changing behavior here.
- No silent error swallowing, audit reduction, or authorization loosening.
- Money/rounding behavior must stay aligned with current production behavior.
