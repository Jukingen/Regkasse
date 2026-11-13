# Cloud Production Readiness Guide — Regkasse (SaaS)

> **Decision:** Regkasse ships as a **multi-tenant Cloud (SaaS)** product.  
> On-premise / single-tenant deployment license remains a separate layer (`LICENSE_SYSTEM.md`) and is **out of scope** for go-live with mandant customers on shared hosts.

**Last updated:** 2026-08-07  
**Audience:** Founders, ComplianceOfficer, Super Admin ops, engineering  
**Target:** First **~10 paying mandants** on production cloud hosts

| Related | Link |
|---------|------|
| Production hosts | [`POS_PRODUCTION_ARCHITECTURE.md`](POS_PRODUCTION_ARCHITECTURE.md) |
| Docker / host gate | [`DOCKER_PRODUCTION_READINESS.md`](DOCKER_PRODUCTION_READINESS.md) · [`DOCKER_PRODUCTION.md`](DOCKER_PRODUCTION.md) |
| RKSV deploy gate | [`DEPLOYMENT_COMPLIANCE.md`](DEPLOYMENT_COMPLIANCE.md) · [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md) |
| Backup / DR | [`BACKUP_AND_DISASTER_RECOVERY.md`](BACKUP_AND_DISASTER_RECOVERY.md) |
| Mandant billing | [`BILLING_TENANT_LICENSE.md`](BILLING_TENANT_LICENSE.md) |
| DEP §7 | [`DEP_EXPORT_DEVELOPMENT.md`](DEP_EXPORT_DEVELOPMENT.md) |
| Monitoring | [`MONITORING.md`](MONITORING.md) · [`ALERTING.md`](ALERTING.md) |
| Agent rules | [`../AGENTS.md`](../AGENTS.md) § Multi-Tenant, GDPR, Backup, DEP |
| DPA / AVV template | [`DPA_TEMPLATE.md`](DPA_TEMPLATE.md) (Art. 28 GDPR) |
| Customer SLA | [`SLA_CUSTOMER.md`](SLA_CUSTOMER.md) (print to 1–2 page PDF) |
| Go-live checklist | [`GO_LIVE_CHECKLIST.md`](GO_LIVE_CHECKLIST.md) (detailed ~10-customer launch) |

```text
POS UI:  https://pos.regkasse.at
FA UI:   https://admin.regkasse.at
API:     https://api.regkasse.at
```

---

## 0. Executive summary

Before taking real fiscal traffic for ~10 customers, treat these five workstreams as a single gate:

| # | Workstream | Must be true |
|---|------------|--------------|
| 1 | **GDPR / DSGVO** | Legal docs + in-product rights (View / Export / Delete) + AVV + retention policy |
| 2 | **Data export** | GDPR ZIP download + **DEP §7 download** reliable for Betriebsprüfung |
| 3 | **SLA** | Written uptime / support / fiscal incident promises you can actually meet |
| 4 | **Ops / deploy** | Staging → Canary → Production, backups, monitoring, TSE/FON locked |
| 5 | **Pricing** | Clear SaaS packages, VAT invoices, grace/lock lifecycle understood by sales |

**Do not** promise on-prem restore-to-production, 99.99% uptime, or “we delete all fiscal data on request” — RKSV retention and product architecture forbid those claims.

---

## 1. GDPR / DSGVO compliance checklist

Regkasse is a **processor** (Auftragsverarbeiter) for mandant business data and a **controller** for its own SaaS account / billing / platform logs. Fiscal rows are dual-constrained: **DSGVO + RKSV retention (≥7 years)**.

### 1.1 Legal & contracts (do before first paid customer)

- [ ] **Privacy policy (Datenschutzerklärung)** published (website + linked from FA login / settings)
- [ ] **Terms of service (AGB)** covering SaaS subscription, fiscal responsibility of the mandant, and service limits
- [ ] **AVV / DPA (Auftragsverarbeitungsvertrag)** template ready; signed with every mandant (Art. 28 GDPR)
- [ ] **Sub-processor list** maintained (hosting, email, SMS if any, Sentry, Fiskaly/TSE vendor, DNS, payment for *your* invoices if used)
- [ ] **TOMs** (technical/organisational measures) one-pager: encryption in transit, access control, backups, tenant isolation, audit logs
- [ ] **Imprint (Impressum)** + company contact for data-protection inquiries
- [ ] **DPO / Datenschutzkontakt** named (internal role or external DSB) with a monitored mailbox
- [ ] **Records of processing activities** (Art. 30) for SaaS ops + mandant processing categories
- [ ] **Breach procedure** (Art. 33/34): detect → contain → notify supervisory authority ≤72h when required → notify mandant
- [ ] Clarify **roles in AVV**: customer = Verantwortlicher for POS/customer data; Regkasse = Auftragsverarbeiter for hosted processing

### 1.2 Product capabilities already in codebase (verify in production)

| Right / flow | Product surface | Notes |
|--------------|-----------------|-------|
| **Access (View)** | FA `/tenant/[id]/data-management` | Inventory summary; auto-approved |
| **Portability (Export)** | Same + ZIP via `DataExportService` | Opaque link `GET /data/download/{token}`; default TTL **7 days** (`DataExport:DownloadLinkValidDays`) |
| **Erasure (Delete)** | Same; Super Admin execute | **Manual approval + 7-day wait**; purge keeps RKSV fiscal rows |
| **License lifecycle** | Grace 7d → Locked 8–30d → Archived >30d | POS blocked when Locked+; FA read-only; see `LicenseGracePeriodConfig` |
| **Tenant isolation** | JWT `tenant_id` + EF filters | Cross-tenant → **HTTP 404** (not 403) |
| **Audit / activity** | `AuditEventType` + `ActivityEventType` | Security + bell/SSE; avoid secrets in payloads |

**Production config to set:**

```yaml
DataExport:
  PublicApiBaseUrl: "https://api.regkasse.at"   # no trailing slash
  DownloadLinkValidDays: 7
  DownloadPathTemplate: "/data/download/{token}"
```

- [ ] Confirm export artifacts land on durable disk (`App_Data/data-exports/` or mounted volume)
- [ ] Confirm email/activity `DataExportReady` / `DataAccessDeleteRequested` reach Mandanten-Admin
- [ ] Confirm Delete never removes payments / receipts / fiscal invoices / TSE / audit required for RKSV
- [ ] SuperAdmin **2FA** enabled in Production (`TwoFactorAuth:Enabled=true`; no Dev bypass)
- [ ] CSRF enabled in Production (`Security:Csrf:Enabled=true`)
- [ ] JWT secret ≥32 chars; secrets only via host env / secret store (never git)

### 1.3 Retention matrix (publish a customer-facing version)

| Data class | Minimum retention | Erasure on GDPR Delete? |
|------------|-------------------|-------------------------|
| Payments, receipts, TSE signatures, DEP-relevant fiscal | **7 years** (RKSV) | **No** — masked/retained |
| Audit / security logs | **7 years** | No (legal hold) |
| Products, categories, customers, company settings, non-fiscal invoices | Business need | **Yes** (after approved Delete + wait) |
| Identity credentials | Until account end / membership soft-remove | Soft-remove / deactivate on purge |
| GDPR export ZIP + download token | Token TTL (7d); file per ops policy | Purge after expiry |
| DEP export JSON on disk | Ops + archive policy (`DepExportStorage` / archive retention) | Not a substitute for RKSV store |
| Backups | Tenant default **30d**; System **90d** (config) | Separate from GDPR Delete |

### 1.4 Organisational checklist

- [ ] Staff access: least privilege; Super Admin impersonation audited
- [ ] No production DB dumps on laptops without encryption + ticket
- [ ] Vendor DPAs signed (cloud host, Fiskaly, email provider, error tracking)
- [ ] Cookie / tracking: FA/POS — only necessary cookies + documented analytics if any
- [ ] International transfers: prefer EU/EEA hosting; document SCCs if a sub-processor is outside EEA
- [ ] Customer FAQ: “What happens to my Kassen data if I cancel?” → Grace → Locked → Archived → Export → optional Delete (RKSV kept)

### 1.5 Explicit non-goals (do not claim)

- Full erasure of **signed fiscal history** on customer request  
- Automatic restore of deleted business data without backup purchase / ops process  
- Using `IgnoreQueryFilters()` for normal mandant support (Super Admin ops only)

---

## 2. Data export improvements

Two different export products — do not conflate them in support tickets or AGB:

| Export | Purpose | Audience | Primary API |
|--------|---------|----------|-------------|
| **GDPR / mandant data ZIP** | Portability / exit | Mandanten-Admin (`backup.manage`) | Data-management + `/data/download/{token}` |
| **DEP §7 (BMF JSON)** | Betriebsprüfung / Signaturjournal | Admin with `report.export` + `audit.view` | `/api/admin/rksv/dep-export*` |

### 2.1 GDPR data export — production hardening

Already implemented: collect → ZIP → opaque token → notify (`DataExportReady`).

**Before go-live:**

- [ ] `DataExport:PublicApiBaseUrl` points at public API HTTPS host
- [ ] Disk volume for `App_Data/data-exports/` is backed up / has free space alerts
- [ ] Smoke: Manager requests Export → receives link → downloads ZIP → opens `data-export.json` (`regkasse.tenant-data-export.v2`)
- [ ] Confirm RKSV fields are **masked** (`***`) in export JSON
- [ ] Retry worker (`DataRightsExportProcessorService`) runs in Production
- [ ] Document support SLA for failed exports (see §3)

### 2.2 DEP export download fix (planned / in progress)

**Problem addressed:** History rows existed, but operators needed a reliable way to **re-download** completed DEP JSON without re-running a full period export (and without leaking cross-tenant files).

**Design (aligned with current code):**

| Piece | Behaviour |
|-------|-----------|
| On-disk store | `DepExportStorage:StorageRootRelativeDirectory` (default `App_Data/dep-exports`) |
| Auth download by id | `GET /api/admin/rksv/dep-export/download/{exportId}` and `…/history/{id}/download` |
| Opaque token | Columns `download_token`, `download_token_expires_at_utc` on `dep_export_history` |
| Auto-issue | `DepExportStorage:IssueDownloadTokenOnComplete` (default `true`) |
| Token TTL | `DepExportStorage:DownloadTokenTtlHours` (default **24**) |
| Issue / rotate | `POST /api/admin/rksv/dep-export/download/{exportId}/token` |
| Token download | `GET /api/admin/rksv/dep-export/download/token/{token}` (still JWT + permissions; tenant filter → 404) |
| Migration | `20260807120000_AddDepExportHistoryDownloadToken` |

**FA / activity:**

- [ ] History UI shows download when file is stored; surface token expiry if exposed in DTO (`HasActiveDownloadToken` / `downloadTokenExpiresAtUtc`)
- [ ] Activity / email payloads use **download URL or token**, never embed full DEP JSON
- [ ] Scheduled DEP jobs write to the same storage root and issue tokens when completed

**Production checklist:**

- [ ] Apply migration on Staging, then Production
- [ ] Mount durable volume for `App_Data/dep-exports` (and archive root if used)
- [ ] Configure:

```yaml
DepExportStorage:
  StorageRootRelativeDirectory: "/data/dep-exports"   # or Windows absolute path
  DownloadTokenTtlHours: 24
  IssueDownloadTokenOnComplete: true
```

- [ ] Smoke: manual DEP export → history row `Completed` → download by id → download by token → expired token → 404 `RKSV_DEP_EXPORT_TOKEN_INVALID`
- [ ] Smoke: wrong tenant / missing tenant → **404**
- [ ] Prüftool spot-check on a stored file (`scripts/verify-rksv-dep-export.ps1`) for at least one canary mandant
- [ ] Audit: successful exports still log `RksvDepExportJson`; downloads recorded via download-history side effects where enabled

**Still recommended (follow-ups, not blockers for 10 customers if smoke passes):**

- [ ] Optional public-style link pattern similar to GDPR (`PublicApiBaseUrl`) only if product needs email deep-links without FA session — prefer staying on authenticated admin routes for DEP
- [ ] Retention job: purge expired tokens; archive/purge files per `DepExportArchive` policy
- [ ] Alert when storage disk &gt; 80% (reuse backup staging disk alert pattern)

### 2.3 Support cheat sheet

| Symptom | Likely cause | Action |
|---------|--------------|--------|
| `RKSV_DEP_EXPORT_FILE_NOT_FOUND` | Missing `storage_path` / disk wiped | Re-run export; fix volume |
| `RKSV_DEP_EXPORT_TOKEN_INVALID` | Expired / rotated token | `POST …/token` again |
| Empty / invalid BMF JSON | Missing cert / bad JWS | See `DEP_EXPORT_DEVELOPMENT.md` |
| GDPR link 404 | Token &gt;7d or file purged | Re-request Export |
| Cross-tenant download attempt | Isolation working | Expect **404** |

---

## 3. SLA documentation — what to promise

Write SLAs you can **measure** with existing monitoring ([`ALERTING.md`](ALERTING.md), [`MONITORING.md`](MONITORING.md)). Over-promising fiscal “zero downtime” is a legal risk.

### 3.1 Recommended customer-facing SLA (v1 — first 10 mandants)

Publish as `SLA` / AGB annex. Adjust numbers only after 30–60 days of production metrics.

| Metric | Promise (recommended) | Measurement |
|--------|----------------------|-------------|
| **Platform availability** | **99.5%** monthly for API + POS + FA (excluding planned maintenance) | Blackbox on `/api/health/live`, FA `/health`, POS `/healthz` |
| **Planned maintenance** | ≤4h / month; notice ≥48h (email / status page) | Change calendar |
| **Critical incident response** | Acknowledge ≤ **1 hour** business hours; ≤ **4 hours** 24/7 for “POS cannot take payments” | On-call (`ONCALL_WEBHOOK_URL`) |
| **Critical incident target restore** | Best effort **4 hours** (RTO aspirational); no hard money-back at v1 | Incident ticket |
| **Backup RPO** | System backup daily (cron); tenant backup per schedule — **RPO ≤ 24h** typical | `BackupScheduledEnqueueService` |
| **Support channel** | Email / ticket; FA activity for product events | Business hours Mo–Fr 09:00–17:00 Vienna (example) |
| **DEP / GDPR export** | Best effort **1 business day** if automated path fails | Support queue |
| **Digital service requests** | **1–2 business days** (already reflected in FA copy) | Existing UI SLA copy |
| **Security incident notice** | Without undue delay; supervisory ≤72h when applicable | Breach runbook |

**Credits (optional, keep simple for v1):** e.g. 5% monthly fee credit if monthly availability &lt; 99.5%, capped at 25% — only if finance agrees. Skip money-back until metrics are stable.

### 3.2 Explicit exclusions (must list)

- Customer internet, local printers, payment terminals, end-user devices  
- Fiskaly / TSE vendor outages outside Regkasse control (document dependency)  
- FinanzOnline / BMF outages  
- Force majeure, DDoS beyond capacity plan  
- Mandant misconfiguration (wrong FON credentials, decommissioned register misuse)  
- Soft-TSE / Simulation left on by customer request in non-prod  
- Recovery of data deleted after approved GDPR Delete (except RKSV retained rows / backup within retention)

### 3.3 Fiscal responsibility split

| Party | Responsible for |
|-------|-----------------|
| **Mandant** | Correct Stammdaten, daily closing discipline, Sonderbelege deadlines, FON credentials, Betriebsprüfung delivery of DEP |
| **Regkasse** | Platform availability, tenant isolation, TSE integration health, DEP generator correctness, backup of hosted data per policy |
| **Shared** | Incident communication when TSE/FON path fails |

### 3.4 Internal SLOs (ops — stricter than customer SLA)

Align alerts with what you already encode:

| Signal | Internal target | Alert |
|--------|-----------------|-------|
| API live | Fail ≥2m → page | `RegkasseApiDown` |
| API ready | Fail ≥5m → critical | `RegkasseApiNotReady` |
| Error ratio | &lt;5% / 5m | `RegkasseHighErrorRate` |
| Latency p95 | &lt;1s / 10m | `RegkasseHighLatencyP95` |
| TSE fleet / FON failures | Immediate page | Fiscal alerts |

Do **not** put internal SLOs into the customer contract unless you can staff 24/7.

### 3.5 Status & communication

- [ ] Public or customer status page (even a simple hosted page)  
- [ ] Incident template: impact, start UTC, workaround, next update time  
- [ ] Postmortem for Sev-1 (TSE/POS down, data leak suspicion) within 5 business days  

---

## 4. Deployment checklist — before ~10 live customers

Use this as the **business go-live** gate. Technical Docker/RKSV gates remain mandatory:

- [`DOCKER_PRODUCTION_READINESS.md`](DOCKER_PRODUCTION_READINESS.md)  
- [`DEPLOYMENT_COMPLIANCE.md`](DEPLOYMENT_COMPLIANCE.md)  
- [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md)

### 4.1 Infrastructure & hosts

- [ ] DNS + TLS for `pos` / `admin` / `api` (and Sites if sold)
- [ ] EU/EEA region hosting chosen; disk encrypted at rest
- [ ] Postgres HA or at least automated backups + tested restore to **isolated** DB (never prod restore by default)
- [ ] Redis healthy; API/FA/POS resource limits set
- [ ] Secrets via env / vault; `.env.production` not in git
- [ ] `FINANZONLINE_MODE=Production` only after ComplianceOfficer sign-off
- [ ] TSE production lock / Device|Real mode per [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md)
- [ ] NTP sync verified (`isSynchronized`)
- [ ] CSRF + SuperAdmin 2FA on
- [ ] Rate limiting / security headers enabled as in Production config

### 4.2 Multi-tenant SaaS posture

- [ ] Single POS UI architecture live ([`POS_PRODUCTION_ARCHITECTURE.md`](POS_PRODUCTION_ARCHITECTURE.md))
- [ ] Production clients do **not** send `X-Tenant-Id` / `?tenant=`
- [ ] Cross-tenant IDOR test → **404**
- [ ] Impersonation audited; support playbook for “Login as”
- [ ] Tenant hard-delete disabled in Production; soft-delete / archive only
- [ ] License grace / lock / archive behaviour demoed to support

### 4.3 Fiscal & compliance

- [ ] Startbeleg / payment / Tagesabschluss smoke on Staging + one canary mandant
- [ ] DEP export + **download by id/token** smoke (§2.2)
- [ ] Monatsbeleg / Jahresbeleg reminder config understood
- [ ] Offline limits documented for cashiers (50 offline TSE intents; voucher never offline)
- [ ] Deployment compliance gate + `deployment.approve` separation of duties

### 4.4 Backup & DR (SaaS)

- [ ] System backup schedule active; tenant backup available to Mandanten-Admin (`backup.manage`)
- [ ] Retention configured (Tenant 30d / System 90d or policy)
- [ ] Restore **validation** path tested on isolated DB; dual approval understood
- [ ] Customer communication: “we back up; you can download tenant packages; we do not auto-restore production”

### 4.5 Observability & support

- [ ] Prometheus + Alertmanager → Slack / on-call webhooks live
- [ ] Sentry (FA) project + alerts
- [ ] FA `/admin/monitoring` accessible to Super Admin
- [ ] Runbooks: API down, TSE unhealthy, FON fail, backup fail, DEP download fail
- [ ] Support mailbox + escalation to engineering
- [ ] On-call rota for first 10 customers (even if founder-led)

### 4.6 Customer onboarding kit (repeatable for each mandant)

1. Sign AGB + AVV  
2. Super Admin creates tenant + first Mandanten-Admin  
3. License sale / key (`REGK-{yyyyMMdd}-{slug}-…`) applied  
4. Company Stammdaten + tax number  
5. Cash register + TSE commissioning + Startbeleg  
6. FON credentials (Production)  
7. Train: Tagesabschluss, Storno, DEP export download, backup download  
8. Optional: website / online orders (non-fiscal)  
9. Hand over: support email, SLA PDF, data-management page URL  

### 4.7 Capacity sketch for ~10 mandants

| Resource | Starting point (adjust after metrics) |
|----------|----------------------------------------|
| Concurrent POS terminals | Plan ~2–5 per mandant → ~50 devices class |
| DB | Single primary OK if backups + disk headroom; plan read replica when CPU/IO &gt;70% sustained |
| API replicas | ≥2 behind load balancer for rolling deploys |
| Storage | Postgres + dep-exports + data-exports + backup staging; alert at 80% |
| Cost buffer | Hosting + Fiskaly + support time &gt; license COGS |

### 4.8 Go / no-go

**Go** only if: Docker readiness complete, RKSV cutover signed, GDPR pack signed for pilot customers, DEP download smoke green, on-call + backups verified, SLA PDF attached to AGB.

**No-go** if: FON still Simulation in Production, TSE unlocked/misconfigured, no durable volumes, no AVV, or DEP files only in memory/ephemeral container FS.

---

## 5. Pricing model — recommended SaaS POS packaging

Billing is already **mandant SaaS license sales** (`license_sales`, net + 20% USt default, PDF invoice). Prices are **commercial** — the product does not hard-code package amounts (tests use sample **€299 net / 12 months**).

### 5.1 Market positioning (Austria, RKSV cloud POS)

Compete on **compliance + multi-tenant admin + support**, not cheapest hardware bundle.

| Segment | Typical need | Regkasse fit |
|---------|--------------|--------------|
| Micro (1 Kasse, Café/Imbiss) | Simple POS + RKSV | Starter |
| SMB (2–5 Kassen, Gastro) | Multi-user, reports, backup | Business |
| Growing (5–10+ / multi-site) | Priority support, digital extras | Plus / custom |

### 5.2 Recommended packages (list prices, EUR **net**/month)

Charge **per mandant** with a **per cash-register** add-on so TSE/vendor cost scales.

| Package | Incl. registers | Monthly net | Yearly net (≈2 months free) | Includes |
|---------|-----------------|-------------|-----------------------------|----------|
| **Starter** | 1 | **€49** | **€490** | POS + FA, RKSV/TSE path, Tagesabschluss, basic reports, tenant backup, email support (business hours) |
| **Business** | up to 3 | **€99** | **€990** | + DEP schedules/history, priority email, online-order inbox, working-hours website gate |
| **Plus** | up to 8 | **€179** | **€1 790** | + phone/callback support target, higher export retention, digital website publish assist |
| **Extra Kasse** | +1 | **€25**/mo | **€250**/yr | Each additional register beyond package |

**Setup / onboarding (one-time, recommended):** **€149–€399** depending on remote vs on-site training (covers Startbeleg, FON, first DEP drill).

**Digital extras (optional, already permissioned separately):** website / app publish as add-on **€19–€49**/mo or one-time project fee — do not bury inside fiscal SLA.

Align Super Admin license sale UI with these list prices (or discount explicitly on the sale row). Example yearly Business: **€990 net** → record as 12-month sale (similar shape to billing preview tests).

### 5.3 What not to do early

- Unlimited registers on Starter (TSE + support cost blow-up)  
- Lifetime licenses for cloud (ops cost is recurring)  
- Bundling hardware margins into SaaS without separate SKU  
- Promising custom on-prem forks for €49/mo  

### 5.4 Commercial terms tied to product lifecycle

| Event | Product behaviour | Commercial suggestion |
|-------|-------------------|------------------------|
| Paid & active | Full POS + FA | Standard invoice |
| Days 1–7 overdue | Grace warnings | Soft dunning email |
| Days 8–30 | POS locked; FA read-only | Keep charging or pause per AGB; offer Export |
| &gt;30 days | Archived | Offboarding package; GDPR Export; Delete only on request |
| Churn | Soft-delete tenant | Keep RKSV data 7 years (storage cost → tiny archive fee optional later) |

Document in AGB: **no refund of unused license period** after Startbeleg in Production (optional goodwill credit).

### 5.5 Unit economics (sanity check for 10 customers)

Example mix: 6× Starter + 3× Business + 1× Plus ≈  
`6×49 + 3×99 + 179 ≈ €770` MRR net before extras.

Ensure **MRR &gt; (hosting + Fiskaly + observability + support hours)**. If TSE vendor is per-register, the **€25 extra Kasse** line must cover that COGS with margin.

### 5.6 Invoicing ops checklist

- [ ] Super Admin creates `license_sales` with correct net + 20% VAT  
- [ ] PDF invoice with company logo / UID  
- [ ] Keys format `REGK-{yyyyMMdd}-{tenantSlug}-{8}`  
- [ ] Reminders (`BillingReminderHostedService`) enabled  
- [ ] `billing_audit_log` reviewed periodically (not fiscal audit)

---

## 6. Single checklist — Cloud go-live (print this)

### Legal / GDPR
- [ ] AGB + Datenschutzerklärung + AVV template  
- [ ] Sub-processors + TOMs + breach runbook  
- [ ] Data-management View/Export/Delete verified on Production  
- [ ] Retention / RKSV exception explained to customers  

### Exports
- [ ] GDPR ZIP public base URL + volume  
- [ ] DEP storage volume + download by id  
- [ ] DEP download token issue / expire / rotate smoke  
- [ ] Prüftool spot-check  

### SLA / support
- [ ] Customer SLA PDF (99.5%, RTO/RPO, exclusions)  
- [ ] On-call + Slack alerts live  
- [ ] Status / incident template  

### Deploy / fiscal
- [ ] Docker production readiness complete  
- [ ] ComplianceOfficer production sign-off  
- [ ] TSE Real/Device + FON Production  
- [ ] System + tenant backup proven  
- [ ] Tenant isolation 404 test  

### Commercial
- [ ] Package prices decided and pasted into sales sheet  
- [ ] Onboarding fee + extra-Kasse SKU  
- [ ] First 10 mandants onboarded with checklist §4.6  

---

## 7. Document ownership

| Topic | Owner (suggested) | Review cadence |
|-------|-------------------|----------------|
| AVV / privacy | Founder + legal counsel | On sub-processor change |
| SLA numbers | Ops lead | Quarterly vs metrics |
| DEP / GDPR export tech | Backend | Each release touching exports |
| Pricing | Founder / sales | After first 10 customers |
| RKSV cutover | ComplianceOfficer | Every production promote |

---

## 8. Revision history

| Date | Change |
|------|--------|
| 2026-08-07 | Initial Cloud (SaaS) production readiness guide: GDPR, DEP download token plan, SLA, 10-customer deploy gate, pricing |

When this guide conflicts with executable CI / Compose / code, **code and CI win** — update this document in the same PR.
