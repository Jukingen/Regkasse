# RKSV — Receipt / Invoice Fields and Implementation Status

> **Legal notice:** This table is not a complete interpretation of Austrian RKSV law. It only documents what exists **in this repo** on the data model, API, and print paths. It does not claim compliance or legal certainty.

> **Language:** Explanations are in English. Fixed German UI strings (for example template footers) may be quoted as they appear in code.

---

## Data sources (summary)

| Source | File / table (selected) |
|--------|------------------------|
| Receipt DTO | `backend/DTOs/ReceiptDTO.cs`, `ReceiptService.MapToDtoAsync` |
| Receipt entity | `backend/Models/Receipt.cs`, `ReceiptItem`, `ReceiptTaxLine` |
| POS invoice | `backend/Models/Invoice.cs` (`invoices`) |
| Payment | `backend/Models/PaymentDetails.cs` (TSE, `ReceiptNumber`, `TaxDetails` JSON) |
| TSE signature payload | `backend/Tse/BelegdatenPayload.cs` |
| Beleg number | `backend/Services/ReceiptSequenceService.cs` → `AT-{RegisterNumber}-{yyyyMMdd}-{seq}` |
| Tax rates | `TaxTypes` in `backend/Models/Product.cs` + `TaxType` enum |
| Company information | `CompanyProfileOptions` (used in `ReceiptService` / `PaymentService`) |
| RKSV Sonderbelege (admin) | `RksvSpecialReceiptsController`, `RksvSpecialReceiptService`, `RksvSpecialReceiptDtos.cs`; RKSV fields on the payment entity (`PaymentDetails` / `Models`) |

---

## Field-by-field review

For each row below: **Purpose (business/legal context — not definitive legal advice)**, **Repo status**, **Related code**, **Display / print / export**, **Gap risk**, **Suggested technical step (code was not written)**.

### 1. Unternehmername / company name

| | |
|--|--|
| **Purpose** | Identify the business on the Beleg (typical RKSV set together with UID). |
| **Status** | **Implemented:** `ReceiptCompanyDTO.Name` ← `_companyProfile.CompanyName` (`ReceiptService.MapToDtoAsync`). `Invoice.CompanyName` is set on the POS invoice (invoice create inside `PaymentService`). |
| **Code** | `ReceiptService.cs`, `Invoice.cs`, `DTOs/ReceiptDTO.cs` |
| **Display** | Can be passed into the company block of the POS receipt template / `receiptFormatter.ts` (client). |
| **Risk** | Empty or wrong profile → incorrect name on the customer receipt. |
| **Suggestion** | `CompanyProfile` validation and per-environment configuration checks. |

### 2. Fortlaufende Belegnummer

| | |
|--|--|
| **Purpose** | Unique, incrementing Beleg number per cash register. |
| **Status** | **Implemented:** `ReceiptSequenceService` sequence per day + cash register; format `AT-{registerNumber}-{yyyyMMdd}-{n}`. Aligned use with `Receipt.ReceiptNumber`, `PaymentDetails.ReceiptNumber`, `Invoice.InvoiceNumber`. |
| **Code** | `ReceiptSequenceService.cs`, `PaymentService.cs` (allocate), `Receipt.cs` |
| **Display** | Yes (`ReceiptDTO.ReceiptNumber`). |
| **Risk** | Collision or sequence drift (rare race); DB unique-index strategy is supported by separate migrations in the repo. |
| **Suggestion** | Production observation: alert on unique violations of sequence and `ReceiptNumber`. |

### 3. Datum der Belegausstellung

| | |
|--|--|
| **Purpose** | Issue date of the Beleg. |
| **Status** | **Implemented:** `Receipt.IssuedAt` = payment `CreatedAt`; DTO field `Date`. TSE payload `BelegdatenPayload.BelegDatum` (filled in `DD.MM.YYYY` format — payload class). |
| **Code** | `ReceiptService`, `BelegdatenPayload.cs`, `ReceiptDTO.cs` |
| **Display** | Yes (DTO `Date`). |
| **Risk** | Time-zone shift; the code uses `PostgreSqlUtcDateTime` for Vienna day and UTC conversions (closings/reports). |
| **Suggestion** | Operational documentation of local time-zone clarity (IST/CEST) on the receipt view. |

### 4. Uhrzeit der Belegausstellung

| | |
|--|--|
| **Purpose** | Issue time of the Beleg. |
| **Status** | **Partial:** `ReceiptDTO.Date` carries a single `DateTime` (date+time); TSE payload has `Uhrzeit` (`HH:mm:ss`). On the print side, separate fields can be derived from the combined date-time in `receiptFormatter`. |
| **Code** | `BelegdatenPayload.Uhrzeit`, `ReceiptDTO.Date` |
| **Display** | Depends on client formatting. |
| **Risk** | Missing time on a date-only template. |
| **Suggestion** | Explicit time row according to template requirements. |

### 5. Kassenidentifikationsnummer

| | |
|--|--|
| **Purpose** | RKSV Kassen-ID (in writing usually `CashRegisters.RegisterNumber`). |
| **Status** | **Implemented:** `ReceiptDTO.KassenID` / `DisplayRegisterNumber` = `RegisterNumber`; `Invoice.KassenId` is the same text. |
| **Code** | `ReceiptService.MapToDtoAsync`, `Invoice.cs` |
| **Display** | Yes. |
| **Risk** | Mixing GUID with text; DTO comments say this is `RegisterNumber`, not the register GUID. |
| **Suggestion** | Training: Kassen-ID vs internal UUID distinction for operators. |

### 6. Menge und handelsübliche Bezeichnung

| | |
|--|--|
| **Purpose** | Quantity and commercial product/service name per line. |
| **Status** | **Implemented:** `ReceiptItem` / `ReceiptItemDTO` (`Name`, `Quantity`). Lines with a `+ ` prefix for the old modifier structure (`ReceiptService`). |
| **Code** | `ReceiptService.cs`, `Models/ReceiptItem.cs` |
| **Display** | Yes. |
| **Risk** | Readability on legacy modifier lines. |
| **Suggestion** | Track the move to the Phase 2 flat product model (`Phase2.LegacyModifier` message in logs). |

### 7. Zahlungsbetrag (payment amount)

| | |
|--|--|
| **Purpose** | Total paid by the customer. |
| **Status** | **Implemented:** `Receipt.GrandTotal`, `ReceiptDTO.GrandTotal`, TSE payload `Betrag` (string format). |
| **Code** | `ReceiptService`, `BelegdatenPayload` |
| **Display** | Yes. |
| **Risk** | Cash tendered/change (`Tendered`/`Change`) uses simplified defaults on the receipt DTO: `ReceiptService` assigns `Tendered = GrandTotal`, `Change = 0` on the `Payments` list — **cash detail may not be enriched at the top level**. |
| **Suggestion** | If real `Tendered`/`Change` exists on cash transactions, fill from the payment source (separate development topic). |

### 8. Amount breakdown by tax rate (20%, 10%, 13%, 0%, 19%)

| Rate | **Status** |
|------|------------|
| **20%** | **Implemented** — `TaxTypes.Standard` → `20.0m` (`Product.cs`). |
| **10%** | **Implemented** — `TaxTypes.Reduced` → `10.0m`. |
| **13%** | **Implemented** — `TaxTypes.Special` → `13.0m` (hospitality and similar class). |
| **0%** | **Implemented** — `TaxTypes.ZeroRate` / `TaxType.ZeroRate` (2026 reform note on the enum). |
| **19%** | **Not found in current implementation** — no 19% branch in `TaxTypes.GetTaxRate`; a non-Austrian or transitional rate is **not defined** in this fixed table. |

**Code:** `TaxType.cs`, `Product.cs` (`TaxTypes`), `ReceiptTaxLine` aggregation in `ReceiptService`.

**Display:** `ReceiptDTO.TaxRates` (rate, net, tax, gross).

**Risk:** Wrong classification if a special scenario that needs 19% (for example complex EU B2B) is not in this table.

**Suggestion:** If 19% must be added to the business rules that manage product tax, extend `TaxTypes` and validations (not present today).

### 9. Zahlungsart (payment method)

| | |
|--|--|
| **Purpose** | Document how the payment was taken. |
| **Status** | **Implemented:** `Invoice.PaymentMethod` (`PaymentMethod` enum), `ReceiptPaymentDTO.Method` on the receipt DTO (`PaymentMethod.ToString()`). |
| **Code** | `Invoice.cs`, `ReceiptService.MapToDtoAsync` |
| **Display** | Yes (DTO). |
| **Risk** | Split payment is summarized on a single line (list has one element). |
| **Suggestion** | Data-model extension if split-payment support is required. |

### 10. RKSV machine-readable code / QR payload

| | |
|--|--|
| **Purpose** | RKS-V QR text (for RKSV verification). |
| **Status** | **Implemented:** `Receipt.QrCodePayload` and `ReceiptSignatureDTO.QrData`; format `_R1-AT1_{register}_{ReceiptNumber}_{timestamp:sortable}_{total:0.00}_0.00_{certSerial}_{jws}`. |
| **Code** | `ReceiptService.AddReceiptFromPaymentToContextAsync` |
| **Display** | Input to QR generation in `receiptFormatter.ts` / `receiptPrinter.ts`. |
| **Risk** | The fixed `0.00` segment (inside the payload) has meaning under the business rules; changing it affects signature/compliance. |
| **Suggestion** | External verification of segment meaning against the RKSV specification (legal + vendor documentation). |

### 11. Signature / TSE / RKSV (JWS)

| | |
|--|--|
| **Purpose** | Sign the Beleg with TSE and keep it verifiable. |
| **Status** | **Implemented:** `PaymentDetails.TseSignature` (compact JWS), `Receipt.SignatureValue`, parsed columns (`SignatureFormat`, `JwsHeader`, `JwsPayload`, `JwsSignature` — `Receipt` entity). `Invoice.TseSignature` is required. |
| **Code** | `PaymentService`, `Receipt.cs`, `Invoice.cs`, `Tse` layer |
| **Display** | `ReceiptSignatureDTO` (algorithm, serial number, timestamp, previous signature, signature value). |
| **Risk** | Payment policy when TSE is down (demo / mandatory flags) is a separate set of business rules; daily closing requires TSE. |
| **Suggestion** | Per-environment TSE-mandatory runbook. |

### 12. Certificate serial number

| | |
|--|--|
| **Purpose** | Trail of which TSE certificate signed the receipt. |
| **Status** | **Implemented:** `ITseService.GetTseCertificateInfoAsync` → `ReceiptSignatureDTO.SerialNumber` (`CertificateNumber`). |
| **Code** | `ReceiptService.cs` |
| **Display** | Yes on the DTO. |
| **Risk** | Mapping archives to an old serial number during certificate rotation. |
| **Suggestion** | Document the certificate renewal procedure. |

### 13. Previous Beleg signature (chain)

| | |
|--|--|
| **Purpose** | DEP / RKSV chain integrity. |
| **Status** | **Implemented:** `Receipt.PrevSignatureValue`; source `PaymentDetails.PrevSignatureValueUsed` or `SignatureChainState.LastSignature` / last receipt (`GetLastSignatureValueForCashRegisterAsync`). |
| **Code** | `ReceiptService.cs` |
| **Display** | `ReceiptSignatureDTO.PrevSignatureValue`. |
| **Risk** | Chain break → may relate to `IntegrityCheckService` and fiscal export warnings. |
| **Suggestion** | Periodic check of `/rksv/integrity` and `api/admin/integrity`. |

### 14. Umsatzzähler (encrypted counter) — separate field

| | |
|--|--|
| **Purpose** | Encrypted turnover counter in some RKSV/TSE models. |
| **Status** | **Not found in current implementation** — there is **no** explicit `Umsatzzähler` database field or DTO field; the value **may** be embedded **inside the JWS** in a TSE-vendor-specific way — this is an **inference outside repo evidence** and is not certain. |
| **Code** | — |
| **Display** | **None** as a separate row. |
| **Risk** | Gap between auditor expectation and UI presentation. |
| **Suggestion** | JWS decode runbook based on the TSE provider documentation in use (with legal counsel). |

### 15. DEP / audit-store reference

| | |
|--|--|
| **Purpose** | DEP row or package reference. |
| **Status** | **Partial:** The QR payload is usually **not** a DEP row (comment on `FinanzOnlineService`). DEP-pattern validation exists for `belegpruefung`. The fiscal export package includes receipt + signature + chain status (`IFiscalExportService`). There is **no** single “DEP row id” field **on Receipt**. |
| **Code** | `FinanzOnlineRkdbBelegpruefungValidator`, `FiscalExportController` |
| **Display** | Direct DEP ref. on the receipt is **absent** (verified). |
| **Risk** | Inability to produce a suitable `beleg` on FinanzOnline TEST submission. |
| **Suggestion** | Vendor DEP export integration if needed (separate project). |

### 16. Storno / RKSV Sonderbeleg fields (`RksvSpecialReceiptKind`)

| | |
|--|--|
| **Storno** | **Partial:** `PaymentDetails.IsStorno` → `ReceiptDTO.FiscalTraceKind = "Storno"` (`ReceiptService.MapToDtoAsync`). On `Invoice`: `DocumentType`, `StornoReasonCode`, `StornoReasonText`, `CreditNote` enum value. A separate printed “STORNO” watermark was **not verified in this document** (template layer). |
| **RKSV special receipt kinds** | **Implemented (admin):** `POST api/rksv/special-receipts/*` (`RksvSpecialReceiptsController`) → `RksvSpecialReceiptService`. On the payment row: `RksvSpecialReceiptKind` (`Nullbeleg`, `Startbeleg`, `Monatsbeleg`, `Jahresbeleg`, `Schlussbeleg` and similar constants in `RksvSpecialReceiptKinds`). Receipt DTO: `ReceiptDTO.RksvSpecialReceiptKind`, `RksvNullbelegActsAsJahresbeleg`, optional `RksvFinanzOnlineSubmission` (`RksvFinanzOnlineSubmissionStatusDto`) — the last field is filled only on the **Startbeleg** and **Jahresbeleg** create paths (aligned with `RksvSpecialReceiptFinanzOnlineSubmissionTracker` / outbox handler; UI side `isRksvFinanzOnlineTrackedSpecialReceiptKind`). |
| **Jahresbericht (formal report)** | Must not be confused with Jahres**beleg** (Sonderbeleg); the formal annual report has a separate controller (`docs/RKSV_CASH_REGISTER_OPERATIONS.md` section 3). |

**Code:** `Invoice.cs`, `PaymentDetails` (RKSV fields), `ReceiptDTO.cs`, `ReceiptService.cs`, `RksvSpecialReceiptDtos.cs`

**Risk:** The distinction between refund (`Refund`) and Storno must be operationally clear (`FiscalTraceKind` `"Refund"`).

**Suggestion:** Operational documentation of `InvoiceController` and POS refund paths for credit-note / Storno workflow; for Sonderbelege see `RKSV_CASH_REGISTER_OPERATIONS.md` section 4.

### 17. FinanzOnline verification URL (printed as text)

| | |
|--|--|
| **Purpose** | Vorgehensweise / verification-link text. |
| **Status** | **There is no `VerificationUrl` field on the backend C# `ReceiptDTO`** (grep: none). The frontend type `frontend/types/ReceiptDTO.ts` defines optional `verificationUrl`; `receiptFormatter.ts` / `receiptPrinter.ts` print it if present. Evidence that the API fills it was **not found**. |
| **Code** | `frontend/types/ReceiptDTO.ts`, `receiptFormatter.ts` |
| **Display** | If the API does not fill it, it **may not appear** on the receipt. |
| **Risk** | Missing customer information. |
| **Suggestion** | Produce the URL via backend `ReceiptDTO` + `MapToDtoAsync` (separate work). |

### 18. UID / Steuernummer (company)

| | |
|--|--|
| **Purpose** | Tax number (ATU…). |
| **Status** | **Implemented:** `ReceiptCompanyDTO.TaxNumber` — priority `receipt.Payment?.Steuernummer`, otherwise `_companyProfile.TaxNumber`. `Invoice.CompanyTaxNumber` is required. |
| **Code** | `ReceiptService.MapToDtoAsync`, `Invoice.cs` |
| **Display** | Yes. |
| **Risk** | Mixing customer UID with company UID on the payment row; the business rule depends on `Steuernummer` selection (`PaymentService`). |

---

## Printing and templates

- **Physical print:** `frontend/services/receiptPrinter.ts`, `receiptFormatter.ts`.
- **Template management (non-fiscal example):** `MultilingualReceiptController` — description says “Non-fiscal … does not create Payment, Receipt, or TSE records” (`GenerateReceipt`).

---

## Receipt fields in export

- **Fiscal export JSON:** `GET api/admin/fiscal-export` (`FiscalExportController`, `IFiscalExportService`) — package contents include receipt + signature + closing + chain warnings (profile-based).
- **Legal export completeness:** A gate for reports; not a direct receipt-field list (`LegalExportCompletenessController`).

---

## Summary table — high level

| Field | Implementation |
|------|-----------|
| Unternehmername | Present |
| Belegnummer | Present (`AT-…`) |
| Datum / Uhrzeit | Present (DTO `Date`; separate-field policy is client-side) |
| Kassen-ID | Present |
| Menge / Bezeichnung | Present |
| Zahlungsbetrag | Present |
| Tax 20/10/13/0 | Present (fixed table) |
| Tax 19% | **Absent** |
| Zahlungsart | Present (simple list) |
| QR / RKS text | Present |
| JWS / TSE | Present |
| Certificate SN | Present |
| Previous signature | Present |
| Umsatzzähler (separate) | **Absent** |
| DEP reference (receipt row) | **Absent** (separate validation / export layers exist) |
| RKSV Sonderbelege (Nullbeleg, Startbeleg, Monatsbeleg, Jahresbeleg, Schlussbeleg) | **Present** (admin API + `RksvSpecialReceiptKind`; FO submission DTO: Startbeleg + Jahresbeleg) |
| Jahresbericht (formal annual report) | Separate product (not Jahres**beleg**) |
| RKSV verification URL (API) | **Absent on the backend** |

---

## Final checklist

### Missing or high-risk compliance areas (technical view)

1. **19% VAT** — not defined in the product tax table (`TaxTypes`).
2. **RKSV Sonderbelege** — implemented; the dedicated FinanzOnline submission trail (`RksvSpecialReceiptFinanzOnlineSubmissions`) is filled only on the Startbeleg and Jahresbeleg create paths. No row is created in that table for other Sonderbeleg kinds.
3. **Umsatzzähler** — no explicit field; it may be embedded inside TSE (uncertain).
4. **FinanzOnline verification URL** — absent on backend `ReceiptDTO`; print on the receipt is not bound to the API.
5. **FinanzOnline payload of formal reports** — Non-DEP summary in code notes; classic DEP file production was not evidenced in the scope of this document.
6. **Cash change detail** — `ReceiptService` payment-list defaults may be simplified.

### Documentation gaps

- The distinction between the POS **reports** screen and formal **Tagesbericht** should be made clear to operators.
- For FinanzOnline behavior after monthly/yearly **Tagesabschluss**, the codebase and the operations runbook should be aligned.

### Suggested next development work (priority idea — legal approval is separate)

1. Backend contract and POS template fill for `VerificationUrl`.
2. If needed, add **19%** or an EU-specific rate to `TaxTypes` and product validation (after the business requirement is clear).
3. Enrich `ReceiptPaymentDTO` (split payment, cash change).
4. Product decision + code alignment on whether FinanzOnline submission for monthly/yearly closing should match the daily path.
