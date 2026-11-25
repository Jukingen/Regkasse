# 03 — Domain, Fiscal (RKSV/TSE) and Payment

> Part 3 of the Regkasse AI briefing. Index: [`../AI_PROJECT_BRIEFING.md`](../AI_PROJECT_BRIEFING.md).
> Verified at commit `e2be4d5b` (2026-11-24). **High-risk area.** Nothing here is a legal compliance guarantee.

## 1. Core domain concepts

| Concept | What it is | Notes |
|---|---|---|
| Tenant ("Mandant") | A customer company. Root of data isolation | Table `tenants`, slug e.g. `dev`. Platform sentinel `SystemTenantIds.Platform` / slug `platform` is an inactive system row, not a real mandant |
| Cash register ("Kasse") | Physical or logical register | `CashRegister`: `RegisterNumber`, `Status` (Closed, Open, Maintenance, Disabled, **Decommissioned**), `CurrentUserId`, balances. A decommissioned register accepts no sessions or payments |
| Cart / table order | Items before payment | `/api/pos/cart/*`, table switching and recovery are high risk |
| `PaymentDetails` | The fiscal sale record | Table `payment_details`. Holds amounts, tax, method, TSE signature, `PrevSignatureValueUsed`, receipt number, `IdempotencyKey`, special-receipt metadata, storno/refund links, offline replay metadata |
| Receipt | Printable/displayable fiscal receipt | `receipts`, `receipt_items`, `receipt_tax_lines`; QR payload, signature parts |
| Invoice | Accounting-facing document created with the payment | `invoices`; snapshots of company/customer, TSE data, country/VAT at issue (`CountryCodeAtIssue`, `VatRegimeAtIssue`) |
| Receipt sequence | Sequential receipt numbers | `receipt_sequences`, `IReceiptSequenceService` / `ReceiptSequenceService` |
| Signature chain | Each receipt signs over the previous signature | `signature_chain_state` per cash register |
| Daily closing (Tagesabschluss) | End-of-day close, also monthly/yearly | `daily_closings`; endpoints `POST /api/Tagesabschluss/daily|monthly|yearly`. It is **not** the same as Schlussbeleg |
| Voucher (Gutschein) | Stored value | `Voucher` + `VoucherLedgerEntry`; code stored only as `CodeHash` and `MaskedCode` |
| Payment method definitions | Per-register methods | Seeded: `cash` (Bar), `card` (Karte), `transfer` (Überweisung), `voucher` (Gutschein) active; `check` (Scheck), `mobile` (Mobil) inactive |
| Tenant settings | `company_settings` | Country, VAT regime, billing country, working hours, vertical profile id, RKSV preferences |

Money rules: keep existing precision and rounding (`CartMoneyHelper`). Do not change rounding casually.

## 2. POS payment flow (backend)

Entry: `POST /api/pos/payment` (`PaymentController.Create`). Implemented in `PaymentService` and its partials (`PaymentService.CreatePaymentCore.cs`, `...InTransaction.cs`). The 2026-11 refactor split `PaymentService` from about 4,700 to about 1,270 lines, with behavior preserved and golden-master tests added first.

Order of operations (verified in `CreatePaymentCore`):

1. **License gate:** `EnsureLicenseAllowsPaymentAsync`.
2. **Sale gate preconditions:** `PaymentSaleGateService.ValidatePreconditionsAsync` (register readiness, TSE health policy, NTP time-sync gate, tenant limits, voucher rules).
3. **Resolve actor and customer.**
4. **Storno/refund redirect:** `PaymentStornoRefundGate.TryRedirectStornoOrRefundAsync` (storno and refund requests take their own services).
5. **Idempotent replay check** by `IdempotencyKey` / offline transaction id.
6. **Prepare fiscal sale:** `PrepareFiscalSaleAsync`. Load sale products for the tenant.
7. **Begin DB transaction.** In-transaction idempotency re-check.
8. **Register commit validation:** `ValidatePaymentRegisterForCommitAsync` (decommissioned, Startbeleg required, session state).
9. **Calculate totals** (country-aware `ITaxStrategy` for AT/DE/CH/EU default).
10. **Settlement** (`PaymentSettlementService`): payment method, voucher ledger movement, change/cash tender.
11. **Persist fiscal documents** (`PaymentDocumentPersistenceService`): allocate receipt number, build the signing payload, sign via TSE (`TseService` → provider), update `signature_chain_state`, stage `payment_details`, `invoices`, `receipts`.
12. `SaveChanges`, tenant extension hook `PaymentAfterPersist`, **commit**. On a commit failure, `PaymentCommitFailureMapper` maps to a stable error.
13. **Post-commit** (`PaymentPostCommitService`): audit, activity feed, FinanzOnline submission dispatch, notifications.

Only the method names and their order were read directly from `CreatePaymentCore`. The one-line descriptions of what each step checks (for example, which gates `PaymentSaleGateService` applies, or exactly where receipt numbering and signing happen inside `PaymentDocumentPersistenceService`) come from docs and file names. Read the code before changing any step.

Rules that must hold:

- The whole fiscal write (payment, invoice, receipt, signature chain, sequence) is atomic.
- A fiscal payment must not silently succeed without a required signature. No client flag may skip TSE signing.
- Voucher payment requires online validation. Voucher codes are never logged, cached, or stored in offline queues. Voucher balance never goes below zero.
- Reprint (`/api/pos/receipts/{id}/reprint`) creates **no** new fiscal document.
- `CreatePaymentRequest` validation must not be loosened to allow arbitrary zero-amount normal payments. Zero-value documents are RKSV special receipts only.

### Storno vs refund

- **Storno** (full cancel) is classified by `StornoReason` and linked to the original (`OriginalPaymentId`, `OriginalReceiptId`). `PosStornoController` (`/api/pos/storno`), `PaymentStornoService`. A unique index prevents duplicate storno rows for one payment (migrations in Nov 2026).
- **Refund** (partial) goes through `PaymentRefundService` (`POST /api/pos/payment/{id}/refund`).
- Do not conflate them. Voucher refund/balance-restoration is noted as incomplete.

### Card / online payment (POS hosted checkout)

Card or PayPal is captured first through a gateway. Rows live in `gateway_payment_intents` **before** the fiscal commit. The fiscal Beleg is still created by `POST /api/pos/payment`. A webhook never writes `payment_details`. Endpoints: `/api/pos/payment/initiate`, `/api/pos/payment/initiate/{id}`; webhooks: Stripe / PayPal / mock (`StripeWebhookController`, `PaymentWebhookController`). Not the same as customer **online orders**. Doc: `docs/ONLINE_PAYMENTS.md`.

## 3. TSE and signature chain

- **Providers:** `ITseProvider` implementations `FakeTseProvider` (Development), `RealTseProvider`, Fiskaly HTTP (`Tse/Fiskaly*`), `UnsupportedVendorTseProvider`. Selection through `TseProviderFactory` and configuration (`Tse:TseMode`, `Tse:Provider`).
- **Per-tenant Fiskaly credentials** (BYOK): table `tenant_fiskaly_credentials` (migration `AddTenantFiskalyCredentials`). Mandanten-Admin enters API key, secret, and SCU in FA `/settings`. Production does not use one shared host key. Cutover doc: `docs/FISKALY_PRODUCTION_CUTOVER.md`.
- **Signing pipeline:** `SignaturePipeline`, `BelegdatenPayload` / `BelegdatenPayloadBuilder`, `RksvMachineCodeBuilder` (machine code `_R1-AT1_...`), compact JWS (`JwsParser`), certificate chain and thumbprint. `FiscalSignatureRouter` routes signing by country (AT default).
- **Chain rule:** each fiscal receipt references the previous signature. Receipt numbers are sequential per register, with no duplicates and no gaps. A detected gap is logged and alerts.
- **TSE health:** `TseHealthCheckService` (configurable intervals and thresholds), cache key `tse_health_*` (30 s). Failover/auto-healing exist as Super Admin ops and default to off (`AllowAutoFailover=false`). They may change which device signs. Do not loosen without an explicit task.
- **NTP gate:** `NtpTimeSyncService`. When enabled, online fiscal payments are rejected if the last sync failed, no offset is known, or the absolute offset exceeds `MaxAllowedOffsetSeconds` (example default 5 s). `CriticalOffsetSeconds` (example 60 s) only drives warning severity.
- **RKSV overlay:** table `rksv_runtime_config` (singleton, not tenant-scoped): `RKSV.Mode` Demo/Production, `RKSV.TseMode` Simulation/Real, `RKSV.FinanzOnlineMode`, `RKSV.ShowDemoLabel` (receipt footer "DEMO / NICHT FISKAL"). FA `/admin/rksv/config`, API `GET/POST /api/admin/rksv/config`. It is **not** a hardware TSE switch. `TseMode=Real` also disables the Development TSE health bypass. Do not treat `/settings/development-mode` `bypassTseCheck` as `RKSV.Mode`.

## 4. RKSV special receipts

All are signed zero-value receipts that use the **normal** receipt number sequence and signature chain. They are `PaymentDetails` rows with `RksvSpecialReceiptKind`.

| Receipt | Rule |
|---|---|
| Nullbeleg | Zero value, no revenue effect |
| Startbeleg | Exactly one per register. A register that requires it must not allow normal sales before it exists |
| Monatsbeleg | One per register per month; must be created within 7 days after month end. December may act as Jahresbeleg (`UseDecemberMonatsbelegAsJahresbeleg`) |
| Jahresbeleg | One per register per year; due by January 31 |
| Schlussbeleg (Endbeleg) | Permanent decommissioning only. Requires a closed register with no open session. Afterwards the register is Decommissioned. Not for holidays or seasonal pauses; not a daily closing |

Endpoints: `POST /api/rksv/special-receipts/{nullbeleg|startbeleg|monatsbeleg|jahresbeleg|schlussbeleg}`. Reminders: `RksvReminderService` (admin warning 14 days before a deadline, email 7 days before). Sales are **not** hard-blocked after an early Jahresbeleg (treated as a process responsibility). Duplicate creation is blocked.

## 5. DEP export and other exports

- **DEP §7 export (Signaturjournal):** implemented. `GET /api/admin/rksv/dep-export` (params `cashRegisterId`, `fromUtc`, `toUtc` max 366 days, `includeSpecialReceipts`, `includeDailyClosings`). Output is BMF `Belege-Gruppe` JSON grouped by certificate thumbprint, compact JWS, chronological. Permissions `report.export` + `audit.view`; always audited; cross-tenant is 404. Verified with the BMF Prüftool JAR (`scripts/rksv/verify-rksv-dep-export.ps1`, JDK 17+). Key files: `AdminRksvDepExportController`, `RksvDepExportService`, `BelegdatenPayload*`, `TseCertificateChainBuilder`. Doc: `docs/DEP_EXPORT_DEVELOPMENT.md`.
- **Diagnostic fiscal export** (`/api/admin/fiscal-export`): JSON/CSV packages for diagnostics and accounting. **Not** an official DEP and not legal proof. Payloads carry a `notLegalProofNotice`. When enabled, the `X-Disclaimer-Acknowledged: true` header is required (`RequireDisclaimerAcknowledgmentFilter`).
- **RKSV QR:** `RksvReceiptQrPayloadBuilder`, `QrImageService` (tries ECC levels, versions, and finally truncates to a safe size for voucher-heavy payloads).

## 6. FinanzOnline (BMF)

- Outbox pattern: `finanz_online_outbox_messages` (tenant id stored as a string, not `ITenantEntity`), `finanz_online_submissions`, `rksv_special_receipt_finanz_online_submissions`. Workers: `FinanzOnlineOutbox`, `FinanzOnlineRetryHostedService`, monitor service.
- Retry: exponential backoff, max 5 attempts. Operator retry `POST /api/admin/finanzonline-outbox/{id}/retry` (permission `finanzonline.submit`) resets rows to `Pending`. `ProtocolSuccess` and `AwaitingProtocol` cannot be retried. Super Admin `mark-resolved` closes a row without submitting.
- Honest status: the session SOAP path is configurable; the **RKSV submission client is a guarded skeleton** and by default Fake/Disabled in examples. Do not describe it as production-complete BMF integration. Manual BMF Belegcheck remains part of operations. Only Startbeleg and Jahresbeleg have explicit outbox/tracking plumbing.
- Production should use real SOAP; test mode uses `FinanzOnline:Mode=Simulation`.

## 7. Offline systems (two separate systems, never merge)

| System | Table | Admin UI | API | Purpose |
|---|---|---|---|---|
| Legacy TSE intents | `offline_transactions` | `/admin/tse/offline-transactions` | `/api/offline-transactions/replay` | Fiscal payment intents queued while TSE or connectivity is down. Replay after recovery |
| Full order snapshots | `offline_orders` | `/rksv/offline-orders` | `/api/pos/offline-orders/*` | Complete POS order snapshots |

- POS persists a local queue (`@regkasse/offline_transactions_v1`, AsyncStorage/localStorage). The queue survives restarts.
- Cap: `tenant_limits.max_offline_transactions` (default 50 per tenant). At the cap, HTTP 409 `LIMIT_EXCEEDED`; the POS warns at 80%.
- Voucher payments must **never** be queued offline.
- Monitoring: `/api/admin/offline-monitoring/*`. Docs: `docs/OFFLINE_SYSTEM_INDEX.md`, `docs/release/OFFLINE_SYSTEMS_SEPARATION.md`. Risk: duplicate payments if an operator retries before queue reconciliation.

## 8. Vouchers (Gutschein)

- Admin APIs `/api/admin/vouchers`, POS validation `/api/pos/vouchers/validate`, redemption in the payment settlement step.
- Ledger entries (issue, redeem, cancel, refund where implemented) are the audit trail. `PaymentVoucherLedgerService` writes them.
- Rules: no plaintext code storage or logging; unique `CodeHash` per tenant; offline voucher payments blocked; admin sees only masked code plus ledger history.

## 9. Country and fiscal regimes

- Austria (`company_settings.country = 'AT'`) is the only production fiscal path. `ICountryProfileRegistry` seeds AT, DE, CH, and a registry-only `EU_DEFAULT`. Unknown or legacy values resolve to AT.
- `ITaxStrategy` and `IInvoiceStrategy` are wired into `PaymentService` / `InvoiceService`. Austrian strategies are thin adapters over existing code. Unknown pairs throw `UNKNOWN_TAX_REGIME`. Do not fork `PaymentService` or `TseService` internals.
- Feature flags (`IFeatureFlagService`, stored in `tenant_settings` as `FeatureFlags:{Name}`): `Fiscal.RksvAt` is locked on for AT. `Fiscal.KassenSicherheitDe` and `QrRechnung:BankSubmit` must **not** be enabled for real tenants until `docs/COUNTRIES.md` §16 marks them production-ready.
- Status (from `.cursor/rules/104-fiscal.mdc` and `docs/COUNTRIES.md`): DE has contract, Development soft signer, fiskaly-de HTTP and `PaymentService` wiring, but no DSFinV-K download, no DE offline, and `de_tse_signatures` instead of `payment_details.TseSignature`. CH has QR payload/PDF and tax sets, bank submit blocked. EU e-invoicing has UBL, Schematron and validation persistence, while XRechnung, ZUGFeRD, Peppol promotion and Storecove onboarding are blocked. OSS destination rates are done.
- Rules: no hardcoded country codes, VAT rates, or VAT-ID regexes outside `CountryProfile` seeds. VAT-ID patterns live in `VatIdPatterns` only. Non-AT tenants must not enable RKSV/TSE.

## 10. Non-fiscal domains (do not wire into the fiscal chain)

- **Online orders** (`online_orders`): customers order through websites, PWAs, or native apps. Status workflow `pending → accepted → preparing → ready → completed` (or `cancelled`). `PATCH /api/admin/online-orders/{id}/status`. No TSE, no RKSV. Optional POS cart bridge `POST .../accept` needs `digital.orders.approve` (Super Admin).
- **Marketplace intake:** Wolt (OAuth), Foodora (`client_credentials`), Lieferando (JET Connect). Tables `online_order_integration_providers`, `online_order_integration_tenant_credentials`, product mappings. FA `/admin/online-order-integrations`. Doc: `docs/ONLINE_ORDER_INTEGRATIONS.md`.
- **Working hours:** restrict customer online-order intake only. They never gate POS, FA, or authenticated APIs (`posOperationsAllowed` is always true). Server gate `OnlineOrderIntakeService`; code `ONLINE_ORDERS_CLOSED`.
- **Digital services:** website generator (templates Modern, Classic, Minimal) and mobile app (PWA/Native). Super Admin creates/publishes; Manager previews/requests.
- **Vertical features:** kitchen display (`KitchenHub` SignalR), tables, appointments, patient records (vet), IMEI tracking (handy shop), taxi trips, ticket events/redemption, lodging rooms/folios, retail variants/barcodes.
- **GDPR / data rights:** `CustomerDataRightsService`, `DataExportService`, `DataDeletionService`. A purge keeps fiscal amounts, TSE signatures, and the RKSV issuer snapshot, and anonymizes other PII. Expired-license customer data management exists in FA `/tenant/[id]/data-management`.

## 11. Quick "can I touch this?" table

| Area | Touch? |
|---|---|
| `PaymentService*`, `Payment*Service`, `TseService`, `SignaturePipeline`, `ReceiptSequenceService`, `signature_chain_state` | Only with explicit scope, a plan, and extended tests |
| `RksvSpecialReceiptService`, Startbeleg/Monatsbeleg/Jahresbeleg/Schlussbeleg policies | Only with explicit scope |
| FinanzOnline outbox | Only with explicit scope. Keep audit trail |
| Voucher ledger | Only with explicit scope |
| Report/receipt **layout** (PDF visuals) | Lower risk, but never change fiscal values, QR payload, signature fields, or numbering. See part 09 |
| Online orders, digital services, marketing, FA UI | Normal-risk areas, still respect tenant and permission rules |
