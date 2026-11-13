# Data Processing Agreement (DPA) / Auftragsverarbeitungsvertrag (AVV)

**Template — Regkasse Cloud (SaaS)**  
**Legal basis:** Article 28 GDPR (DSGVO)  
**Related:** [`CLOUD_PRODUCTION_READINESS.md`](CLOUD_PRODUCTION_READINESS.md) §1 (GDPR checklist)

> **Status:** Draft template for legal counsel review. Replace all `[brackets]` before use.  
> **Not legal advice.** Have Austrian counsel adapt language, governing law, and Annexes for your entity.  
> **Format:** Markdown source of truth. Export to PDF/DOCX for signature if needed.

**Version:** 1.0 · **Last updated:** 2026-08-07

---

## Cover / Parties

**This Data Processing Agreement (“DPA” / “AVV”)** is entered into between:

| Role | Party |
|------|--------|
| **Controller** (Verantwortlicher) | **[Customer legal name]** (“**Customer**” / “**Mandant**”), [address], [UID / Firmenbuchnummer] |
| **Processor** (Auftragsverarbeiter) | **[Regkasse operator legal name]** (“**Provider**” / “**Regkasse**”), [address], [UID / Firmenbuchnummer] |

**Effective date:** [YYYY-MM-DD]  
**Linked master agreement:** SaaS subscription / AGB dated [YYYY-MM-DD] (the “**Main Agreement**”)

This DPA forms part of the Main Agreement. In case of conflict on data-protection matters, **this DPA prevails**.

---

## Recitals

1. Customer uses the Provider’s multi-tenant cloud POS and admin platform (**Regkasse**) at approximately `pos.regkasse.at`, `admin.regkasse.at`, and `api.regkasse.at` (or successor hosts).
2. In providing the Service, Provider processes personal data on behalf of Customer.
3. The parties enter into this DPA to comply with Article 28 GDPR and applicable Austrian/EU data-protection law.
4. Customer remains **Controller** for personal data that Customer and its users enter into or generate via the Service (POS, FA, optional storefront/online orders). Provider is **Processor** for that processing. Provider acts as **independent Controller** solely for its own SaaS account administration, billing of Customer, platform security logs, and product improvement that does not require Customer content (as described in Provider’s privacy notice).

---

## 1. Definitions

Terms not defined here have the meaning in the GDPR. In particular:

- **Personal Data**, **Processing**, **Data Subject**, **Personal Data Breach**, **Sub-processor** — as in GDPR.
- **Service** — the Regkasse Cloud SaaS (POS, Frontend Admin, API, optional Sites/online-order features) under the Main Agreement.
- **Fiscal Data** — payment, receipt, invoice, TSE/signature, DEP, and related audit records required under Austrian RKSV / fiscal law.
- **Instructions** — documented instructions in this DPA, the Main Agreement, in-product configuration by authorised Customer users, and written instructions from Customer’s Datenschutzkontakt.

---

## 2. Subject matter and duration

2.1 Provider processes Personal Data solely to provide, secure, support, and improve the Service as described in **Annex A** (purposes) and **Annex B** (categories).

2.2 Duration equals the term of the Main Agreement plus any post-termination retention required by this DPA (including the **RKSV retention exception** in Clause 10) and by law.

2.3 Processing ends when Customer data is deleted or returned under Clause 11, subject to Clause 10.

---

## 3. Roles and Customer responsibilities

3.1 Customer determines the purposes and means of Processing of Mandant business data and is responsible for:

- Lawfulness of Processing (Art. 6 GDPR) toward Data Subjects (employees, cashiers, end customers, etc.);
- Informing Data Subjects (Art. 12–14) where required;
- Accuracy of Stammdaten and user accounts;
- Configuring roles/permissions and authorised administrators (Mandanten-Admin);
- Fiscal compliance duties of the cash-register operator (RKSV, FinanzOnline credentials, Tagesabschluss, Betriebsprüfung).

3.2 Customer warrants that Instructions do not cause Provider to violate GDPR or RKSV. If Provider believes an Instruction is unlawful, Provider shall inform Customer without undue delay and may suspend that Instruction until clarified.

---

## 4. Provider obligations (Art. 28)

Provider shall:

4.1 Process Personal Data **only on documented Instructions** from Customer, unless required by Union or Member State law (in which case Provider informs Customer unless legally prohibited).

4.2 Ensure persons authorised to process Personal Data are bound by confidentiality.

4.3 Implement **Technical and Organisational Measures (TOMs)** per **Annex D**, appropriate to the risk.

4.4 Engage Sub-processors only under Clause 6 and **Annex C**.

4.5 Assist Customer, insofar as reasonably possible and taking into account the nature of Processing, with Data Subject requests (Clause 7) and with DPIAs / prior consultation where applicable.

4.6 Assist Customer with Personal Data Breach obligations (Clause 8).

4.7 Make available information necessary to demonstrate compliance with Art. 28 and allow audits per Clause 9.

4.8 Delete or return Personal Data after end of services per Clause 11, subject to Clause 10.

4.9 Immediately inform Customer if, in Provider’s opinion, an Instruction infringes GDPR or other Union/Member State data-protection provisions.

---

## 5. Data processing purposes

See **Annex A**. Summary of permitted purposes:

| # | Purpose |
|---|---------|
| A | Operate multi-tenant POS / FA / API for Customer’s mandant |
| B | Authentication, authorisation, session and tenant isolation |
| C | Fiscal signing (TSE), receipt chain, DEP §7 export, FinanzOnline submission as configured |
| D | Backup, disaster-recovery validation, monitoring, security, abuse prevention |
| E | Customer support, impersonation (audited), incident response |
| F | In-product GDPR rights fulfilment (View / Export / Delete of non-RKSV data) |
| G | Optional digital services (website/PWA, online-order intake) if contracted |
| H | Billing of the SaaS subscription (Provider as Controller for invoice parties) |

Provider shall **not** sell Customer Personal Data or use it for unrelated advertising.

---

## 6. Sub-processors

6.1 Customer grants **general authorisation** for Provider to engage Sub-processors listed in **Annex C**, as updated under this Clause.

6.2 Provider shall impose data-protection obligations on each Sub-processor equivalent to those in this DPA (Art. 28(4)).

6.3 Provider remains liable to Customer for Sub-processor performance as if Provider performed the services itself.

6.4 **Change notice:** Provider will notify Customer of intended addition or replacement of a Sub-processor at least **[14 / 30]** days in advance (email or status page / FA notice). Customer may object on reasonable data-protection grounds within **[14]** days. If unresolved, Customer may terminate the affected Service for cause as set out in the Main Agreement.

6.5 **International transfers:** Prefer EU/EEA hosting. Where a Sub-processor processes outside the EEA, Provider shall ensure an Art. 44 et seq. transfer mechanism (e.g. adequacy decision or SCCs) and document it in Annex C.

---

## 7. Data Subject rights procedures

7.1 Customer is primarily responsible for responding to Data Subject requests. Provider assists via product features and support.

7.2 **In-product procedures** (Frontend Admin data-management, authorised users with `backup.manage` / applicable roles):

| Request type | Procedure | Typical timing |
|--------------|-----------|----------------|
| **Access / View** | Inventory / summary of Customer’s tenant data | Near-instant (auto-approved) |
| **Portability / Export** | ZIP artifact; opaque download link (default validity **7 days**) | Target under 24 hours; retry if failed |
| **Erasure / Delete** | Request → notification → Customer confirm → **minimum 7-day wait** → purge of **non-RKSV** data | Manual approval; Super Admin execute path as designed |
| **Rectification** | Customer corrects data in POS/FA; support assists if blocked | Best effort |
| **Restriction / objection** | Handled case-by-case via Datenschutzkontakt; may include account lock | Best effort |

7.3 Export artifacts may **mask** fiscal secrets (TSE/JWS/QR material shown as redacted) and exclude Identity credentials. Export is not a substitute for DEP §7 Betriebsprüfung export.

7.4 Provider will forward to Customer, without undue delay, any Data Subject request received directly that clearly relates to Customer’s Processing, unless Customer has instructed otherwise.

7.5 **Limitation:** Erasure of **Fiscal Data** is restricted under Clause 10. Provider will explain the RKSV exception to requesters via Customer’s process / product notices.

---

## 8. Personal Data Breach notification

8.1 Provider maintains a breach procedure: **detect → contain → assess → notify → remediate → postmortem**.

8.2 Without undue delay after becoming aware of a Personal Data Breach affecting Customer Personal Data, and in any event **within 48 hours** of confirmation of such Breach (target), Provider shall notify Customer’s Datenschutzkontakt with:

- Nature of the Breach (categories and approximate number of Data Subjects / records, if known);
- Likely consequences;
- Measures taken or proposed;
- Point of contact at Provider;
- Timeline of discovery and containment.

8.3 Customer remains responsible for notifying the supervisory authority (Art. 33 — **without undue delay and, where feasible, not later than 72 hours** after becoming aware) and Data Subjects (Art. 34) where required. Provider shall reasonably assist Customer with information needed for those notices.

8.4 Notification under this Clause is not an admission of fault or liability.

8.5 Provider contact for breaches: **[security@ / dpo@ domain]** (monitored). Customer contact: **[Customer DPO / email]**.

---

## 9. Audits and information

9.1 Upon reasonable written request (not more than once per 12 months, except after a Breach or regulatory inquiry), Provider shall provide:

- Up-to-date TOMs summary (Annex D);
- Current Sub-processor list (Annex C);
- High-level security / compliance documentation reasonably available.

9.2 On-site or deep technical audits require **[30]** days’ notice, confidentiality undertakings, and shall not unreasonably disrupt Service or other tenants. Provider may satisfy audit rights via third-party certificates / penetration-test summaries where available.

9.3 Customer bears its own audit costs unless a material breach of this DPA is found.

---

## 10. RKSV 7-year retention exception (mandatory)

10.1 **Conflict of laws.** Certain categories of Fiscal Data processed via the Service are subject to Austrian **Registrierkassensicherheitsverordnung (RKSV)** and related tax/record-keeping duties requiring retention for a minimum of **seven (7) years** (or longer if applicable law so requires).

10.2 **Exception to erasure.** Notwithstanding Art. 17 GDPR requests, Customer Instructions to “delete all data”, or Clause 11, Provider is **not obliged—and Customer instructs Provider not—to erase or anonymise** the following while legal retention applies:

- Payment records and related fiscal receipt data;
- TSE signatures, signature-chain / DEP-relevant material;
- Fiscal invoices linked to payments;
- Security and compliance **audit logs** required to demonstrate fiscal integrity;
- Other categories listed as non-erasable in Annex B / retention matrix.

10.3 **Product behaviour.** Customer acknowledges that the Service’s GDPR **Delete** flow removes eligible non-fiscal business data (e.g. products, categories, customers, company settings, non-fiscal invoices) after approval and waiting period, while **retaining** RKSV-required Fiscal Data (and may retain masked representations in exports).

10.4 **Controller duty.** Customer remains responsible for its own RKSV retention duties as cash-register operator. This Clause does not transfer fiscal operator liability to Provider.

10.5 **After retention expires.** When retention no longer applies and no other legal hold exists, Parties shall cooperate in good faith on deletion or further anonymisation of remaining Fiscal Data, subject to then-current law and technical feasibility (including backup lifecycle limits).

10.6 **No false marketing.** Provider does not claim, and Customer shall not represent to Data Subjects, that “all fiscal history can be deleted on request” while RKSV retention applies.

---

## 11. Return and deletion after end of Service

11.1 Upon termination or expiry of the Main Agreement (and after license grace / lock / archive phases described in product docs, if applicable), Customer may:

- Use in-product **Export** to obtain a portability ZIP; and/or
- Download **tenant backup** packages where permitted by the Main Agreement / permissions.

11.2 After Customer’s export opportunity and any contractually agreed wind-down, Provider shall delete or irreversibly anonymise **non-retained** Personal Data from production systems within a reasonable period (**target: [30–90] days**), and from backups according to backup retention (typical Tenant backup **~30 days**, System backup **~90 days**, configurable).

11.3 **Clause 10 prevails** for Fiscal Data and legally required audit logs.

11.4 Written certification of deletion is available on request for erasable categories.

---

## 12. Security — Technical and Organisational Measures

Provider implements TOMs described in **Annex D**, including encryption in transit, access control, multi-tenant isolation, backup, logging, and Super Admin controls (e.g. 2FA in Production). Customer shall protect its credentials, enforce least privilege for its users, and promptly revoke access for leavers.

---

## 13. Confidentiality

Each Party shall keep confidential the other Party’s non-public information obtained under this DPA, except where disclosure is required by law or to Sub-processors / advisors under equivalent confidentiality.

---

## 14. Liability

Liability under this DPA follows the Main Agreement, except that nothing excludes liability that cannot be limited under GDPR Art. 82 or mandatory law. Administrative fines imposed on one Party due to the other Party’s breach may be claimed as damages subject to the Main Agreement’s caps and exclusions, where legally permitted.

---

## 15. Governing law and venue

Governing law: **[Austria]**. Exclusive venue: **[Vienna / …]**, unless mandatory consumer or GDPR venue rules apply.

---

## 16. Amendments

Material changes to Annexes C or D follow Clause 6.4 (Sub-processors) or written agreement. Non-material clarifications may be published with version bump; continued use after notice constitutes acceptance where permitted by the Main Agreement.

---

## Signature block

| | Controller (Customer) | Processor (Provider) |
|--|----------------------|----------------------|
| Name | | |
| Title | | |
| Date | | |
| Signature | | |

---

# Annex A — Processing purposes (details)

| Purpose ID | Description | Legal reference (Customer) |
|------------|-------------|----------------------------|
| A1 | Host and operate POS cash-register workflows for Customer’s business | Art. 6(1)(b)/(f) as determined by Customer |
| A2 | Host Frontend Admin for users, products, reports, settings, backup, data management | Same |
| A3 | Authenticate users; issue JWT; enforce tenant isolation | Same |
| A4 | Create fiscal receipts; TSE signing; special receipts; daily closing | Customer’s RKSV / tax obligations |
| A5 | FinanzOnline outbox / submission when Customer configures Production mode | Customer’s tax reporting duties |
| A6 | DEP §7 export generation and download for tax audit | Customer’s Betriebsprüfung duties |
| A7 | Activity feed, email/webhook notifications for operational events | Legitimate ops / Customer config |
| A8 | Backups (tenant / system strategy) and restore **validation** (no automatic production restore) | Security / continuity |
| A9 | Security monitoring, abuse detection, incident response | Art. 32 / Provider & Customer interests |
| A10 | GDPR View / Export / Delete tooling for Customer’s rights fulfilment | Art. 12–23 assistance |
| A11 | Optional online orders / tenant websites (non-fiscal intake) | If contracted |
| A12 | Support including audited tenant impersonation by Super Admin | Customer request / contract |

---

# Annex B — Categories of data and Data Subjects

## B.1 Data Subjects

- Customer’s employees / cashiers / managers / accountants using POS or FA  
- Customer’s end customers (Beleg, loyalty/customer records if used, online-order contact data)  
- Customer’s technical contacts and administrators  
- (Indirect) visitors to Customer’s optional website/storefront  

## B.2 Categories of Personal Data

| Category | Examples | Erasure on GDPR Delete? |
|----------|----------|-------------------------|
| **Tenant / company data** | Company name, address, tax number (e.g. ATU…), settings, working hours | Eligible business settings: **Yes** (after process); tax identity may remain in fiscal context |
| **User / identity data** | Login identifier, name, role, permissions, membership; password hashes (not plaintext) | Soft-remove / deactivate on purge |
| **Product / catalog** | Products, categories, prices, modifiers | **Yes** |
| **Customer master data** | End-customer profiles if used | **Yes** |
| **Payment / fiscal data** | Amounts, payment methods (card numbers masked), receipt IDs, timestamps | **No** — RKSV ≥7 years (Clause 10) |
| **TSE / signature data** | Compact JWS, thumbprints, chain state, Sonderbelege, Tagesabschluss signatures | **No** — RKSV ≥7 years |
| **Invoices** | Fiscal invoices linked to payments; non-fiscal invoices | Fiscal: **No**; non-fiscal: **Yes** |
| **Orders** | POS/offline order snapshots; online orders (status fulfilment) | Online/non-fiscal: per product rules; fiscal-linked: retain as required |
| **Vouchers** | Ledger balances (codes never logged in plaintext offline) | Per product / legal need |
| **Audit / security logs** | Actor, action type, IP, correlation id, timestamps | **No** — typically ≥7 years / legal hold |
| **Export artifacts** | GDPR ZIP, DEP JSON on disk, download tokens | Tokens expire (e.g. 7d / 24h); files per ops retention — not a substitute for live fiscal store |
| **Backup artifacts** | Tenant/system backup packages | Separate retention (~30d / ~90d); not erased by GDPR Delete alone |
| **Support / telemetry** | Tickets, limited diagnostics; error tracking (prefer no secrets) | Per Sub-processor / ops policy |

**Special categories (Art. 9):** Service is **not designed** for processing special-category data. Customer shall not upload such data unless a separate written addendum and lawful basis exist.

---

# Annex C — Sub-processors (template list)

> Update before each customer signature. Prefer EU/EEA. Complete transfer tool column.

| # | Sub-processor | Service | Location | Transfer tool (if outside EEA) | Personal data involved |
|---|---------------|---------|----------|--------------------------------|------------------------|
| 1 | **[Cloud hosting provider, e.g. Hetzner / AWS eu-central / …]** | Compute, storage, DB hosting | **[EU/EEA]** | N/A or SCCs | Tenant DB, files, backups |
| 2 | **[DNS / CDN, e.g. Cloudflare]** | DNS, TLS, optional CDN | **[…]** | **[…]** | IP, request metadata |
| 3 | **[Email provider, e.g. …]** | Transactional email (export ready, license, breach notices) | **[…]** | **[…]** | Email, name, notification content |
| 4 | **[Fiskaly or TSE/crypto vendor]** | TSE / signature services as configured | **[…]** | **[…]** | Fiscal signing-related data |
| 5 | **[Error tracking, e.g. Sentry]** | Application error monitoring (FA/API as enabled) | **[…]** | **[…]** | Device/browser, truncated errors — no passwords/voucher codes |
| 6 | **[SMS provider — if used]** | Optional OTP/alerts | **[…]** | **[…]** | Phone numbers |
| 7 | **[Payment provider for Provider’s invoices — if used]** | Collect SaaS fees from Customer | **[…]** | **[…]** | Billing contact (Provider as Controller) |
| 8 | **[Status page / on-call — if used]** | Incident communication | **[…]** | **[…]** | Subscriber email |

Customer acknowledges the list current as of **[YYYY-MM-DD]**. Latest list available at: **[URL or “on request”]**.

---

# Annex D — Technical and Organisational Measures (TOMs)

Aligned with Art. 32 GDPR and Regkasse Cloud production posture.

## D.1 Pseudonymisation and encryption

- TLS for public HTTPS endpoints (`pos` / `admin` / `api`)  
- Secrets (JWT signing keys, vendor keys) stored in host secret store / env — not in source control  
- Payment card data masked in logs/UI where applicable (`**** **** **** 1234` pattern)  
- GDPR exports mask TSE/JWS/QR secrets  

## D.2 Access control and confidentiality

- Authentication via JWT; production Super Admin **2FA (TOTP)**  
- Role-based permissions (e.g. Mandanten-Admin vs Cashier vs Super Admin)  
- CSRF protection enabled in Production for state-changing requests  
- Least-privilege staff access; production DB dumps only under ticket + encryption  
- Audited Super Admin **impersonation** when used for support  

## D.3 Multi-tenant isolation

- Tenant-scoped data model (`tenant_id`) with EF Core global query filters  
- Cross-tenant access attempts resolve to **HTTP 404** (not 403)  
- Production POS tenant from JWT `tenant_id` (not Host slug / `X-Tenant-Id`)  

## D.4 Integrity, availability, resilience

- Health probes: live / ready (DB + fiscal posture gates)  
- Monitoring, metrics, alerting (API down, high error rate, TSE/FON failures)  
- Scheduled backups (System and/or Tenant strategies); retention per config  
- Restore exercised only in **isolated validation** environments — no casual production restore  
- Resource limits / multi-instance API recommended for rolling deploys  

## D.5 Logging and auditability

- Security-sensitive actions written to audit log (actor, role, tenant, action, correlation id, IP, UTC time)  
- Activity feed for operational events (backup, license, FON failures, etc.) without raw secrets  
- DEP exports audited (`RksvDepExportJson`)  

## D.6 Development and change control

- Additive database migrations; fiscal deploys treated as compliance-sensitive  
- Staging / canary / production promotion with ComplianceOfficer sign-off where required  
- Separation of Simulation vs Production FinanzOnline mode  

## D.7 Breach and incident handling

- Detect → contain → notify Customer (Clause 8) → remediate → postmortem for severe incidents  
- On-call / Slack (or successor) alerting for critical availability and fiscal path failures  

## D.8 Data minimisation in support

- Support accesses least data necessary  
- No logging of passwords, voucher codes in plaintext, or full payment secrets  

---

# Annex E — Retention matrix (customer-facing summary)

| Data class | Minimum retention | Erased by GDPR Delete? |
|------------|-------------------|------------------------|
| Payments, receipts, TSE signatures, DEP-relevant fiscal | **7 years** (RKSV) | **No** |
| Audit / security logs | **7 years** / legal hold | **No** |
| Products, categories, customers, company settings, non-fiscal invoices | Business need / contract | **Yes** (after approval + wait) |
| Identity / memberships | Until account end | Soft-remove / deactivate |
| GDPR export ZIP + token | Token ~7 days | Purge after expiry |
| DEP files on disk | Ops / archive policy | Not full fiscal archive substitute |
| Backups | ~30d tenant / ~90d system (typical) | Separate from Delete |

---

# Annex F — Contact points

| Role | Name | Email | Phone |
|------|------|-------|-------|
| Customer Datenschutzkontakt / DPO | | | |
| Provider Datenschutzkontakt / DPO | | | |
| Provider security / breach | | | |
| Provider support | | | |

---

## Document control

| Version | Date | Author | Notes |
|---------|------|--------|-------|
| 1.0 | 2026-08-07 | Engineering (template) | Initial AVV/DPA from Cloud Production Readiness GDPR section |

**Counsel checklist before first signature:** fill Annex C with real vendors; confirm governing law; align Delete wait / retention with live product; translate to German if Customer requires German-only AVV; attach to AGB pack for each mandant.
