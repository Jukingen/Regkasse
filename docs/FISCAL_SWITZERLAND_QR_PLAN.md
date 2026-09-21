# Switzerland QR-Rechnung implementation plan (Paket 21)

**Last updated:** 2026-09-21  
**Status:** Plan only. **Implementation is NOT STARTED** except the existing payload **shape** (`IQrRechnungBuilder` / `QrRechnungBuilder`). This is not a legal opinion and does not claim SIX, SPS, or MWST compliance.  
**Hub:** [`FISCAL_SWITZERLAND.md`](FISCAL_SWITZERLAND.md) · [`COUNTRIES.md`](COUNTRIES.md) §16

**There is no bank HTTP API in this package.** A QR-bill is a **payment instrument** the payer’s CH/LI bank scans. “Bank submission” in the remaining-gaps table means **bank-compatible QR-bill output**, not a credit-transfer origination API. eBill is a separate network and is **out of v1**.

Do not enable Austrian RKSV/TSE for CH tenants.

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

Today `BuildPdfAsync` throws (`docs/FISCAL_SWITZERLAND.md`). Payload-only stays the first coding slice.

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
| **1. Payload-only** | Finish validation (checksum, QRR/SCOR/NON, type S). Persist payload JSON. `BuildPdfAsync` still throws. | Golden-vector tests vs SIX examples. |
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

### Migration (additive)

`qr_rechnung_documents`: tenant_id, invoice/payment fk, spec_version, reference_type, iban (masked in logs), payload json, pdf artifact id (nullable). Do not store full IBAN in audit `old_values`.

### Audit (new values ≥ 100; do not reuse AT Fiskaly*)

| Name | When |
|------|------|
| `QrRechnungPayloadBuilt` | Validated payload stored |
| `QrRechnungPdfGenerated` | PDF bytes stored / downloaded |
| `QrRechnungValidationFailed` | Bad IBAN/ref (no secret in payload) |

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
- [`EINVOICING_EU_SUBMISSION_PLAN.md`](EINVOICING_EU_SUBMISSION_PLAN.md) — CH is **not** Peppol-first  
- [`ENVIRONMENT_CONFIGURATION.md`](ENVIRONMENT_CONFIGURATION.md) §4 (`QrRechnung` lock)
