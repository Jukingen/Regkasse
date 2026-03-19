# Fiscal / payment schema migration timeline (go-live reference)

Focus: `receipt_sequences`, `signature_chain_state`, `offline_transactions`, `payment_details`, `receipts`, `invoices`. Ordered by migration timestamp.

| Migration | Tables / risk |
|-----------|----------------|
| **20260318135708_AddReceiptSequencesTable** | Creates `receipt_sequences` with `kassen_id` (string) + unique `(kassen_id, sequence_date)`. |
| **20260317223230_AddUniqueIndexPaymentDetailsReceiptNumber** | Unique on `payment_details."ReceiptNumber"` (filtered). **Data risk:** duplicate BelegNr before migrate fails deploy. |
| **20260318171559_PaymentCashRegisterIdFk** | **High impact:** `payment_details`: maps `KassenId` → `cash_register_id`, **DELETE** rows that cannot map; drops `KassenId`. `receipt_sequences`: maps `kassen_id` → `cash_register_id`, **DELETE** nulls + **dedup** duplicate `(cash_register_id, sequence_date)` keeping highest `next_sequence`. `signature_chain_state`: creates table if missing (ordering drift), maps `register_id` → `cash_register_id`, **DELETE** unmappable + **dedup** per register. **Invoices:** conditional SQL for `CashRegisterId` / `source_payment_id` drift; **NOTICE** if unresolved empty register. |
| **20260318190000_AddSignatureChainStateTable** | Idempotent create if table missing (baseline `register_id` shape). Superseded in practice by 71559 reshape. |
| **20260318180649_OfflineTransactionEntity** | `offline_transactions` + `payment_details.offline_transaction_id` FK (SET NULL). Mixed PascalCase columns on `offline_transactions` (`CashRegisterId`, `PayloadJson`, …). |
| **20260318175000_PaymentDetailsOriginalReceiptId** | `original_receipt_id` → `receipts`. |
| **20260318184135_OfflineTransactionReplayHardening** | Offline replay columns + backfill `OfflineCreatedAtUtc` from `created_at`. |
| **20260318180000_EnsureUniqueIndexPaymentDetailsIdempotencyKey** | Defensive column + **unique partial** index on `idempotency_key`. **Data risk:** duplicate non-null keys block index create until cleaned. |
| **20260318195117_OfflineTransactionFinalHardening** | Offline: `payload_hash`, device/sequence, clock drift; **unique** `(CashRegisterId, device_id, client_sequence_number)` and `(CashRegisterId, payload_hash)`; **backfill** `payload_hash` via `pgcrypto` digest of `"PayloadJson"`. |

## Classified risks

- **Destructive cleanup:** 71559 deletes payments without resolvable register; deletes orphan sequence/chain rows; dedups sequences and chain state (losing duplicate rows).
- **Rename / drift:** `KassenId` / `kassen_id` / `register_id` removed in favour of UUID `cash_register_id`. Any manual SQL still using old names will break.
- **Dedup:** Receipt sequence and signature chain duplicates removed by migration — fiscal meaning of dropped rows should be understood in audits.
- **Invoice register gap:** 71559 does not hard-fail on unresolved `CashRegisterId`; zero-UUID or wrong register possible until manual fix.

See `scripts/sql/fiscal_go_live_validation.sql` for post-migrate checks. CI/release gate: `scripts/run_fiscal_go_live_validation.sh` and `.github/workflows/fiscal-validation.yml`; details in `FISCAL_VALIDATION_CI.md`.
