# BMF Belegcheck — Manual Workflow and System Summary

> **Legal notice:** This document is for **operational guidance**. It is **not legal advice**. For binding information and deadlines on Austrian RKSV, FinanzOnline, and BMF processes, rely on the competent authorities, official guides, and specialist opinion.

> **Language:** Explanations are in **English**. **German** terms that are established in the user interface and official process (for example **Startbeleg**, **Jahresbeleg**, **FinanzOnline**, **BMF Belegcheck**) are left in **German** in this text as well.

---

## 1. What is BMF Belegcheck?

**BMF Belegcheck** can be thought of as the set of processes and tools around the Federal Ministry of Finance (**BMF**) for registered cash registers (**Registrierkasse**) that read and verify the **RKSV-QR code** on a **Beleg** (receipt). In practice, operators typically:

- Scan the QR with the **BMF Belegcheck** app on a mobile device,
- If needed, sign in on the **FinanzOnline** side with authentication / a code,
- Note the app’s **verification result** (valid / invalid and reason) in the business records or archive.

This document summarizes the path to follow for **Startbeleg** and **Jahresbeleg** in both **manual** (app + QR) and **in-system** (admin panel, queue, status fields) form. For other **Sonderbelege**, it clarifies the split between **manual verification** and **automatic FinanzOnline submission**.

---

## 2. Which receipts are in scope?

### 2.1. Manual BMF Belegcheck (QR) — all relevant Sonderbelege

For the RKSV **Sonderbelege** below, the **RKSV-QR** on the receipt can be scanned with the official **BMF Belegcheck** app and the result recorded; this path is **not mandated in code** and is an operational choice.

| Receipt kind | Automatic FinanzOnline “RKSV submission” trail in this repo (`RksvSpecialReceiptFinanzOnlineSubmission` / receipt-detail card) | Manual Belegcheck (QR) |
|----------|---------------------------------------------------------------------------------------------------------------------|-------------------------|
| **Nullbeleg** | None — manual verification only (app / operations note). | Yes (if the QR is readable). |
| **Monatsbeleg** | None — **NotRequired** (no separate automatic FON submission by design; December → Jahresbeleg). Decision: [`MONATSBELEG_FINANZONLINE_DECISION.md`](MONATSBELEG_FINANZONLINE_DECISION.md). FA info card + receipt-detail note. | Yes (optional). |
| **Startbeleg** | Present — outbox + submission row after create; worker path. | Yes (recommended operational complement). |
| **Jahresbeleg** | Present — outbox + submission row after create; worker path. | Yes. |
| **Schlussbeleg** | None — `RksvSpecialReceiptService` does not trigger the RKSV special-receipt outbox queue for this kind; **there is no automatic RKSV webservice submission**. | Yes (via QR). |

Summary: **Automatic RKSV special-receipt submission tracked through FinanzOnline** is defined only on the **Startbeleg** and **Jahresbeleg** create paths; for **Monatsbeleg** the product decision is **NotRequired** (DEP storage is required, no separate FON outbox); for **Nullbeleg** the system in this sense only supports manual verification; **Schlussbeleg** is not sent on this automatic path.

### 2.2. Difference between invoice reconciliation (legacy) and RKSV submission

| Concept | What does it update / track? | Typical admin entry |
|--------|---------------------------|-------------------|
| **Invoice / payment-row FinanzOnline reconciliation (legacy)** | Invoice submission via `PaymentService.RetryFinanzOnlineSubmitAsync` → `FinanzOnlineService.SubmitInvoiceAsync`; reconciliation fields such as **FinanzOnlineStatus** on `PaymentDetails` (`FinanzOnlineReconciliationController`, `POST api/admin/finanzonline-reconciliation/retry/{paymentId}`). | FinanzOnline-Abgleich **(Legacy)** queue / retry. |
| **RKSV special-receipt FinanzOnline submission trail** | For **Startbeleg** / **Jahresbeleg**: `rksv_special_receipt_finanz_online_submissions` and the related **FinanzOnline outbox** message types (`RksvStartbelegSubmission`, `RksvJahresbelegSubmission`); `RksvFinanzOnlineSubmission` on the receipt DTO. | FinanzOnline **outbox** worker processing; RKSV FinanzOnline card on the **Beleg** detail. |

**Legacy admin retry** (`.../finanzonline-reconciliation/retry/{paymentId}`) retries the **invoice** submission path; that endpoint does **not** update the **RKSV submission** status shown on the receipt detail for **Startbeleg/Jahresbeleg** (different data model and processing path).

---

## 3. Manual workflow (operator)

The sequence below is typical preparation for a **manual BMF Belegcheck**; small differences can exist by environment and permissions.

### 3.1. Create the receipt (**create receipt**)

1. An authorized user completes the RKSV **Sonderbeleg** flow on the **admin** side for the relevant **Sonderbeleg** kind (**Startbeleg** / **Jahresbeleg** / others — see §2.1).
2. Admin summary: go to the receipt via the **RKSV Sonderbelege** screen (under the RKSV menu in `frontend-admin`) or the **Belegliste**. The FinanzOnline **RKSV submission** summary card is meaningful only on **Startbeleg** and **Jahresbeleg** details.
3. When the receipt is created, the **Receipt** / payment record and TSE signature production complete on the server according to backend rules (detail: `docs/RKSV_RECEIPT_INVOICE_REQUIREMENTS.md`, `docs/RKSV_CASH_REGISTER_OPERATIONS.md`).

### 3.2. Print the receipt (**print receipt**)

1. A physical or PDF copy of the receipt is printed or exported so the QR code stays **readable**.
2. If the QR is damaged, cropped, or contrast is too low, the mobile app cannot read it (see [Section 7](#7-troubleshooting-troubleshooting)).

### 3.3. Scan in the **BMF Belegcheck** app via QR

1. Open the official **BMF Belegcheck** mobile app.
2. Scan the **RKSV-QR** code on the receipt.
3. The app shows a summary result for format and signature-chain aspects of the code.

### 3.4. Authenticate with a **FinanzOnline** code if needed

1. In some scenarios the app asks for the business **FinanzOnline** session or a one-time verification code.
2. The code is entered through a secure channel (authorized person); secrets such as **password / client secret** must **not** be written in this document or in operations notes, and screenshots must not be shared.

### 3.5. Verify the result (**verify result**)

1. Record the app output (for example valid / warning / error code).
2. Internal quality check: confirm that the receipt number, date, and cash register (**Kassen-ID**) match the app result.

### 3.6. Archive (**archive receipt / result**)

1. Keep the printed receipt or PDF, an app screenshot (if corporate policy allows), or the official output in an accessible archive for the **audit retention period**.
2. Observe personal-data minimization and DSGVO / internal policy rules only.

---

## 4. In-system workflow (this repo)

The table below summarizes where developers / system administrators can follow the path through **code and UI**. It does not claim legal “sufficiency.”

| Step | What happens? | Where is it tracked / visible? |
|------|------------|---------------------------|
| Create receipt | **Startbeleg** / **Jahresbeleg** production on the backend uses special-receipt services and payment/receipt records. | Backend: `RksvSpecialReceiptsController`, `RksvSpecialReceiptService` (summary). Admin: **RKSV Sonderbelege** page, receipt-create buttons. |
| QR visibility | QR data is carried in the signature block of the receipt DTO; printing depends on the POS/admin flow. | Admin **Beleg** detail: QR field on `ReceiptDTO` / detail card; template and printer paths on the POS side. |
| Status tracking (RKSV special-receipt FinanzOnline path) | Submission lifecycle for **Startbeleg** and **Jahresbeleg** only: `rksv_special_receipt_finanz_online_submissions` + outbox (`RksvSpecialReceiptFinanzOnlineOutboxHandler`). **Nullbeleg**, **Monatsbeleg**, and **Schlussbeleg** are not tracked in this table. | Status column on the admin **RKSV Sonderbelege** table (tracked kinds only); RKSV FinanzOnline card on the **Beleg** detail. |
| Error view | Last error code / message and last-attempt time are reflected on the DTO (no raw credentials). | Same detail card and logs (technical logs follow the English policy; operator text on German screens). |

For in-admin **QR format check** (parse request to the server), a Belegcheck-like verification page (`/rksv/belegcheck` and similar) can also be used; this does not replace the BMF app — it is for **extra verification** or support.

---

## 5. Webservice (automatic submission) workflow

If the **FinanzOnline** webservice integration and outbox architecture are enabled (company / TSE settings and worker processing):

- The system **enqueues** and processes matching messages (**queue → submit**),
- Status can be updated to values such as **Submitted** / **Verified** on successful protocol steps,
- On transient network or session errors, the outbox **worker** retry policy applies (including RKSV special-receipt messages). **Retry** on the **legacy payment-row reconciliation** screen belongs to **invoice** submission; see §2.2.

If the integration is **off** or the business rule waits for manual approval:

- The tracking row can show states that mean **ManualVerificationRequired** (manual verification needed); the operator completes the result with **BMF Belegcheck** or the official process and updates the archive note.

> **Note:** It is not guaranteed that the “webservice on/off” mapping is the same in every environment; in production, read the `FinanzOnline` configuration, the outbox screen, and the related logs together.

---

## 6. Deadlines (**deadlines**)

- The **legal deadline** and official wording on the BMF / FinanzOnline side for **Jahresbeleg** must be confirmed only from **authoritative sources**; this document does not give a binding calendar.
- **Operational note (not legal advice):** in many business practices the Belegcheck / notification side of the annual receipt is targeted for completion by **15 February of the following year**. This wording is for **internal planning**; it does not replace the statutory text.

---

## 7. Troubleshooting (**troubleshooting**)

| Symptom | Possible cause | Suggested check |
|---------|-------------|------------------|
| **QR missing** | Print crop, template error, or the signature block not landing on the receipt. | POS/admin receipt preview; QR field on the **Beleg** detail; printer DPI / cut areas. |
| **Invalid QR** | Corrupt payload, wrong cash-register sequence, signature-chain mismatch. | **BMF Belegcheck** result code; format check with the admin **Belegcheck** verification page (if present); TSE chain logs (technical). |
| **TSE unavailable** | Hardware or service interruption at signing time. | TSE connection warnings (POS), backend TSE error logs; cash-register **Bereitschaft** checks before receipt create. |
| **FinanzOnline credentials missing** | Missing API user / certificate / company setting. | Company FinanzOnline settings; authorized user; **credential** values must **not** be written into operations documents. |
| **Failed submission** | Network, session, validation, or service rejection (RKSV special-receipt outbox path or invoice path). | **Startbeleg/Jahresbeleg:** receipt detail + FinanzOnline **outbox** (RKSV message type). **Legacy retry** affects invoice fields; it does not update the RKSV submission row (§2.2). |

---

## Related documents

- `docs/RKSV_RECEIPT_INVOICE_REQUIREMENTS.md` — Receipt fields and implementation status.
- `docs/RKSV_CASH_REGISTER_OPERATIONS.md` — Cash register and RKSV operations (admin paths, permissions).
- `docs/RKSV_OFFICIAL_SOURCES.md` — BMF / FinanzOnline / RIS links (external source).

---

*End of document — operational guide; not legal advice.*
