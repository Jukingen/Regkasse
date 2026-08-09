# Service Level Agreement (SLA)  
## Regkasse Cloud (SaaS) — Customer document

**Version:** 1.0 · **Effective:** [YYYY-MM-DD]  
**Provider:** [Regkasse operator legal name] (“**we**” / “**Regkasse**”)  
**Customer:** Subscribing Mandant under the Main Agreement / AGB (“**you**”)

> This SLA is an annex to your SaaS subscription (AGB). It is written for customers — not an internal ops runbook.  
> **Print tip:** Export to PDF (A4). Target length: **1–2 pages**. Replace `[brackets]` before sending.  
> Source of commercial numbers: [`CLOUD_PRODUCTION_READINESS.md`](CLOUD_PRODUCTION_READINESS.md) §3.

---

### 1. What this SLA covers

We provide the hosted Regkasse Cloud services used in production:

| Service | Typical URL |
|---------|-------------|
| Cash register (POS) | `https://pos.regkasse.at` |
| Admin panel (FA) | `https://admin.regkasse.at` |
| API | `https://api.regkasse.at` |

Optional extras (tenant websites, online-order intake) follow the same support channels; digital build requests may have separate delivery targets (see §5).

---

### 2. Uptime commitment

| Commitment | Detail |
|------------|--------|
| **Monthly availability** | **99.5%** for POS + Admin + API combined |
| **How we measure** | Successful health checks of the public production endpoints (excluding planned maintenance) |
| **Allowed downtime** | About **3.6 hours** per calendar month outside maintenance windows |
| **Planned maintenance** | At most **4 hours** per month; we aim to notify you **≥ 48 hours** in advance (email and/or status page) |

Availability for a month =  
`(total minutes − unplanned downtime minutes) ÷ total minutes × 100`,  
excluding planned maintenance announced as above.

---

### 3. Response times

We acknowledge support tickets and incidents as follows (clock starts when your request reaches an official support channel in §6):

| Priority | Examples | Target acknowledgement |
|----------|----------|------------------------|
| **Critical** | POS cannot take payments; platform-wide outage; security incident affecting your tenant | **Within 1 hour** |
| **Normal** | Questions, configuration help, non-blocking bugs, report/export issues | **Within 4 hours** during support hours |

**Support hours (Normal):** Monday–Friday **09:00–17:00** Europe/Vienna (public holidays in Austria excluded).  
**Critical:** We staff an on-call path for payment-blocking outages; acknowledgement target remains **1 hour**.

**Restore target (Critical):** We aim to restore service or provide a workable workaround on a **best-effort basis within 4 hours**. This restore target is aspirational and is **not** a hard money-back guarantee beyond the credits in §5.

| Other targets | Timing |
|---------------|--------|
| DEP / data export stuck (automated path failed) | Best effort **1 business day** |
| Digital service requests (website/app), if contracted | **1–2 business days** for first response / triage |
| Security incident notice to you | Without undue delay (we assist your GDPR/RKSV duties as per AVV) |

---

### 4. Exclusions (not counted against the SLA)

The following do **not** reduce our 99.5% uptime score and are outside Critical/Normal response commitments as platform faults:

1. **Your network or devices** — internet, Wi-Fi, PCs/tablets, local printers, card terminals, cash drawers  
2. **TSE / signature vendor outages** — third-party TSE (e.g. Fiskaly) or crypto/hardware issues outside our control  
3. **FinanzOnline / BMF outages** or delayed authority systems  
4. **Misconfiguration by your users** — wrong FON credentials, decommissioned register misuse, Simulation/test TSE left on by request  
5. **Force majeure** — natural disaster, war, large-scale DDoS beyond our capacity plan, widespread ISP failure  
6. **Scheduled maintenance** announced per §2  
7. **Data you deleted** after an approved GDPR Delete (RKSV fiscal rows remain per law; we cannot restore erased business data except from backups still within retention)  
8. **Beta / non-production** environments

**Fiscal note:** You remain the cash-register **operator** (Stammdaten, Tagesabschluss, Sonderbelege deadlines, FON credentials, DEP for Betriebsprüfung). We provide the platform, tenant isolation, TSE *integration* health, DEP generation, and hosted backups per policy.

---

### 5. Compensation (service credits)

If **monthly availability** under §2 falls **below 99.5%** for reasons that are **not** excluded in §4, you may request a **service credit** on the following SaaS subscription fee for that month:

| Monthly availability | Credit on that month’s net SaaS fee |
|----------------------|-------------------------------------|
| 99.0% – &lt; 99.5% | **5%** |
| 95.0% – &lt; 99.0% | **10%** |
| &lt; 95.0% | **25%** |

- Credits are **capped at 25%** of that month’s net SaaS fee.  
- Credits are your **sole and exclusive remedy** for downtime under this SLA (unless mandatory law says otherwise).  
- Credits are applied to a future invoice; they are **not** cash refunds.  
- Request credits in writing within **30 days** after the month ends, with approximate downtime windows.  
- License setup fees, hardware, and third-party TSE vendor charges are excluded from credit calculation.

---

### 6. Support channels

| Channel | Use for | How |
|---------|---------|-----|
| **Support email** | All tickets (Critical & Normal) | **[support@your-domain.at]** |
| **Admin panel** | In-app activity / notifications | `https://admin.regkasse.at` (bell / activity feed) |
| **Status page** | Outage visibility | **[status URL or “announced by email”]** |
| **Emergency (Critical only)** | POS cannot take payments | **[phone / on-call number — optional]** |

Please include: tenant/company name, register number (if relevant), time (UTC or Vienna), short description, and screenshots. Do **not** send passwords, full card data, or voucher codes in plaintext.

---

### 7. Changes

We may update this SLA with reasonable notice (email or Admin notice). Continued use after the effective date constitutes acceptance, unless your Main Agreement requires written consent.

---

### Acceptance

| | Customer | Provider |
|--|----------|----------|
| Name / title | | |
| Date | | |
| Signature | | |

---

*Internal reference only (not part of the customer PDF body): measurement endpoints and ops alerts are documented in `MONITORING.md` / `ALERTING.md` and `CLOUD_PRODUCTION_READINESS.md` §3.*
