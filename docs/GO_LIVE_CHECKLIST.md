# Go-Live Checklist — Regkasse Cloud (SaaS, ~10 customers)

**Purpose:** Detailed operational checklist to launch the multi-tenant Cloud product for the first **~10 paying mandants**.  
**Based on:** [`CLOUD_PRODUCTION_READINESS.md`](CLOUD_PRODUCTION_READINESS.md)  
**Also required:** [`DOCKER_PRODUCTION_READINESS.md`](DOCKER_PRODUCTION_READINESS.md) · [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md) · [`DEPLOYMENT_COMPLIANCE.md`](DEPLOYMENT_COMPLIANCE.md) · [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md) · [`PRODUCTION_DEPLOYMENT_RUNBOOK.md`](PRODUCTION_DEPLOYMENT_RUNBOOK.md)

| Field | Value |
|-------|--------|
| **Target go-live date** | [YYYY-MM-DD] |
| **Programme owner** | [Name] |
| **ComplianceOfficer** | [Name] |
| **Last updated** | 2026-08-07 |

**Production hosts**

```text
POS UI:  https://pos.regkasse.at
FA UI:   https://admin.regkasse.at
API:     https://api.regkasse.at
```

> **No-go if any of these are true at cutover:** FON still Simulation in Production; TSE Soft/Demo/Fake or lock bypassed; ephemeral disk for DB / DEP / data-exports; no signed AVV for first paying customers; DEP history without durable stored files; no on-call and no successful System backup in the last 7 days.

**How to use:** Tick boxes with date + evidence (ticket, screenshot, command output). Prefer a project board that mirrors section IDs.

**Highest priority after this doc (work next):**

1. **TSE Production Configuration** (P0)  
2. **FinanzOnline Production Configuration** (P0)  
3. **Backup Strategy** (P1)  
4. **Monitoring Setup** (P1)  
5. **Customer Onboarding Process** (P2)

---

## 1. Pre-Go-Live (4 weeks before)

Maps to readiness Weeks **1–4** (DNS/TLS, TSE/FON, backup, monitoring).

### 1.1 Infrastructure setup

- [ ] **Production DNS configuration**
  - [ ] `api.regkasse.at` → Production API
  - [ ] `admin.regkasse.at` → Production Admin UI (FA)
  - [ ] `pos.regkasse.at` → Production POS (Single POS UI; tenant from JWT)
  - [ ] Optional: Sites / custom domains if sold (`frontend-sites`, verified `TenantDomain`)
  - [ ] SSL/TLS certificates installed; HTTPS redirect verified
  - [ ] Wildcard `*.regkasse.at` only if still needed for legacy/custom hosts (not required for Single POS UI)
  - [ ] Reserved labels respected: `pos`, `api`, `admin`, `www`

- [ ] **Production environment**
  - [ ] EU/EEA hosting region chosen; disk encryption at rest
  - [ ] PostgreSQL production database provisioned (HA optional; backups mandatory)
  - [ ] Redis provisioned and healthy
  - [ ] Production compose / host stack with resource limits (API, DB, Redis, UIs)
  - [ ] Production config via env / secret store (never commit secrets)
  - [ ] Connection strings tested from API containers
  - [ ] JWT secret rotated (≥32 chars); old keys retired
  - [ ] Durable volumes mounted:
    - [ ] Postgres data
    - [ ] `App_Data/dep-exports` (or `DepExportStorage:StorageRootRelativeDirectory`)
    - [ ] DEP archive root (if `DepExportArchive` enabled)
    - [ ] `App_Data/data-exports` (GDPR ZIP)
    - [ ] Backup staging / archive roots
  - [ ] `DataExport:PublicApiBaseUrl` = `https://api.regkasse.at` (no trailing slash)
  - [ ] `DepExportStorage` production paths + `CleanupEnabled` / `CleanupIntervalHours` verified
  - [ ] EF migrations applied (incl. DEP download-token migration); DB backup taken **before** migrate
  - [ ] `ASPNETCORE_ENVIRONMENT=Production` confirmed on API

- [ ] **Backup strategy**
  - [ ] Automated System backup schedule (cron) enabled
  - [ ] Tenant backup available to Mandanten-Admin (`backup.manage`)
  - [ ] Retention: Tenant **~30d**, System **~90d** (or documented policy)
  - [ ] Restore **validation** on isolated DB tested (dual Super Admin approval understood)
  - [ ] **No** automatic restore to production
  - [ ] Mandanten-Admin can list/download **own** tenant packages; cannot see System dumps
  - [ ] Backup failure alerts configured (activity + Slack/on-call)
  - [ ] Disk usage alert ~**80%** on backup staging + export volumes

**Refs:** [`BACKUP_AND_DISASTER_RECOVERY.md`](BACKUP_AND_DISASTER_RECOVERY.md) · [`BACKUP_PERMISSIONS.md`](BACKUP_PERMISSIONS.md) · [`DOCKER_PRODUCTION.md`](DOCKER_PRODUCTION.md)

### 1.2 TSE & FinanzOnline setup

- [ ] **TSE Production configuration**  
  **Refs:** [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md) · [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md)
  - [ ] `Tse:TseMode=Device` (or approved Real/Device posture — **not** `Off` / `Demo`)
  - [ ] `Tse:Mode=Real` (**not** `Fake`)
  - [ ] `Tse:Provider=fiskaly` (or approved vendor; soft/stub forbidden in Production)
  - [ ] Align `RKSV:Mode` / `RKSV:TseMode` with Production lock validators
  - [ ] Fiskaly (or vendor) API credentials in secret store
  - [ ] TSE device(s) / SCU provisioned per canary + pilot registers
  - [ ] TSE health check passing; `/health/tse/mode` fail-closed as expected
  - [ ] NTP / time sync OK (`isSynchronized`; online fiscal blocked if offset bad)
  - [ ] Soft/Fake/Demo **forbidden** in Production (no unsafe escape hatch in live)

- [ ] **FinanzOnline Production configuration**  
  **Refs:** [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md)
  - [ ] Complete **TEST** BMF drills before Production cutover
  - [ ] `FinanzOnline:Session:UseSimulation=false` (and Registrierkassen / TransmissionQuery layers as required)
  - [ ] `FinanzOnline:RksvSubmission:ClientKind=Real` (**not** `Fake` in Production)
  - [ ] FON credentials configured (TID, BENID, PIN / benutzerschlüssel) per mandant policy
  - [ ] FON session test successful (ComplianceOfficer sign-off)
  - [ ] Kasse + SCU registered in FON for canary / first pilots
  - [ ] Startbeleg path proven on TEST, then Production canary (**Startbeleg is not auto-created**)
  - [ ] Ausfall / outbox retry policy understood for Production
  - [ ] Production FON mode only after **ComplianceOfficer** sign-off

**Week −4…−2 exit:** DNS/TLS green; durable volumes; TSE locked; FON Production gated; health live/ready OK.

---

## 2. Week before go-live

Maps to readiness **Week 5** (+ final verification of Weeks 1–4).

### 2.1 Data migration & seeding

- [ ] **Production database migrations**
  - [ ] All migrations applied
  - [ ] `dotnet ef migrations list` (or equivalent) shows no pending
  - [ ] Migration rollback / restore-from-backup plan documented
  - [ ] DEP download-token columns present on `dep_export_history`

- [ ] **Tenant onboarding readiness (target ~10)**
  - [ ] Onboarding kit dry-run on **internal canary** mandant complete
  - [ ] Process ready to create 10 tenant rows (do **not** enable all 10 on day 0 — prefer 1–3 pilots first)
  - [ ] Each pilot: Mandanten-Admin user created
  - [ ] Each pilot: ≥1 cash register + TSE commissioning path
  - [ ] Products: demo catalog **or** real import agreed with customer
  - [ ] License key / sale row ready (`REGK-{yyyyMMdd}-{slug}-…`)
  - [ ] AGB + AVV signed before fiscal Production use

### 2.2 Monitoring & alerts

- [ ] **Monitoring setup**  
  **Refs:** [`MONITORING.md`](MONITORING.md) · [`ALERTING.md`](ALERTING.md)
  - [ ] Prometheus scrapes API `/metrics`
  - [ ] Health: `/api/health/live`, `/api/health/ready` (and FA/POS health as deployed)
  - [ ] Uptime / blackbox monitoring on public hosts
  - [ ] Error rate + latency dashboards
  - [ ] FA `/admin/monitoring` accessible to Super Admin
  - [ ] Sentry (FA) project + basic alert recipes

- [ ] **Alerting setup**
  - [ ] Critical errors → Slack / email / on-call webhook
  - [ ] TSE health / fleet alerts
  - [ ] FinanzOnline submission failure alerts
  - [ ] Backup failure / missing Succeeded run alerts
  - [ ] License expiry reminders (30 / 14 / 7 days) verified
  - [ ] Controlled alert fire test on Staging (API down / fiscal degrade)
  - [ ] On-call rota named for first 10 customers (even founder-led)

### 2.3 Security & compliance

- [ ] **Security hardening**
  - [ ] `ASPNETCORE_ENVIRONMENT=Production`
  - [ ] CSRF enabled (`Security:Csrf:Enabled=true`; no Dev bypass in Production)
  - [ ] SuperAdmin 2FA enabled (`TwoFactorAuth:Enabled=true`; no Dev bypass)
  - [ ] Rate limiting configured
  - [ ] Security headers enabled
  - [ ] CORS restricted to production domains
  - [ ] Production clients do **not** use `X-Tenant-Id` / `?tenant=`
  - [ ] Cross-tenant IDOR smoke → HTTP **404** (not 403)
  - [ ] Tenant hard-delete disabled in Production (soft-delete / archive only)
  - [ ] Swagger disabled or network-restricted in Production

- [ ] **Compliance**
  - [ ] GDPR AVV/DPA prepared from [`DPA_TEMPLATE.md`](DPA_TEMPLATE.md); Annex C sub-processors filled
  - [ ] Privacy policy (Datenschutzerklärung) published
  - [ ] AGB published; fiscal responsibility of mandant clear
  - [ ] Customer SLA PDF from [`SLA_CUSTOMER.md`](SLA_CUSTOMER.md) (99.5%, exclusions)
  - [ ] TOMs + breach runbook (≤72h supervisory where required)
  - [ ] RKSV compliance / cutover docs signed for Production image
  - [ ] Data retention matrix communicated (RKSV ≥7 years vs GDPR Delete)
  - [ ] GDPR data-management smoke: View / Export ZIP / Delete approval (canary); RKSV rows retained
  - [ ] DEP export smoke: generate → history `Completed` → download by id → token issue/expire
  - [ ] Prüftool spot-check on one stored DEP file (canary)
  - [ ] `DepExportCleanupHostedService` / archive purge enabled per policy (hot cleanup ≠ 7-year archive wipe)

### 2.4 Documentation

- [ ] **Customer documentation**
  - [ ] User manual / quickstart (German) for POS cashiers + Mandanten-Admin
  - [ ] FAQ (incl. cancel → Grace → Locked → Export / Delete; RKSV kept)
  - [ ] Support email / phone defined (matches SLA)
  - [ ] Onboarding / welcome email template ready
  - [ ] Cashier one-pager: offline limit **50**, voucher never offline, when to escalate

- [ ] **Internal documentation**
  - [ ] Runbooks: API down, TSE unhealthy, FON fail, backup fail, DEP download fail
  - [ ] Escalation procedures (Support → Engineering → ComplianceOfficer)
  - [ ] On-call contacts list
  - [ ] Staging → Canary → Production promote path; `deployment.approve` separation of duties
  - [ ] Link pack: this checklist + readiness + Docker gate + RKSV cutover + DEP + SLA + DPA

**Week −1 exit:** Legal pack ready; monitoring live; security Production; canary onboarding dry-run done; smoke suite green.

---

## 3. Go-Live day

### 3.1 Pre-launch verification

- [ ] **Deployment verification**
  - [ ] Backend API responding (`GET /api/health` and `/api/health/live` / `ready`)
  - [ ] Admin UI accessible (`https://admin.regkasse.at/login`)
  - [ ] POS UI accessible (`https://pos.regkasse.at`)
  - [ ] Swagger disabled or restricted
  - [ ] Current image tag has ComplianceOfficer `deployment.approve` sign-off
  - [ ] Docker production readiness Part A evidence complete

- [ ] **Smoke tests**
  - [ ] API smoke (auth, health, tenant isolation 404)
  - [ ] Admin UI smoke (login, permission-gated pages)
  - [ ] POS login + cart smoke
  - [ ] POS payment + **TSE signature** successful (canary / first pilot)
  - [ ] Receipt generation successful
  - [ ] Tagesabschluss path verified (or scheduled same day with ComplianceOfficer)
  - [ ] DEP export + download successful
  - [ ] Tenant backup list/download (Mandanten-Admin scope) OK
  - [ ] FON submission path healthy (or explicitly deferred only if Go criteria allow — default: required)

### 3.2 Customer onboarding

Prefer **1–3 pilots on day 0**, then stagger remaining mandants over Week 1–2.

#### Per-mandant kit (repeat for each)

| Step | Action | Done |
|------|--------|------|
| O-1 | Sign AGB + AVV (+ SLA annex) | [ ] |
| O-2 | Create tenant + first Mandanten-Admin | [ ] |
| O-3 | Create/apply license sale key | [ ] |
| O-4 | Company Stammdaten + ATU tax number | [ ] |
| O-5 | Cash register + TSE commissioning + **Startbeleg** | [ ] |
| O-6 | FON credentials (Production) | [ ] |
| O-7 | Train: Tagesabschluss, Storno, DEP download, backup download | [ ] |
| O-8 | Optional: website / online orders (non-fiscal) | [ ] |
| O-9 | Hand over: support contact, SLA PDF, `/tenant/[id]/data-management` | [ ] |

- [ ] **First customer (pilot #1)**
  - [ ] Tenant provisioned
  - [ ] Admin credentials delivered securely
  - [ ] Welcome email sent
  - [ ] Support contact provided
  - [ ] Startbeleg + first real payment signed

- [ ] **Remaining customers (toward #2–10)**
  - [ ] Schedule staggered enablement (not all at once)
  - [ ] Credentials + welcome emails prepared
  - [ ] Capacity check before each batch (API replicas, disk, Fiskaly seats)

**Refs:** [`CUSTOMER_ONBOARDING.md`](CUSTOMER_ONBOARDING.md) · readiness §4.6

### 3.3 Go-live announcement

- [ ] Internal team notified
- [ ] Support team briefed (known issues, escalation)
- [ ] Monitoring dashboard open for launch window
- [ ] Rollback plan reviewed (Section 6)
- [ ] Status / incident template ready

---

## 4. Post-Go-Live (Week 1)

### 4.1 Monitoring & validation

- [ ] **Day 1**
  - [ ] Error rates checked (API / FA / POS)
  - [ ] TSE health checked
  - [ ] FinanzOnline submissions / outbox checked
  - [ ] Backup completion verified (Succeeded run)
  - [ ] DEP storage disk headroom checked
  - [ ] Pilot customer can take payments end-to-end

- [ ] **Day 2–7**
  - [ ] Performance metrics reviewed (latency, error ratio)
  - [ ] Customer feedback collected
  - [ ] Support tickets triaged within SLA (critical ≤1h business hours)
  - [ ] SLA compliance spot-check
  - [ ] After ~2 weeks stable: invite next mandants toward ~10

### 4.2 Quick wins

- [ ] Address critical bugs (P0 / P1) within SLA
- [ ] Update docs / FAQ from real ticket themes
- [ ] Schedule first maintenance window (Week 2+) with ≥48h notice if customer-facing
- [ ] Sev-1 postmortem within 5 business days if applicable

---

## 5. Go / No-Go decision criteria

### 5.1 Decision table

| Criteria | Status |
|----------|--------|
| All critical pre-launch checklist items complete? | ⬜ Yes / No |
| Smoke tests passing (incl. TSE + DEP download)? | ⬜ Yes / No |
| Backups verified (Succeeded + validation restore)? | ⬜ Yes / No |
| Monitoring alerts configured and tested? | ⬜ Yes / No |
| On-call engineer / rota available? | ⬜ Yes / No |
| Rollback plan reviewed (and preferably tested)? | ⬜ Yes / No |
| Customer onboarding kit ready? | ⬜ Yes / No |
| AVV signed for first paying pilots? | ⬜ Yes / No |
| TSE Real/Device + FON Production (not Simulation)? | ⬜ Yes / No |
| Durable volumes for DB + DEP + data-exports? | ⬜ Yes / No |
| Tenant isolation 404 verified? | ⬜ Yes / No |
| ComplianceOfficer production image sign-off? | ⬜ Yes / No |

**Decision:** ⬜ **GO** / ⬜ **NO-GO** / ⬜ **GO with conditions**

### 5.2 Automatic No-Go triggers

| # | Trigger | If true → **No-Go** |
|---|---------|---------------------|
| N1 | FinanzOnline still Simulation / Fake client in Production | |
| N2 | TSE Soft / Demo / Fake or production lock bypassed | |
| N3 | Ephemeral FS for DB or DEP / data-exports | |
| N4 | No signed AVV for first paying customers | |
| N5 | DEP download still metadata-only (no stored file) | |
| N6 | No on-call **or** no System backup Succeeded in last 7 days | |

### 5.3 Decision record

| Field | Value |
|-------|--------|
| **Meeting date** | [YYYY-MM-DD] |
| **Decision** | ⬜ GO · ⬜ NO-GO · ⬜ GO with conditions |
| **Conditions / follow-ups** | |
| **First customer enable date** | [YYYY-MM-DD] |
| **Signed — Programme owner** | |
| **Signed — ComplianceOfficer** | |
| **Signed — Founder / Product Owner** | |

---

## 6. Rollback plan (emergency)

Use only if go-live fails in a way that cannot be fixed forward safely.

### 6.1 Application rollback

1. Redeploy previous known-good image tag (ComplianceOfficer aware).
2. Switch load balancer / reverse proxy to previous version.
3. Smoke: `/api/health/live`, `/api/health/ready`, FA login, POS login (read-only fiscal if unsure).
4. Confirm TSE/FON posture still Production-locked on the rolled-back build.

### 6.2 Database rollback

1. Prefer **forward fix** for additive EF migrations.
2. If schema must revert: restore from pre-migrate backup to **isolated** verification first.
3. Production DB restore is **last resort** — document data-loss risk; never casual `pg_restore` over live fiscal data.
4. Tenant ZIP packages are **not** full `pg_restore` substitutes (see backup docs).

### 6.3 Communication

1. Notify affected customers (impact, start UTC, workaround, next update).
2. Give estimated resolution time aligned with SLA.
3. Post-mortem after resolution (Sev-1 within 5 business days).

---

## 7. Timeline summary

| Phase | Timeline | Key activities |
|-------|----------|----------------|
| **Pre-Go-Live** | Week −4 to −2 | Infrastructure, DNS/TLS, TSE/FON lock, durable volumes |
| **Pre-Go-Live** | Week −2 to −1 | Backup proof, monitoring/alerts, DEP/GDPR smoke |
| **Week before** | Week −1 | Legal pack, security hardening, docs, canary onboarding dry-run |
| **Go-Live day** | Week 0 | Deploy verify, smoke, 1–3 pilots, monitoring window |
| **Post-Go-Live** | Week 1–2 | Daily health, tickets, stagger customers toward ~10 |

**Capacity sketch (~10 mandants):** plan ~2–5 POS terminals each; API ≥2 replicas preferred; alert storage at 80%. See readiness §4.7.

---

## 8. Owner & sign-off

| Role | Owner | Sign-off date |
|------|-------|---------------|
| **Technical Lead / Engineering** | | |
| **Operations** | | |
| **ComplianceOfficer** | | |
| **Product Owner / Founder** | | |
| **Support lead** | | |
| **Finance (billing)** | | |

---

## Progress summary

| Phase | Focus | Exit met? | Notes |
|-------|--------|-----------|-------|
| §1 Pre-Go-Live | Infra, TSE/FON, backup | ⬜ | |
| §2 Week before | Migrations, monitoring, security, docs | ⬜ | |
| §3 Go-Live day | Deploy, smoke, pilots | ⬜ | |
| §4 Post Week 1 | Monitoring, fixes | ⬜ | |
| §5 Go/No-Go | Decision record | ⬜ | |

---

## Related documents

| Doc | Role |
|-----|------|
| [`CLOUD_PRODUCTION_READINESS.md`](CLOUD_PRODUCTION_READINESS.md) | Master readiness (GDPR, DEP, SLA, pricing, deploy gate) |
| [`SLA_CUSTOMER.md`](SLA_CUSTOMER.md) | Customer-facing SLA |
| [`DPA_TEMPLATE.md`](DPA_TEMPLATE.md) | AVV / Art. 28 |
| [`DOCKER_PRODUCTION_READINESS.md`](DOCKER_PRODUCTION_READINESS.md) | Container gate |
| [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md) | Fiscal cutover |
| [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md) | FON Production cutover |
| [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md) | TSE Production lock |
| [`DEPLOYMENT_COMPLIANCE.md`](DEPLOYMENT_COMPLIANCE.md) | Deploy audit + sign-off |
| [`DEP_EXPORT_DEVELOPMENT.md`](DEP_EXPORT_DEVELOPMENT.md) | DEP generate + download |
| [`CUSTOMER_ONBOARDING.md`](CUSTOMER_ONBOARDING.md) | Mandant onboarding flow |
| [`MONITORING.md`](MONITORING.md) · [`ALERTING.md`](ALERTING.md) | Observability |
| [`BACKUP_AND_DISASTER_RECOVERY.md`](BACKUP_AND_DISASTER_RECOVERY.md) | Backup / DR |

---

## Next step

Work through items in this order unless ComplianceOfficer overrides:

1. **TSE Production Configuration** (P0) — §1.2  
2. **FinanzOnline Production Configuration** (P0) — §1.2  
3. **Backup Strategy** (P1) — §1.1  
4. **Monitoring Setup** (P1) — §2.2  
5. **Customer Onboarding Process** (P2) — §2.4 / §3.2  

Say which item to tackle first (recommended: **TSE Production Configuration**).

---

## Revision history

| Date | Change |
|------|--------|
| 2026-08-07 | Initial 6-week tabular plan from Cloud Production Readiness |
| 2026-08-07 | Expanded detailed checkbox Go-Live Checklist (pre-launch → Week 1, Go/No-Go, rollback, owners) aligned with readiness + actual TSE/FON config keys |
