# Fiscal / TSE guardrails

Short rules for RKSV closing and special-receipt paths. Prefer this over inventing a parallel `Services/Fiscal/` layout — production code lives under `Services/` + `Tse/` + `FinanzOnlineIntegration/`.

## Atomicity

| Flow | Rule |
|------|------|
| Sonderbelege (`RksvSpecialReceiptService`) | Outer DB TX; invoice/TSE sign enlisted via `CreateInvoiceSignatureAsync(..., dbTransaction)`. |
| Daily closing (`TagesabschlussService`, `DailyClosingService`) | Outer `fiscalTx`; `CreateDailyClosingSignatureAsync(..., fiscalTx)` then persist `DailyClosing`; commit only after successful save. Duplicate unique-index → rollback (no orphan chain advance). |
| Monthly / yearly closing (`TagesabschlussService`, `MonatsbelegClosingService`, `JahresbelegClosingService`) | Same pattern with `CreateMonthlyClosingSignatureAsync` / `CreateYearlyClosingSignatureAsync`. |
| Invoice payments | Same enlistment pattern on the payment→receipt path. |

Do **not** commit TSE chain (`tse_signature_chains` / `tse_signatures`) before the business row that owns the signature. Callers that omit `dbTransaction` still get an internal TX inside `TseService` (standalone sign only).

## Signature chain

- Chain lock: `EnsureChainRowAndLockAsync` (`FOR UPDATE`) before each sign.
- Previous compact JWS feeds the next payload; turnover counter updates with the same TX.
- Closing `BelegDatumUhrzeit`: Vienna business-day **23:59:59** converted to real UTC — never `DateTime.SpecifyKind(localWall, Utc)`.

## Parallel closing stacks (intentional)

| Stack | Surface | Notes |
|-------|---------|--------|
| `TagesabschlussService` | Legacy FA/POS daily/monthly/yearly | Writes `DailyClosing` rows. |
| `DailyClosingService` | Canonical daily closing API | Prefer for new daily work. |
| `MonatsbelegClosingService` / `JahresbelegClosingService` | Phase-2 Monats-/Jahresbeleg entities | Prefer for new monthly/yearly work. |
| `RksvSpecialReceiptService` | Null-/Start-/Monats-/Jahres-/Schlussbeleg as special receipts | Separate from closing aggregates; FON tracking for Start-/Jahresbeleg. |

Do not merge these stacks casually; keep payload math and unique-period indexes intact.

## FinanzOnline

- Special receipts: outbox + `RksvSpecialReceiptFinanzOnlineSubmission` (`CreateInitialPendingRow` → `Pending`).
- Worker: `FinanzOnlineOutbox` — exponential backoff; default **`MaxAttempts = 5`** (AGENTS.md).
- Closing-row `SubmitDailyClosingAsync` may still be simulate/status-stamp only; real SOAP is on the special-receipt / outbox path. Do not assume closing aggregates enqueue FON unless the code path shows it.

## Tests

```bash
dotnet test --filter "FullyQualifiedName~Rksv"
```

Also useful: `Tagesabschluss*`, `DailyClosing*`, `FinanzOnlineOutbox*`, `Monatsbeleg*`, `Jahresbeleg*`.
