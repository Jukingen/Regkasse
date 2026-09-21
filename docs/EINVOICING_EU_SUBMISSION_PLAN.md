# EU e-invoicing submission plan (Paket 22)

**Last updated:** 2026-09-21  
**Status:** Decision record only. **Implementation is NOT STARTED.** EN 16931 / XRechnung XML builders still throw. This is not a legal opinion, not Peppol onboarding, and not a ViDA go-live date.  
**Hub:** [`EINVOICING_EU.md`](EINVOICING_EU.md) · [`COUNTRIES.md`](COUNTRIES.md) §16 · DE e-invoicing vs TSE: [`FISCAL_GERMANY.md`](FISCAL_GERMANY.md)

v1 **does not** operate a Regkasse-owned Peppol Access Point. v1 **does not** submit to tax authorities. First coding slice is **validate-only**.

---

## 1. Channel comparison

| Channel | What it is | Syntax | When we would use it |
|---------|------------|--------|----------------------|
| **Peppol BIS Billing 3.0** | OpenPeppol CIUS of EN 16931 + **AS4 four-corner** network (AP + SMP + SML) | **UBL 2.1 only** | Default **cross-border EU** and DE B2G/B2B when the buyer is on Peppol. DE-NRS (German national ruleset) inside BIS since 2025-02-17. |
| **DE XRechnung (KoSIT)** | German CIUS of EN 16931. B2G via **ZRE / OZG-RE**; B2B accepted under the E-Rechnung rules | UBL **or** CII | Domestic DE when the buyer demands XRechnung. UBL XRechnung can travel as Peppol BIS; **CII XRechnung is not a Peppol document type**. |
| **DE ZUGFeRD / FR Factur-X** | Hybrid **PDF/A-3 + CII XML** | CII inside PDF | Human-readable DE/FR invoices. **Not** the v1 wire format (see §3). |
| **FR Chorus Pro** | French **B2G** intake (PPF) | Chorus / Factur-X / UBL depending on flow | French public buyers. French **B2B** is the PDP / e-invoicing reform — **not** Chorus Pro and **not** Peppol-as-mandate. Confirm the current DGFIP calendar before any FR B2B pilot. |
| **IT SDI (FatturaPA)** | Agenzia delle Entrate **Sistema di Interscambio**. Mandatory domestic B2B/B2G | **FatturaPA XML** (not EN 16931 UBL) | Italy domestic. Peppol does not replace SDI. |
| **ViDA “central model”** | EU VAT in the Digital Age: **digital reporting** for intra-EU B2B (Council text; reporting horizon **~2030**, not a 2026 network) | EN 16931-aligned reporting, not a POS protocol | **Readiness report only** (`en16931Ready`, `viesEnabled`, …). Do not build a ViDA submission client in this package. |

Peppol is a **transport + CIUS**, not a replacement for every national clearance platform. Italy and Poland (KSeF) stay national-clearance-first.

---

## 2. Recommended channel per target country

Mandants keep an ISO country on `company_settings.country`. `EU_DEFAULT` is **not** selectable. CH is in this table because operators ask; **QR-bill is Paket 21**, not Peppol.

| Country | v1 recommended channel | Do not treat as |
|---------|------------------------|-----------------|
| **DE** | **Peppol BIS 3.0 (UBL)** to Peppol buyers; **XRechnung UBL** for ZRE/OZG-RE B2G. Receive-side EN 16931 from 2025-01-01 is a **buyer** duty — issuing mandate phases 2027/2028 (turnover thresholds). | USB TSE / KassenSichV (Paket 20). ZUGFeRD hybrid is v2. |
| **CH** | **QR-Rechnung** ([`FISCAL_SWITZERLAND_QR_PLAN.md`](FISCAL_SWITZERLAND_QR_PLAN.md)). Optional later: eBill. Peppol only if a specific counterparty demands it. | ViDA, Peppol mandate, EN 16931 B2G. |
| **FR** | **Chorus Pro** for B2G. B2B: **PDP / Factur-X** per current DGFIP reform — **out of v1** until Legal confirms the live calendar. | Peppol as the French B2B legal channel. |
| **IT** | **SDI + FatturaPA** (dedicated later package). v1: do not emit Peppol as a substitute for SDI. | EN 16931 UBL as Italian domestic invoice. |
| **NL** | **Peppol** (NLCIUS / Simplerinvoicing practices). | A French/Italian clearance portal. |
| **ES** | **FACe** (B2G, Facturae). B2B: **Verifactu** / **TicketBAI** (Basque) are fiscal-software duties, not Peppol. Peppol optional for EU counterparties. | Peppol as the Spanish SII/Verifactu replacement. |
| **PL** | **KSeF** national platform (use the current MF timetable; it has moved before). Peppol only for cross-border counterparties that are not on KSeF. | Peppol as the Polish B2B mandate. |

v1 **product scope:** DE Peppol/XRechnung **validator** + hosted-AP **pilot** for DE (and NL if a mandant needs it). FR/IT/ES/PL = **documentation + refuse-to-submit** until a dedicated package. CH = Paket 21.

---

## 3. UBL 2.1 vs CII

**Decision: canonical syntax is UBL 2.1 (Peppol BIS Billing 3.0).**

| Syntax | Use |
|--------|-----|
| **UBL 2.1** | v1 builder (`IEn16931XmlBuilder`). Peppol BIS 3.0. DE-NRS / XRechnung UBL. |
| **UN/CEFACT CII** | **Not** in v1. Needed later for ZUGFeRD/Factur-X hybrid and CII-only XRechnung. `IXrechnungXmlBuilder` may wrap the same UBL mapping in v1 and leave CII as `NotImplementedException`. |

Rationale: one Schematron/testbed path; Peppol does not carry CII XRechnung; AT reverse-charge disclosures already exist without XML (Paket 12-c). Building two syntaxes before the first valid UBL file is waste.

`EInvoicing.En16931` continues to gate XML builders, not reverse-charge **tax**. `EInvoicing.XRechnung` stays off until a DE B2G pilot. `EInvoicing.Zugferd` stays off (not country-derived).

---

## 4. Peppol Access Point registration

**v1: do not register Regkasse as an OpenPeppol Access Point.**

Operating an AP requires OpenPeppol membership, a Peppol Service Provider Agreement with the Peppol Authority of the **legal seat** (AT → Austrian authority if listed, else OpenPeppol AISBL), company-registration docs, AS4 stack, **testbed** certification, production **PKI**, SMP publication, and often ISO 27001 / PASR (Germany: KoSIT PASR). SML insourcing deadlines in 2026 (SMP registration **2026-05-31**, AP DNS lookup **2026-08-31**) are the **AP operator’s** problem — not a reason to stand up our own AP now.

**v1 transport:** a **certified hosted Access Point** (shared / white-label). Regkasse is C1 (issuer software). The provider is C2/C3. Each mandant that must be reachable gets a Peppol **participant id** (e.g. `iso6523-actorid-upis::9915:<VAT>` or the scheme the AP assigns) published on the provider’s SMP.

Checklist before a DE pilot (provider does the network steps):

1. DPA + Peppol participant agreement with the AP.  
2. Testbed document type: BIS Billing 3.0 invoice + credit note.  
3. Production PKI lives at the AP, not in `appsettings`.  
4. Super Admin stores **participant id + AP environment** per tenant — not AS4 certs.  
5. Fail closed if AP is unset: validate XML, **do not send**.

Own-AP is a later ops decision after volume and ISO 27001.

---

## 5. Phased rollout

| Phase | Scope | Exit |
|-------|--------|------|
| **1. Validator-only** | UBL 2.1 invoice from `InvoiceDocumentDto` + EN 16931 / Peppol BIS 3.0 Schematron (KoSIT + Peppol test files). Persist XML. **No AS4, no Chorus, no SDI, no KSeF.** | Golden files; CI schematron; AT CountryBaseline unchanged. |
| **2. Pilot** | One **DE** canary mandant, hosted AP **TEST**, send to a test participant. XRechnung UBL to a ZRE test endpoint **or** Peppol test buyer — not both in the first week. | Delivery ACK; audit row; rollback = flag off. |
| **3. Production** | DE mandants who opt in, hosted AP **LIVE**. Credit notes. Receive path only if the AP offers inbound (otherwise receive stays “buyer duty”). | Runbook; support playbook; no FR/IT/PL clearance. |

Rollback: `EInvoicing.En16931` / `EInvoicing.XRechnung` off; leave XML artifacts. Do not drop tables.

**ViDA:** keep the read-only readiness object. Update fields when reporting specs freeze; still no submission API in this package.

---

## 6. Config, schema, audit, FA

### Config (planned)

```json
"En16931": {
  "Syntax": "Ubl21",
  "ValidatorMode": "schematron"
},
"Peppol": {
  "AccessPointMode": "hosted",
  "Environment": "TEST",
  "Provider": "not-configured"
}
```

Do not put AP certs in git. `Peppol:AccessPointMode=own` stays unimplemented.

### Migration (additive)

`einvoice_documents`: tenant_id, invoice_id, syntax, profile (`PEPPOL_BIS_3` / `XRECHNUNG_UBL`), xml artifact, schematron result.  
`einvoice_submissions`: outbox (Queued/Sent/Ack/Failed), participant id, correlation id — empty until pilot.

### Audit (≥ 100)

| Name | When |
|------|------|
| `EinvoiceValidated` | Schematron pass/fail (fail stores rule ids, not the full XML if huge) |
| `EinvoiceSubmitted` | AP accepted (pilot+) |
| `EinvoiceSubmissionFailed` | Transport/NACK |
| `EinvoiceSettingsChanged` | Super Admin AP / flag overlay |

Never log VAT-ID + full XML together in activity feed.

### FA

| Surface | Who |
|---------|-----|
| `/admin/einvoicing` | Super Admin: validator, AP env, canary tenant |
| Tenant invoice: “EN 16931 XML” download | Mandanten-Admin when flag on |
| Readiness card | `en16931Ready`, `viesEnabled`, `ossRegistered`, `eInvoicingCapable` (still read-only) |

i18n `einvoicing.*` de/en/tr. OpenAPI + Orval with the API. POS does **not** send Peppol from the till.

---

## 7. Explicit non-goals (v1)

- Own Peppol AP / SMP / SML  
- FatturaPA, Chorus B2B PDP, KSeF, Verifactu, TicketBAI clients  
- CII / ZUGFeRD PDF embedding  
- Live VIES (`Vies.CheckEnabled` stays default off)  
- OSS destination rates (Paket **30-d**, still NOT STARTED)  
- ViDA central reporting API  

---

## Related docs

- [`EINVOICING_EU.md`](EINVOICING_EU.md)  
- [`FISCAL_GERMANY.md`](FISCAL_GERMANY.md) / [`FISCAL_GERMANY_PROVIDER_DECISION.md`](FISCAL_GERMANY_PROVIDER_DECISION.md) — TSE ≠ e-invoice  
- [`FISCAL_SWITZERLAND_QR_PLAN.md`](FISCAL_SWITZERLAND_QR_PLAN.md)  
- [`COUNTRY_LAYER_CUTOVER.md`](COUNTRY_LAYER_CUTOVER.md)  
- Peppol BIS Billing 3.0: [docs.peppol.eu/poacc/billing/3.0/bis](https://docs.peppol.eu/poacc/billing/3.0/bis/)
