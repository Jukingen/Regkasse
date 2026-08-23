# May 2027 Signaturkarte mandate — design plan (P1-3)

**Date:** 2026-07-29  
**Action:** [`RKSV_ACTION_PLAN.md`](RKSV_ACTION_PLAN.md) → **P1-3** (~5–8 PD); runbook overlap: **P1-4**  
**Related:** [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md), [`RKSV_COMPLIANCE_ASSESSMENT.md`](RKSV_COMPLIANCE_ASSESSMENT.md) §5c, `TseCertificateService`

> This plan is an **operational program** design. Compliance must confirm the exact legal text / BMF announcement; keep the date configurable. Soft/Demo devices are marked “non-compliant / excluded” in reports.
>
> **Status (2026-07-29):** ✅ Implemented — `SignaturkarteProgram` config, `TseDevices` compliance columns, daily reminder hosted service, FA `/admin/tse/signaturkarte-program` + layout banner (separate from expiry).

---

## 1. Mandate (independent of expiry)

### 1.1 Two clocks

| Clock | Source | Meaning |
|-------|--------|---------|
| **Certificate `ExpiresAt`** | X.509 / `TseDevice.ExpiresAt` | Technical expiry; existing `ProcessExpiryWarningsAsync` (30-day default) |
| **Program deadline `Mai2027`** | Operational / regulatory target | Last date by which all production Signaturkarte / SCUs must be **renewed / replaced** |

They do **not** substitute for each other:

- A card with `ExpiresAt` = 2028 can still need May 2027 program “replaced” evidence.
- A card with `ExpiresAt` = 2026-12 fires both expiry and the 2027 program (technical renewal first).

### 1.2 Constant (config)

```json
"SignaturkarteProgram": {
  "Enabled": true,
  "DeadlineUtc": "2027-05-31T21:59:59Z",
  "DisplayName": "Mai 2027 Signaturkarte",
  "ReminderDaysBefore": [180, 90, 30, 7],
  "ExcludeDemoAndSoftDevices": true,
  "RequireExplicitComplianceFlag": true
}
```

- **Deadline:** Default **2027-05-31** Vienna end of day → normalize to UTC (Compliance confirms the exact day: start vs end of month).
- UI label “Mai 2027”; technical compare uses `DeadlineUtc`.

### 1.3 Definition of “renewed / compliant”

A device is **program-compliant** (recommendation — Compliance approves):

1. `TseDevice` active + Production fiscal path (`TseMode=Device`, `Mode=Real`, Provider ≠ soft/fake), **and**
2. At least one of:
   - `SignaturkarteProgramCompliantAtUtc != null` **and** `>=` program start cut (Ops marked / auto after renew sync), **or**
   - New certificate `IssuedAt >= ProgramEpochUtc` (for example card issued after 2026-06-01 — policy), **or**
   - Super Admin `MarkCompliant` with vendor ticket / audit note.

First release: **explicit flag** (`CompliantAtUtc` + actor) is safest; automatic IssuedAt rule is phase two.

New column(s) (additive migration):

- `tse_devices.signaturkarte_program_compliant_at_utc` (nullable)
- `tse_devices.signaturkarte_program_compliant_by` (nullable string)
- optional: `signaturkarte_program_note`

---

## 2. Warning system (milestone reminders)

### 2.1 Pattern

Reuse the license / grace milestone model (`GracePeriodReminderMilestones`, `LicenseReminderHostedService`):

| Component | Role |
|-----------|------|
| `SignaturkarteProgramMilestones` | Does `ReminderDaysBefore` match today? |
| `ISignaturkarteProgramReminderService` | Daily job: due milestone → Activity + email |
| Hosted service | Beside existing `LicenseReminderHostedService` or a shared scheduler |
| Dedup | `signaturkarte-program:{deadline:yyyyMMdd}:{days}:{scope}` |

### 2.2 Milestones (desired)

| Horizon | Days (to deadline) | Severity | Who |
|---------|--------------------|----------|-----|
| 6 months | 180 | Info / Warning | Super Admin (+ Ops email list) |
| 3 months | 90 | Warning | Super Admin + affected Mandanten-Admin |
| 1 month | 30 | Warning | Same + tenant-level counts |
| 1 week | 7 | Critical | Same; FA banner required |

Extra (optional): deadline day `0`, after deadline `Overdue` (Critical, daily digest).

### 2.3 Channels

1. **Activity feed** — new types: `SignaturkarteProgramReminder` (new enum values in the 170+ range), metadata: `{ deadlineUtc, daysRemaining, nonCompliantDeviceCount, tenantId? }`
2. **Email** — Super Admin distribution list + Mandanten-Admin (if the tenant has non-compliant devices); composer: license-reminder style, no secrets.
3. **FA banner** — §3.
4. **Audit** — `SIGNATURKARTE_PROGRAM_REMINDER_SENT` (tenant/platform).

### 2.4 Scope rules

| Role | What they receive |
|------|-------------------|
| **Super Admin** | Platform summary: X tenants / Y devices non-compliant |
| **Mandanten-Admin (`Manager`)** | Own-tenant devices only |
| Soft/Demo / `TseMode=Off` | Out of count (`ExcludeDemoAndSoftDevices`) |

### 2.5 Relation to certificate expiry

- `TseCertificateExpiringSoon` stays **separate** (ExpiresAt).
- Program reminder copy: *“Mai 2027 Signaturkarte-Pflicht — unabhängig vom Zertifikatsablauf.”*
- In FA, do not mix two badges: `Expires` vs `Program 2027`.

---

## 3. FA banner / widget (recommended — included in P1-3)

### 3.1 Banner (layout / RKSV hub)

Condition: `Enabled && now < Deadline+grace && NonCompliantCount > 0` (or Super Admin always sees a summary).

| Days remaining | UI |
|----------------|-----|
| \> 90 | Thin info Alert (dismissible 7 days localStorage) |
| 30–90 | Warning Alert, no dismiss (session) |
| ≤ 7 or overdue | Critical Alert, sticky; link “Compliance report” |

i18n: `signaturkarteProgram.banner.*` (de/en/tr).

### 3.2 Widget (optional, low cost)

- **`/admin/tse-management`** top card: countdown + non-compliant / total.
- Super Admin dashboard mini-stat: `Mai 2027: 12 open`.
- Link: `/admin/tse/signaturkarte-program` (report page).

### 3.3 API

```text
GET /api/admin/tse/signaturkarte-program/status
→ { deadlineUtc, daysRemaining, totals: { compliant, nonCompliant, excluded }, milestonesNext }
```

Permission: Super Admin `system.critical`; Mandanten: own-tenant summary (`settings.view` / TSE view).

---

## 4. Reporting

### 4.1 Report page / export

**Route:** `/admin/tse/signaturkarte-program` (Super Admin); Mandanten: `/settings/tse` or tse-management filter.

| Column | Description |
|--------|-------------|
| Tenant | slug / name (SA only) |
| DeviceId / Serial | TSE device |
| Provider | fiskaly / … |
| Certificate thumbprint / serial | short |
| ExpiresAt | technical expiry |
| ProgramCompliantAt | null → **Open** |
| Status | Compliant \| Open \| Excluded (Demo/Soft) \| Revoked |
| Days to deadline | number |
| Actions | Mark compliant, Open renew runbook, Schedule renewal |

### 4.2 API

```text
GET /api/admin/tse/signaturkarte-program/devices?status=Open&tenantId=
POST /api/admin/tse/signaturkarte-program/devices/{id}/mark-compliant  { note }
GET /api/admin/tse/signaturkarte-program/export.csv
```

CSV/Excel: for Ops weekly review. Audit every mark-compliant.

### 4.3 Summary metrics

- % compliant (production devices)
- Open by tenant (Top N)
- Expiring before deadline ∩ Open (double risk)
- Trend: weekly compliant delta (optional activity snapshot)

### 4.4 Merge with existing service

Add a `programCompliant` field to `TseCertificateService.GetCertificateInfoAsync` / fleet overview; a separate “renew” remains the P1-4 runbook (Fiskaly sync).

---

## 5. Operator runbook / checklist

### 5.1 Program ownership

| Role | Task |
|------|------|
| **Compliance** | Deadline date, “compliant” definition, mandate text |
| **Ops** | Vendor (Fiskaly) card-swap procedure, SCU id rotation |
| **Super Admin** | Platform report, reminder recipients |
| **Mandanten-Admin** | Renew / appointment for own devices |

### 5.2 Checklist (per tenant)

- [ ] No Soft/Demo TSE in Production (`TSE_PRODUCTION_CONFIG_LOCK`)
- [ ] Current Fiskaly/A-Trust card order / swap date for every active SCU
- [ ] New certificate synced onto the device record (`RenewCertificateAsync` / provision)
- [ ] FA **Mark compliant** + note (ticket no)
- [ ] Whether Startbeleg / FON registration is required after card swap (Compliance)
- [ ] DEP / signature-chain smoke (1 test beleg)
- [ ] Backup / TSE DR note updated

### 5.3 Timeline (recommendation)

| Period | Action |
|--------|--------|
| **≤ 2026-11** (~6 months left) | Inventory report; vendor capacity; first Super Admin mail |
| **2027-02** (~3 months) | Mandanten mail to all Open tenants; weekly SA review |
| **2027-04** (~1 month) | Critical banner; daily Open list; escalation |
| **Last week of 2027-05** | War room; Open leftovers only |
| **After deadline** | Overdue Critical; optional new fiscal-enablement policy (Compliance) |

### 5.4 Card-swap technical steps (summary — merges with P1-4)

1. New Signaturkarte / SCU in the vendor portal.
2. Config: new `SignatureCreationUnitId` / cert material (secret store).
3. FA: Renew / sync metadata → `ExpiresAt` / thumbprint current.
4. Signature smoke + optional FON update.
5. Mark program-compliant.
6. Secure destruction / vendor return of the old card.

### 5.5 Communication template (subject example)

`[Regkasse] Mai 2027 Signaturkarte — noch {N} Geräte offen (Deadline {date})`

Body: counts, report link, runbook link; **no secrets**.

---

## 6. Implementation breakdown

| Phase | Work | Role | PD |
|-------|------|------|-----|
| 1 | Config + migration (`CompliantAt`) + status DTO | Backend | 1–1.5 |
| 2 | Milestone service + hosted job + Activity/email + tests | Backend | 2–2.5 |
| 3 | Report API + CSV + mark-compliant | Backend | 1 |
| 4 | Banner + report page + i18n | Frontend | 1.5–2 |
| 5 | Runbook finalize + Compliance date approval | Ops / Compliance | 0.5–1 |

**Total:** ~5–8 PD (P1-3). P1-4 (vendor runbook detail + fleet expiry UI) is a separate ~4–6 PD; shared FA surfaces can be reused.

### Acceptance criteria

- [ ] Deadline is readable from config; milestone mail/activity is separate from expiry warnings
- [ ] Super Admin platform report; Mandanten tenant filter
- [ ] Mark compliant persists with audit
- [ ] FA banner severity follows milestones
- [ ] Demo/Soft excluded
- [ ] This document signed as Ops checklist

---

## 7. Decision summary

| Question | Decision |
|----------|----------|
| Independent of expiry? | **Yes** — separate program deadline + compliant flag |
| Reminders | 180 / 90 / 30 / 7 days — Activity + email (+ overdue) |
| FA banner? | **Yes** (in P1-3) |
| Report | Device list + CSV + % compliant |
| Runbook | §5 checklist + timeline |

---

**Last updated:** 2026-07-29 — P1-3 May 2027 Signaturkarte program design.
