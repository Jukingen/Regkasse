# Switzerland QR-Rechnung implementation plan (Paket 21)

**Last updated:** 2026-09-30  
**Status:** QR-Rechnung payload + PDF implemented (`QrRechnungBuilder.BuildPdfAsync` via `QrRechnungPdf`). Bank submit gated behind `QrRechnung:BankSubmit:Enabled` (default false; startup rejects true in Production). This is not a legal opinion and does not claim SIX, SPS, or MWST compliance.  
**Hub:** [`FISCAL_SWITZERLAND.md`](FISCAL_SWITZERLAND.md) · [`COUNTRIES.md`](COUNTRIES.md) §16

**There is no bank HTTP API in this package.** A QR-bill is a **payment instrument** the payer’s CH/LI bank scans. “Bank submission” in the remaining-gaps table means **bank-compatible QR-bill output**, not a credit-transfer origination API. eBill is a separate network and is **out of v1**.

Do not enable Austrian RKSV/TSE for CH tenants.

## Known gaps

- The painted Swiss cross is not the official 7 mm cross with a white border.
- The PDF uses the default QuestPDF font, not embedded Liberation Sans or Arial.
- No pain.001 credit-transfer file.
- No bank scan of the printed bill.

---

## 1. SIX specification version

**Implement Swiss Implementation Guidelines for the QR-bill, Version 2.3.**

| Item | Value |
|------|--------|
| Document | [IG QR-bill v2.3 (EN PDF)](https://www.six-group.com/dam/download/banking-services/standardization/qr-bill/ig-qr-bill-v2.3-en.pdf) |
| Publisher | SIX Interbank Clearing (Swiss Payment Standards) |
| Replaces | v2.2 (2021-02-22) **entirely** |
| In force | Layout/online chapter **2024-01-01**; full technical v2.3 **2025-11-21** (operationally from **2025-11-22**) |
| Pin in config | `QrRechnung:SpecVersion=2.3` |

v2.3 deltas that bind this plan:

- **Structured address only** (address type **S**). Combined address type **K** is gone.
- **Extended character set** (additional umlauts / diacritics).
- Swiss QR Code: Swiss cross in a black square with a white border (unchanged identifier).
- Payment part + **receipt** (Empfangsschein) layout rules remain.

**v2.4** (SIX: SIC release **2026-11-13**, v2.3 remains valid until Nov 2027): no technical change for **CHF**. For **EUR**, only IBAN+SCOR or IBAN+unstructured message. Design the currency/reference matrix so a v2.4 pin is a config bump, not a rewrite.

Hub page: [SIX QR-bill standards](https://www.six-group.com/en/products-services/banking-services/payment-standardization/standards/qr-bill.html).

---

## 2. PDF rendering approach

Today `BuildPdfAsync` renders the receipt and payment part (`QrRechnungPdf`). It is not a measured SIX print overlay.

**Libraries (already in `backend/KasseAPI_Final.csproj`):**

| Library | Version in repo | Role |
|---------|-----------------|------|
| **QuestPDF** | 2026.7.1 | Payment-part + receipt PDF. Already used for billing / fiscal PDFs (`LicenseType.Community`). |
| **QRCoder** | 1.8.0 | Swiss QR matrix. Overlay the **Swiss cross** after encode (QRCoder does not draw the SPS cross by itself). |

Do **not** add iText or a second PDF stack. Do **not** rasterize in POS React Native first — generate on the **API** so FA, Sites, and POS share one byte stream.

### Layout (IG §3)

- ISO **A6** landscape payment part; **receipt** on the left, payment part on the right; **perforation** between them when printed on A4 (invoice above, QR-bill at the bottom).
- Swiss QR: 46 × 46 mm coding region including quiet zone; cross centered.
- Fonts: SPS-permitted sans (Liberation Sans / Arial). Embed a licensed/open face; do not rely on the host having Arial.
- Amount: 2 decimal places; currency **CHF** or **EUR** next to the amount.
- Online display: follow IG **§3.8** (no fake perforation on screen; still show receipt + payment sections).

### Receipt section (Empfangsschein)

Print account / creditor, payable-by (debtor if present), amount, currency, and acceptance point lines per IG. This is **not** an RKSV fiscal receipt. Do not put AT TSE QR on a CH QR-bill.

### Invoice vs QR-bill

CH `InvoiceDocumentDto` (shape) stays the commercial invoice. The QR-bill PDF is an **attachment / second page**, gated by `EInvoicing.QrRechnung`. POS kitchen tickets never include a QR-bill.

---

## 3. IBAN and reference validation

Today `QrRechnungBuilder.NormalizeSwissOrLiechtensteinIban` only strips whitespace, uppercases, and requires prefix **CH** or **LI**. **No checksum, no length, no QR-IBAN vs IBAN, no reference scheme.** That is not enough for bank-compatible output.

### IBAN

| Rule | Detail |
|------|--------|
| Country | **CH** or **LI** only (already). |
| Length | **21** characters after normalize (ISO 13616). |
| Checksum | **ISO 13616 mod-97** (move first 4 chars to end, A=10…Z=35, remainder 1). Reject otherwise. |
| QR-IBAN vs IBAN | QR-IBAN: IID in **30000–31999**. Ordinary IBAN: other CH/LI IIDs. |

### Reference type (default **SCOR**)

| Type | When | Reference | IBAN kind |
|------|------|-----------|-----------|
| **QRR** | Tenant has a **QR-IBAN** from the house bank | **27** digits, recursive **mod-10** (ISR-style). Empty QRR forbidden with QR-IBAN. | QR-IBAN **required** |
| **SCOR** | **Default for v1** (restaurants often have no QR-IBAN) | ISO **11649** Creditor Reference (`RF` + mod-97 + 1–21 alphanumeric), max 25 chars | Ordinary IBAN |
| **NON** | No structured ref | Empty reference; optional unstructured message | Ordinary IBAN |

v1 default: `QrRechnung:DefaultReferenceType=SCOR`. Enable QRR only when `company_settings` (or a later `qr_rechnung_settings` row) stores a QR-IBAN.

### Other payload rules (IG §4)

- Currency: `CHF` or `EUR` (profile default **CHF**). Amount empty = open amount (allowed).
- Creditor: name, street+building **or** street name + building number (type **S**), postal code, town, country.
- Debtor: optional; if present, same structured address.
- Unstructured message / billing info: SPS character set; billing info print optional (v2.3).
- Do not emit address type **K**.

Flag: `EInvoicing.QrRechnung` (CH profile default on). `QrRechnung:BuilderMode`: `not-configured` (stub) → `payload` → `pdf`. Production/Staging still reject `dryRun`.

---

## 4. Bank compatibility checklist

Use this before a pilot. A “fail” means do not send the PDF to a live customer.

| # | Check | Pass |
|---|--------|------|
| 1 | Spec pin is **IG 2.3** | `QrRechnung:SpecVersion=2.3` |
| 2 | Swiss cross visible in the QR | Visual + SIX sample decode |
| 3 | Prefix CH/LI, length 21, **mod-97** | Unit tests from SIX examples |
| 4 | QRR only with QR-IBAN; SCOR/NON never with QR-IBAN | Matrix tests |
| 5 | QRR 27-digit mod-10; SCOR `RF…` mod-97 | SIX Annex examples |
| 6 | Address type **S** only | No combined-address field |
| 7 | Amount scale 2; CHF/EUR | |
| 8 | Payment part + receipt geometry | Overlay on SIX print template |
| 9 | Scan with at least **two** CH retail banking apps (or bank test portal) + one LI if LI IBAN | Ops checklist |
| 10 | Pain.001 mapping not required in v1 | We do not originate payments |
| 11 | EUR + QRR avoided (blocked ahead of v2.4) | Config guard |
| 12 | No AT TSE / RKSV QR on the same page | |

House-bank confirmation: Mandanten-Admin stores IBAN / QR-IBAN; Super Admin does not invent a QR-IBAN.

---

## 5. Phased rollout

| Phase | Scope | Exit |
|-------|--------|------|
| **1. Payload-only** | Checksum, QRR/SCOR/NON, address type S. Implemented in `SwissQrEncoder`. | Golden-vector tests vs SIX examples. |
| **2. PDF** | QuestPDF + QRCoder + Swiss cross. A6 payment part + receipt. FA preview download. | Overlay checklist; two-app scan on TEST IBANs. |
| **3. Pilot** | One CH canary tenant, real IBAN, **unpaid** test invoices (or 0.05 CHF). Flag on. | House bank accepts a scan; no Production CH POS sale required (MWST TSE still not started). |
| **4. Production** | Remaining CH mandants who opt in. Include QR-bill on FA invoice PDF / Sites invoice. | Runbook; rollback = `EInvoicing.QrRechnung` off or `BuilderMode=payload`. |

Rollback: keep stored payloads; stop emitting PDF. Additive table stays.

**Not in these phases:** eBill network, pain.001 origination, Swiss bank host-to-host, MWST TSE (separate from QR-bill).

---

## 6. Config, schema, audit, FA

### Config

```json
"QrRechnung": {
  "BuilderMode": "payload",
  "SpecVersion": "2.3",
  "DefaultReferenceType": "SCOR",
  "AllowEur": true
}
```

`BuilderMode=pdf` turns on `BuildPdfAsync`. `dryRun` stays illegal outside Development.

### Persistence

No `qr_rechnung_documents` table. Option D does not send the bill to a bank, and `QrRechnungPdf.Render` returns bytes without writing a file. The audit events below store a SHA-256 of the SPC text, the invoice id, the tenant id, and a relative path only when a caller passes one. Storing the payload JSON would store the IBAN. The hash in `audit_logs.new_values` is the record.

### Audit

| Name | When |
|------|------|
| `QrRechnungPayloadBuilt` | `QrRechnungBuilder.BuildPayloadAsync`. `newValues` is `payloadHash`, `invoiceId`, `tenantId`, `referenceType`. Not the SPC text. |
| `QrRechnungPdfGenerated` | `QrRechnungBuilder.BuildPdfAsync` after `QrRechnungPdf.Render`. `pdfPathRelative` is relative or null. |
| `ChMwstQrBuilt` | Existing canary event on the payment path. Separate from the two rows above. |

### FA

| Surface | Who |
|---------|-----|
| Tenant invoice / settings: IBAN, QR-IBAN, reference type | Mandanten-Admin (`settings.manage` or invoice permission already used) |
| Preview QR-bill PDF | Same |
| Super Admin CH tenant card: spec version, last generate | `system.critical` |

POS: optional “show QR-bill” after a CH invoice — **not** a fiscal TSE QR. German POS copy.

OpenAPI + Orval with the API.

---

## Related docs

- [`FISCAL_SWITZERLAND.md`](FISCAL_SWITZERLAND.md)  
- [`COUNTRY_LAYER_CUTOVER.md`](COUNTRY_LAYER_CUTOVER.md)  
- [`EINVOICING_EU_SUBMISSION_PLAN.md`](EINVOICING_EU_SUBMISSION_PLAN.md) — Peppol (Paket 22) is a separate channel. A Swiss QR-Rechnung is not a Peppol document and is not an EN 16931 submission.  
- [`ENVIRONMENT_CONFIGURATION.md`](ENVIRONMENT_CONFIGURATION.md) §4 (`QrRechnung` lock)

---

## Bank submission decision (2026-09-29)

This is not a SIX or MWST compliance claim. Paket 21 does not send a bill to a bank.

### What “bank submission” means

**Chosen: Option D.** The QR-Rechnung is a print and PDF artifact. `QrRechnungBuilder.BuildPdfAsync` calls `QrRechnungPdf.Render`. There is no bank HTTP client. `QrRechnung:BankSubmit:Enabled` defaults to false, and `CountryFiscalLockEvaluator.ReasonQrBankSubmit` rejects `true` in Production and Staging.

### State as of 2026-09-30

Option D is still the Paket 21 decision. Do not expand this package to Option B (EBICS / pain.001).

`QrRechnungPayloadBuilt` (audit 111, activity 253) and `QrRechnungPdfGenerated` (audit 112, activity 254) now record that a payload or PDF was built. They store a SHA-256 of the SPC text, the invoice id, the tenant id, and a relative path only when the caller supplied one. They do not store the IBAN and they do not send anything to a bank. `ChQrKnownGaps.json` still pins the missing print features (`official-swiss-cross`, `font-embedding-liberation-arial`, `perforation-line`, `pain001`, `bank-scan`) with `present: false`. `QrRechnung:BankSubmit:Enabled` remains false, and Production and Staging still refuse `true` at startup. A credit-transfer file or an EBICS session would be a new channel, with bank secrets, on top of a PDF this plan still does not treat as a measured SIX print. Peppol ([`EINVOICING_EU_SUBMISSION_PLAN.md`](EINVOICING_EU_SUBMISSION_PLAN.md)) is that other channel for EU e-invoices. It is not the Swiss QR-bill.

Option A (an operator downloads the PDF and uploads it in a bank portal) is an audit trail only. `GET /api/admin/tenants/{tenantId}/ch-qr-invoices/{invoiceId}/pdf` returns the existing PDF when every open gap (`present: false`) is in `Fiscal.ChQrKnownGapsAccepted` and `QrRechnung:BankSubmit:Enabled` is false. Otherwise the download is HTTP 409 `CH_QR_GAPS_NOT_ACCEPTED` with `outstandingGapIds`. `POST .../upload-confirmation` records `uploadedBy`, `uploadedAtUtc`, and `bankReference` after a `QrRechnungPdfGenerated` audit row exists. Audit `QrRechnungPdfDownloaded` is 116. Audit `QrRechnungBankUploadConfirmed` is 117. Neither call opens bank HTTP. It is not Paket 21 and it is not a SIX or MWST compliance claim.

Option B (pain.001 over EBICS) and Option C (SIX eBill) are not Paket 21. Both need per-tenant bank credentials. Those secrets do not belong in `company_settings` and do not belong in `tenant_settings` as plain text. A later package would have to name an encrypted store or an external vault before any key is accepted. This record does not pick EBICS 3.0, a key table, or an eBill network.

### Post-Paket-21 candidates

Option A download and upload-confirmation are implemented as audit rows. They do not turn `QrRechnung:BankSubmit:Enabled` on. Option B is out of scope. See [Option B decision (2026-09-30)](#option-b-decision-2026-09-30).

| Candidate | What it would be | Pre-conditions before any build |
|-----------|------------------|----------------------------------|
| **Option A — operator PDF upload** | The operator downloads the PDF and uploads it in a bank portal. Regkasse does not call the bank. | Implemented as `GET .../ch-qr-invoices/{invoiceId}/pdf` and `POST .../upload-confirmation`. Every open gap must be in `Fiscal.ChQrKnownGapsAccepted`. Audit 116 and 117. No bank credential is stored. |
| **Option B — EBICS / pain.001** | Regkasse originates a credit-transfer file and a bank client sends it. | **Out of scope** (decision below). `pain001` stays `present: false`. `QrRechnung:BankSubmit:Enabled` stays false. No EBICS library, no `bank_credentials` table, no new audit numbers. |

### Next package pre-conditions

Per-tenant acceptance is `tenant_settings` key `Fiscal.ChQrKnownGapsAccepted`. The value is JSON: `acceptedGaps`, `acceptedBy`, `acceptedAtUtc`. Every id must exist in `ChQrKnownGaps.json`. Super Admin writes it with `POST /api/admin/tenants/{tenantId}/ch-qr-gap-acceptance` and reads it, together with the catalog, from the matching GET. There is no global row.

`GET` returns every catalog id, so a new gap in the JSON shows up on that payload without a second list. Audit `ChQrKnownGapsAccepted` is 114. Activity `ChQrKnownGapsAccepted` is 260. While an open gap (`present: false`) is missing from `acceptedGaps`, invoice payload build logs a warning and publishes activity `ChQrKnownGapsOutstanding` (261). The invoice is not blocked.

This record does not change `QrRechnungPdf`, does not set `QrRechnung:BankSubmit:Enabled`, and does not claim SIX IG 2.3 or MWST compliance. Option A now logs the download and the operator's bank-portal upload note. Option B is out of scope.

### Option B decision (2026-09-30)

**Decision: Option B is not in scope.** Regkasse does not originate pain.001 and does not open an EBICS session. This section does not enable `QrRechnung:BankSubmit`, does not add a table, and does not claim SIX or MWST compliance.

#### Scope

QR-Rechnung in this product is a bill the customer pays from the PDF (`QrRechnungPdf`). pain.001 over EBICS is a credit-transfer initiation: the software would move money on a tenant's bank contract. That is a different channel from printing a payment part. Paket 21 already chose Option D (print and PDF only) and said not to expand that package into Option B.

This record does not name a bank or an EBICS 3.0 client. Swiss corporate banks that offer EBICS generally do so for the payer's payment file, not as a required way for a POS to lodge a QR-bill. Picking a bank here would be an unverified partnership claim.

#### Operational workaround

Option A is the workaround, and it is already in code as audit only:

1. Super Admin downloads the existing PDF: `GET /api/admin/tenants/{tenantId}/ch-qr-invoices/{invoiceId}/pdf`. The download is refused (HTTP 409 `CH_QR_GAPS_NOT_ACCEPTED`) unless every open gap (`present: false`) is in `Fiscal.ChQrKnownGapsAccepted`, and it is refused when `QrRechnung:BankSubmit:Enabled` is true.
2. The operator uploads that PDF in their own bank portal, outside Regkasse.
3. Super Admin records the note: `POST .../upload-confirmation` with `uploadedBy`, `uploadedAtUtc`, and `bankReference`.

Audit `QrRechnungPdfDownloaded` is 116. Audit `QrRechnungBankUploadConfirmed` is 117. Neither call stores a bank key or opens bank HTTP. A file already uploaded at the bank cannot be unsent from Regkasse.

#### Why not Option B

- No bank client exists. `QrRechnungBuilder.BuildPdfAsync` stops at PDF bytes.
- `ChQrKnownGaps.json` keeps `pain001` and `bank-scan` at `present: false`.
- `QrRechnung:BankSubmit:Enabled` defaults to false. Production and Staging refuse `true` at startup (`CountryFiscalLockEvaluator.ReasonQrBankSubmit`).
- EBICS keys must not sit in `company_settings` or in `tenant_settings` as plain text. There is no approved vault and no licensed client dependency in this repo.
- A duplicated or rejected bank payment would need a bank-side reversal. Regkasse cannot reverse a payment it never sent, and it must not pretend an audit row is that reversal.

#### If a later package reopens Option B

Do not start that package unless all of these are true:

- At least three paying CH tenants have asked for it in writing, naming the bank.
- That bank has a test contract for pain.001 over EBICS, and the contract says the file is the right instrument for this QR-bill use. A portal upload still available to those tenants is not enough to reopen Option B.
- Security has chosen an external vault (Azure Key Vault, AWS Secrets Manager, or HashiCorp Vault) for the tenant's EBICS key material. A `bank_credentials` table is allowed only if every secret column is ciphertext and the encryption key comes from the deployment secret store. Plaintext in `company_settings` or `tenant_settings` stays forbidden.
- The package names one EBICS library and its license (open-source terms or a paid contract) before the dependency is added. This record does not choose `EbicsClient.NET`, Treasury-NET, or a vendor SDK.
- New audit events `QrRechnungBankSubmitRequested`, `QrRechnungBankSubmitSucceeded`, and `QrRechnungBankSubmitFailed` get new numbers in that package. They must not reuse 116 or 117. Each row stores actor, tenant id, invoice id, and a bank reference. It does not store the key, the pain.001 XML, or the IBAN.
- Rollback in that package is: set `QrRechnung:BankSubmit:Enabled=false`, stop new sessions, and keep the audit rows. A payment the bank already accepted is reversed at the bank, not by deleting a Regkasse row.
- `pain001` stays `present: false` until that package is marked production-ready in [`COUNTRIES.md`](COUNTRIES.md) §16. `QrRechnung:BankSubmit:Enabled` stays false until the same mark. This file does not flip it.

#### What stays

`QrRechnung:BankSubmit:Enabled=false` remains the answer. No `bank_credentials` migration. No EBICS client. PDF bytes are unchanged.

### Why Option A is not “done” either

The current PDF is not a measured SIX IG 2.3 print. `QrRechnungPdf` draws Empfangsschein and Zahlteil and a QR matrix, and it leaves these gaps:

- The Swiss cross is painted into the module matrix. It is not the official 7 mm cross with a white border.
- The page uses the default QuestPDF font, not embedded Liberation Sans or Arial.
- There is no perforation mark.
- There is no pain.001 file and no bank scan of the PDF.

The download audit records that file. This plan still does not treat it as a SIX print.

### Production-ready gate for Paket 21

`QrRechnung:BankSubmit:Enabled` stays false. The artifact path is not production-ready until every row below is true. [`COUNTRIES.md`](COUNTRIES.md) §16 is the status table (payload and PDF implemented, bank submit not started).

| Gate | Must be true |
|------|----------------|
| Config | `EInvoicing.QrRechnung` and `Fiscal.MwstCh` are explicit tenant choices, not an accidental go-live. `QrRechnung:BankSubmit:Enabled=false`. `QrRechnung:BuilderMode` is not `dryRun` (`CountryFiscalLockEvaluator`). |
| Tables | `ch_receipt_sequences` exists. There is no `qr_rechnung_documents` table. The audit hash replaces it. That is not a reason to turn bank submit on. |
| Output | Payload is SPC version `0200`, address type S, reference `QRR` / `SCOR` / `NON`. PDF gaps in the list above are closed or explicitly accepted. |
| Audit | `AuditEventType.ChMwstQrBuilt` (106) exists for a canary QR. `QrRechnungPayloadBuilt` (111) and `QrRechnungPdfGenerated` (112) are written by `QrRechnungBuilder`. `newValues` is a hash and, for the PDF, a relative path. The SPC text and the IBAN are not logged. |
| Tests | Payload starts with `SPC` and version `0200`. PDF bytes are produced. Production startup fails when bank submit is true. No test calls a bank. |

### Rollback

Rollback of the switch is `QrRechnung:BankSubmit:Enabled=false`. Production and Staging already refuse `true` at startup. Nothing was sent, so there is no bank transaction to reverse. There is no stored payload table to delete. A PDF that an operator already downloaded or uploaded outside Regkasse cannot be unsent from here. Do not route the tenant to Austrian TSE.
