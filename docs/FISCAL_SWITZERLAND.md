> **Status:** Partial — PDF implemented, bank submit gated off. Not production-ready. This is not a SIX or MWST compliance claim.

# Fiscal Switzerland (MWST / QR-Rechnung)

**Last updated:** 2026-09-29  
**Hub:** [`COUNTRIES.md`](COUNTRIES.md) · **Rules:** [`../AGENTS.md`](../AGENTS.md)

This page describes the **shape** of the Swiss VAT and QR-bill module. It is not a legal opinion and does not claim MWST or SIX QR-Rechnung compliance.

---

## Purpose

Describe how **CH** mandants should eventually differ from the live **Austria** RKSV/TSE path: MWST calculation and labels, Swiss UID validation, and QR-Rechnung (Swiss QR-bill) **payload** generation for IBAN accounts in Switzerland or Liechtenstein.

Bank submission is config-only: `QrRechnung:BankSubmit:Enabled` defaults to **false**. `true` fails startup in Production and Staging. There is no bank HTTP client.

---

## Status

**NOT production-ready. Domain wired (Paket 30-c); TSE/RKSV paths remain AT-only.**

| Item | State |
|------|--------|
| `CountryProfile` CH seed | Shipped (CHE-… UID, `MWST_CH`, `QR_RECHNUNG`) |
| `SwitzerlandTaxStrategy.CalculateTax` | Rates from `ICountryTaxTypeRegistry` CH seed (no literals in the strategy): `STANDARD` **8.1**, `REDUCED_1` **2.6**, `LODGING` **3.8** (effective 2024-01-01). `CH_KLEINUNTERNEHMER` / `TaxExempt` → 0%. Reverse charge and OSS throw. No VIES |
| `SwitzerlandInvoiceStrategy.BuildInvoiceDocumentAsync` | MWSTG document plus QR-Rechnung payload from `IQrRechnungBuilder` (`QrRechnungBuilder` only) |
| `SwitzerlandTaxStrategy.ProjectFiscalTaxSets` | **Implemented.** `ChTaxSetMapper` reads `ICountryTaxTypeRegistry.Get("CH")`. Empty catalog throws. No AT rate fallback. Lodging lands on `Besonders` |
| `SwitzerlandInvoiceStrategy.AllocateReceiptNumberAsync` | **Implemented.** `IChReceiptSequenceService` on `ch_receipt_sequences`. Format `CH-{slug}-{register}-{seq}`. Missing service → `InvalidOperationException` (no AT sequence) |
| QR-Rechnung payload (SIX) | IG 2.3 SPC text via `QrRechnungBuilder`: IBAN mod-97, address type S, QRR / SCOR / NON. No bank API |
| QR-Rechnung PDF / QR image | `BuildPdfAsync` renders Empfangsschein + Zahlteil (QuestPDF + QRCoder, IG 2.3 / SPC `0200`). No file is written. No bank HTTP |
| QR-Rechnung audit | `QrRechnungPayloadBuilt` (111) and `QrRechnungPdfGenerated` (112). `newValues` holds `payloadHash`, `invoiceId`, `tenantId`, and a relative `pdfPathRelative` when the caller supplied one. The SPC text and the IBAN are not logged. Activity feed uses the same names (253, 254) |
| `qr_rechnung_documents` | Not added. The audit hash is the record. A table would copy that hash or store the IBAN. PDF bytes stay in memory. Bank submit stays off |
| Wiring into `InvoiceService` / `PaymentService` | Tax/invoice domain wired (Paket 30-c); TSE/RKSV paths remain AT-only |
| **Paket 21 — CH QR bank submit** | **Partial — PDF implemented, bank submit gated off** |

Feature-flag gates: `Fiscal.MwstCh` (country-profile default **on** for CH), `EInvoicing.QrRechnung` (on when the profile lists `QR_RECHNUNG`). `Fiscal.RksvAt` stays **off** for CH tenants.

Simulated or fake CH fiscal modes are rejected at host startup (`CountryFiscalLockEvaluator`).

Super Admin may **create** a CH tenant. Production POS sales stay off until one canary is set.

**Canary (Paket 81):** `Mwst:CanaryTenantId` empty denies every tenant. Set `Mwst__CanaryTenantId` to one CH mandant. `KassenSicherheit:Provider` stays `not-configured` (unused for CH). `Mwst:UseTestEndpoint` stays false. Rollback: FA `/admin/mwst` sets `Fiscal.MwstCh=false` for that tenant (`AuditEventType.ChMwstCanaryRolledBack`). A built QR writes `ChMwstQrBuilt` and an information log `CH_MWST`. Country-profile default for `Fiscal.MwstCh` stays on for `MWST_CH`; the canary id is the live lock.

---

## Planned Architecture

- Resolve CH from `CompanySettings.Country` and a CountryProfile. Do not enable RKSV or Austrian TSE for CH.
- MWST label and rate selection (standard, reduced, lodging) come from `CountryTaxType` seeds, not from AT `TaxTypes`.
- VAT-ID format and optional MWST suffix live in CountryProfile seeds (`IVatIdValidator`).
- QR-Rechnung: `SwitzerlandInvoiceStrategy` calls `IQrRechnungBuilder.BuildPayloadAsync` and passes `payment.Id` as `invoiceId` plus `company.TenantId`. `BuildPdfAsync` renders the bill in memory. `QrRechnung:BankSubmit:Enabled` stays **false**; do not call a bank.
- Audit: `IAuditLogService.LogSystemOperationAsync` writes `QrRechnungPayloadBuilt` and `QrRechnungPdfGenerated` with `correlation_id` and `tenant_id`. `newValues.payloadHash` is SHA-256 of the SPC text. Do not log the text or the IBAN. There is no `qr_rechnung_documents` table.
- Switzerland is not the EN 16931 EU default; see [`EINVOICING_EU.md`](EINVOICING_EU.md) only for contrast.

---

## Known gaps

The print gaps below are still open. `ChQrKnownGapsTests` reads `backend/Services/Countries/QrRechnung/ChQrKnownGaps.json` and fails when production code grows an implementation while a gap is marked `present: false`. Do not remove a gap without updating both the fixture and this list. A mandant can record which of those ids it has acknowledged (`Fiscal.ChQrKnownGapsAccepted`). That record does not close a gap, does not change the PDF, and does not enable bank submission. When the tenant country is CH and an open gap (`present: false`) is not in `acceptedGaps`, FA shows an informational banner on the invoice detail, the invoice preview, and the credit-note create dialog (`ChQrRechnungGapWarningBanner`). The banner does not block create, preview, download, or print. This is not a SIX IG 2.3 sign-off.

| Id | Still missing |
|----|----------------|
| `official-swiss-cross` | Official 7 mm Swiss cross with a white border. `QrRechnungPdf.SwissCrossMatrix` paints modules only |
| `font-embedding-liberation-arial` | Embedded Liberation Sans or Arial. The PDF uses the default QuestPDF font |
| `pain001` | pain.001 credit-transfer file |
| `bank-scan` | A bank scan of the printed bill |
| `perforation-line` | Perforation mark between receipt and payment part |

## Remaining gaps

See [`COUNTRIES.md`](COUNTRIES.md) §16. This stub owns **Paket 21** (bank submission). The invoice QR payload is wired.

---

## Open Questions

Answered for v1 in [`FISCAL_SWITZERLAND_QR_PLAN.md`](FISCAL_SWITZERLAND_QR_PLAN.md): spec **IG 2.3**; default reference **SCOR**; PDF via **QuestPDF + QRCoder**; no bank HTTP API.

Still open:

- Is the accommodation special rate in product scope? (CountryTaxType lodging 3.8% is seeded; POS catalog mapping is a later product choice.)
- Are language-specific UID suffixes in scope, or only the MWST suffix?

---

## Related Docs

- [`COUNTRIES.md`](COUNTRIES.md) — multi-country hub
- [`FISCAL_SWITZERLAND_QR_PLAN.md`](FISCAL_SWITZERLAND_QR_PLAN.md) — Paket 21. Option D (print/PDF) remains as of 2026-09-30. Payload and PDF exist. Bank submit stays off. Not Peppol.
- [`COUNTRY_LAYER_CUTOVER.md`](COUNTRY_LAYER_CUTOVER.md) — production country-layer apply order
- [`../AGENTS.md`](../AGENTS.md) — Country & Fiscal Regimes
- [`EINVOICING_EU.md`](EINVOICING_EU.md) — EU e-invoicing stub (not CH QR-Rechnung)
- `RKSV_*.md` — **Austria only**
