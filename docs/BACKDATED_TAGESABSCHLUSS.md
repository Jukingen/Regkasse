# Backdated Tagesabschluss (nachträglicher Tagesabschluss)

> **Legal disclaimer:** This document describes the **implemented** Regkasse behaviour for late daily closings. It is **not** legal advice, not an official BMF/RKSV guarantee, and not a substitute for Steuerberater or authority guidance. Austrian fiscal law and tax-audit practice may require additional organisational measures beyond software.

---

## 1. Purpose

Operators sometimes miss a daily closing (`Tagesabschluss`). The product allows creating a closing for a **past Vienna business day** so missed sales days are not left without a Daily closing row.

The design follows the same honesty principle as late Monatsbeleg / Jahresbeleg:

| Field | Meaning |
|-------|---------|
| `ClosingDate` | Vienna business day being closed (calendar anchor, persisted as UTC midnight) |
| `CreatedAt` / TSE sign time | **Real** current UTC — **never** backdated |
| `IsBackdated` | `true` when the business day is before Vienna “today” |
| `LateCreationReason` | Operator reason (required when backdated) for Betriebsprüfung transparency |

---

## 2. Compliance posture (implementation contract)

### What the system does

1. Aggregates paid invoices for the selected Vienna calendar day only.
2. Signs and persists the closing **now** (real timestamps).
3. Marks the row `is_backdated = true` and stores `late_creation_reason`.
4. Writes audit action `TagesabschlussBackdatedCreated` with `closingDate`, `isBackdated`, and `reason`.
5. Surfaces a clear Admin UI warning before execution.

### What the system deliberately does not do

- It does **not** forge historical signature timestamps.
- It does **not** rewrite receipt/`payment_details` `CreatedAt` times for that day.
- It does **not** claim that a late closing is indistinguishable from an on-time closing.

Late creation must remain **obvious** in data and audit logs.

### Reason field (mandatory for past days)

Backdated closings require a human-readable reason (max 500 characters), e.g.:

- Forgotten closing
- Technical outage
- Staff change
- Free-text “other”

Same-day closings do **not** require a reason.

---

## 3. Admin UI (`frontend-admin`)

**Page:** `/tagesabschluss`  
**File:** `frontend-admin/src/app/(protected)/tagesabschluss/page.tsx`

- Date picker limited to Vienna calendar ≤ today.
- Past date → warning (nachträglich / prüfungstransparent) + **Grund der Verspätung** (required).
- History shows a **Nachträglich** tag when `isBackdated` is true, plus column **Grund (nachträglich)** from `lateCreationReason`.
- History column **Erstellt am** shows real `createdAt` (signing/persist time), distinct from business `closingDate`.

Permissions: `daily_closing.view` / `daily_closing.execute` (existing Tagesabschluss gates).

**POS (Manager):** Mandanten-Admin (`Manager`) receives `shift.open`, `shift.close`, and `tse.sign` so daily closing can be performed from the POS Schicht screen (same as Cashier path). Existing sessions need re-login to pick up new JWT permissions.

---

## 4. API & persistence

### Admin create

`POST /api/Tagesabschluss/daily`

```json
{
  "cashRegisterId": "<uuid>",
  "closingDate": "2026-07-10",
  "reason": "Ich habe vergessen, den Tagesabschluss zu erstellen"
}
```

- Omit / null `closingDate` → Vienna today (not backdated; `reason` ignored).
- Past `closingDate` without `reason` → `400` with English technical message requiring a reason.

### Service entry points

- `ITagesabschlussService.PerformDailyClosingAsync(..., closingDate, reason)` — FA path
- `IDailyClosingService.CreateDailyClosingAsync(..., closingDate, isBackdated, reason)` — summary/create path

### Database

Table `DailyClosings`:

- `is_backdated` (`boolean`, default `false`)
- `late_creation_reason` (`varchar(500)`, nullable)

Migrations (additive): `AddDailyClosingIsBackdated` (`is_backdated`), `AddDailyClosingLateCreationReason` (`late_creation_reason`).

---

## 5. Audit

Successful backdated create → `IAuditLogService.LogSystemOperationAsync`:

| Field | Value |
|-------|--------|
| Action | `TagesabschlussBackdatedCreated` |
| EntityType | `DailyClosing` |
| Request payload | `cashRegisterId`, `userId`, `closingDate`, `isBackdated`, `backdatedReason` / `reason`, `createdAt` (UTC), `daysLate` (Vienna calendar days) |
| EntityId | closing id |

On-time creates use `TagesabschlussCreated` (reason null).

---

## 6. Report text / PDF note

When `IsBackdated` is true, plain-text RKSV reports and daily-closing PDFs include a German operator notice:

```text
═══════════════════════════════════════════
Hinweis: Dieser Tagesabschluss wurde verspätet erstellt.
Ursprüngliches Datum: {closingDate Vienna}
Erstellt am: {createdAt Vienna}
Grund: {lateCreationReason}
═══════════════════════════════════════════
```

Source: `DailyClosingBackdatedReportNote` → `RksvReportTemplate.OperatorNotice` / `PosDailyClosingReportDto.BackdatedNotice`.

---

## 7. Operational checklist (recommended)

1. Close missed days in chronological order when practical (gaps remain detectable either way).
2. Always enter an accurate reason — it is part of the audit trail.
3. Keep TSE connectivity / demo bypass policy unchanged; late close still needs a valid TSE path where production requires it.
4. Resolve payment-without-invoice gaps for that day before closing (existing Sprint 4 block).
5. Coordinate with tax advisor if closures are very old or disputed.

---

## 8. Related docs

- `docs/RKSV_CASH_REGISTER_OPERATIONS.md` — Tagesabschluss / formal Tagesbericht distinction
- `docs/RKSV_OFFICIAL_SOURCES.md` — official source index (external)
- Late Monatsbeleg UI pattern: `frontend-admin/src/features/rksv/components/LateMonatsbelegCreationCard.tsx`

---

## 9. Uncertainty

Whether a particular tax authority accepts a given delay or reason wording is **outside** this repository. Regkasse only guarantees that late closing is recorded honestly and auditable.
