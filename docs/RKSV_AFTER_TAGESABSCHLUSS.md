# RKSV — Behavior after Tagesabschluss (verified)

> **Legal notice:** This document describes **implemented** product behavior verified against the codebase. It is not legal advice and does not certify RKSV/ABG compliance.

> **Related:** [`docs/RKSV_CASH_REGISTER_OPERATIONS.md`](RKSV_CASH_REGISTER_OPERATIONS.md) §11, [`docs/BACKDATED_TAGESABSCHLUSS.md`](BACKDATED_TAGESABSCHLUSS.md)

**Verified:** 2026-07-17 (code inspection + existing unit/integration tests)

---

## RKSV product rules (target)

| # | Rule | Enforced? |
|---|------|-----------|
| 1 | After Tagesabschluss, no new sales/payments on that register while it remains closed | **Yes** (via register status) |
| 2 | A new shift / open register is required before new payments | **Yes** |
| 3 | The same Vienna business day cannot have two completed Daily closings | **Yes** (service + DB unique index) |

---

## Important: there is no `IsClosed` flag

The sample pattern `register.IsClosed` does **not** exist. The canonical field is:

```csharp
// backend/Models/CashRegister.cs
public RegisterStatus Status { get; set; }
// Closed = 1, Open = 2, Maintenance = 3, Disabled = 4, Decommissioned = 5
```

Tagesabschluss (POS path) sets the register to **Closed** after a successful fiscal daily closing.

---

## End-to-end flow (POS Tagesabschluss)

```text
Active shift + Open register
        │
        ▼
PosDailyClosingService.PerformDailyClosingAsync
        │
        ├─ CanPerformClosingAsync?  ──no──► AlreadyClosed / fiscal block
        │
        ├─ TagesabschlussService.PerformDailyClosingAsync
        │     └─ DailyClosing (ClosingType=Daily) + TSE signature
        │
        ├─ Complete CashierShift (EndedAt, totals, DailyClosingId)
        │
        └─ CashRegisterShiftService.TryCloseCashRegisterAsync
              └─ Status = RegisterStatus.Closed, CurrentUserId = null
```

**Key files**

| Layer | File |
|-------|------|
| POS fiscal + shift close | `backend/Services/PosDailyClosingService.cs` |
| FA / shared fiscal closing | `backend/Services/TagesabschlussService.cs` |
| Register open/close | `backend/Services/CashRegisterShiftService.cs` |
| Payment gate | `backend/Services/CashRegisterResolutionService.cs` → used by `PaymentService.CreatePaymentAsync` |
| HTTP | `POST /api/pos/payment` (`PaymentController`), `POST /api/Tagesabschluss/daily`, POS shift daily-closing |

---

## Rule 1 — No new transactions while closed

### Backend (authoritative)

`PaymentService.CreatePaymentAsync` does **not** check a boolean `IsClosed`. It calls:

```csharp
var registerValidation = await _cashRegisterResolution.ValidatePaymentRegisterAsync(...);
```

`EvaluatePaymentRegisterCore` rejects when `Status != Open`:

| Condition | Diagnostic code | API message (English) |
|-----------|-----------------|------------------------|
| `Status == Decommissioned` | `CASH_REGISTER_DECOMMISSIONED` (mapped) | Permanently decommissioned… |
| `Status != Open` (incl. Closed) | `CASH_REGISTER_CLOSED` | Cash register is closed or not usable for payment. |

`PaymentController` maps non-success results with a diagnostic code to **HTTP 400**.

The same check runs again under a register row lock at commit (`ValidatePaymentRegisterForCommitAsync`).

**Tests:** `CreatePaymentAsync_WhenRegisterAlreadyClosed_RejectsWithClosedDiagnostic`, `PaymentRegisterCommitGateTests`, `CashRegisterResolutionServiceTests`.

### POS (UX)

- **No** `register.isClosed` / `Alert.alert('Kasse geschlossen', 'Bitte öffnen Sie einen neuen Tag…')` in `PaymentModal.tsx`.
- Path: `frontend/components/PaymentModal.tsx` → `usePosCashRegisterAssignment` → `computeRegisterGateBlockingPayment` + `buildPosRegisterGateContext` (ensure-ready `nextAction` / `CASH_REGISTER_CLOSED`).
- When closed: banner title **"Kasse ist geschlossen"**, detail/footer via `posRegisterGateCopy.ts` (Schicht öffnen); **Zahlen** disabled (`isRegisterGateBlockingPayment`).
- Submit guard without valid register uses `Alert.alert('Debug Error', … registerGateAlertMessage(…))` — not a dedicated closed-day alert.
- Exact string *"Bitte öffnen Sie einen neuen Tag, um fortzufahren."* is **not** in the POS UI.

### FA

- Status badge: **"Geschlossen"** / closed sub-status **"Tagesabschluss geschlossen"** when inferred after daily closing (`cashRegisters.statusBadges.closed.dailyClosing`).
- Guidance: `cashRegisters.shiftGuidance` explains that after Z-Bericht the register is normally closed; new sales require opening again.
- FA does not take POS payments; reports/history show closing rows via Tagesabschluss + daily-closing reports.

---

## Rule 2 — New day / shift must be opened

| Step | Behavior |
|------|----------|
| After successful POS Tagesabschluss | Register `Status = Closed`; active shift completed |
| New payments | Rejected until register is **Open** again |
| Re-open | `TryOpenCashRegisterAsync` requires `Status == Closed`, then sets `Open` + shift owner |

Opening does **not** require a new Startbeleg (Startbeleg is one-time commissioning). FA copy: `cashRegisters.shiftGuidance.rules.reopenStartbeleg`.

**Nuance (product / compliance):** Code does **not** forbid reopening the same Vienna calendar day after a completed Daily closing. If an operator opens a new shift later the same day, payments are allowed again (`Status == Open`). A **second** Daily closing for that same business day remains blocked (Rule 3). Late same-day sales after Z-Bericht are therefore an operational risk: they are not covered by the already sealed `DailyClosing` for that day. Preferred ops practice: reopen only for the **next** business day.

---

## Rule 3 — Same day cannot have two closings

### Application layer

`TagesabschlussService.CanPerformClosingAsync(cashRegisterId, closingDate)`:

1. Resolves Vienna business day (today, or past for nachträglich).
2. Returns `false` if `HasDailyClosingForBusinessDayAsync` finds any Daily row for that register + `ClosingDate` anchor.
3. Also returns `false` if register missing/decommissioned, or payments-without-invoice exist in the period.

POS: `PosDailyClosingService` exposes block reason `already_closed_today` (`PosDailyClosingBlockReasons.AlreadyClosedToday`).

FA: `tagesabschluss.warnings.warningDailyAlreadyClosed` / can-close API.

### Database layer

Unique filtered index (Completed only):

```text
IX_DailyClosings_CashRegisterId_ClosingDate_ClosingType
UNIQUE (CashRegisterId, ClosingDate, ClosingType) WHERE Status = 'Completed'
```

Migration: `20260711211840_AddDailyClosingPeriodUniqueIndex`.

**Tests:** `PerformDailyClosing_AlreadyClosedToday_Throws`, `PerformDailyClosingAsync_WhenAlreadyClosedToday_ReturnsBlocked`, `GetStatus_AlreadyClosedToday_ReturnsBlockReason`.

**Exception path:** Backdated closing for a **different** past Vienna day that has no Daily row is allowed (see `BACKDATED_TAGESABSCHLUSS.md`). Closing **today** twice is not.

---

## Quick reference — messages

| Surface | Closed / already-closed messaging |
|---------|-----------------------------------|
| API payment | `CASH_REGISTER_CLOSED` + English message; HTTP **400** |
| POS gate | German: Kasse geschlossen → Schicht öffnen |
| POS daily closing status | `Tagesabschluss für heute bereits durchgeführt.` |
| FA badge | `Geschlossen` / `Tagesabschluss geschlossen` |
| FA Tagesabschluss | Already-closed warning for selected date |

---

## Verification checklist

- [x] Closed register cannot pay (`ValidatePaymentRegister*` + PaymentService)
- [x] Tagesabschluss closes register (POS path)
- [x] Duplicate Daily closing same Vienna day blocked (service + unique index)
- [x] POS/FA show closed / already-closed UX
- [ ] **Not enforced:** hard block on reopen + pay after Daily closing on the **same** Vienna day (ops discipline only)

---

## Correction vs outdated snippet

```csharp
// ❌ Does not exist
if (register.IsClosed) { ... }

// ✅ Actual gate (simplified)
if (register.Status != RegisterStatus.Open)
{
    return Failure(CashRegisterResolutionCodes.Closed,
        "Cash register is closed or not usable for payment.");
}
```
