# EU e-invoicing submission plan (Paket 22)

**Last updated:** 2026-09-30  
**Status:** Phase 1 validator-only is partial. UBL sketch plus embedded Schematron. Peppol HTTP, XRechnung CII, and ZUGFeRD are not implemented. This is not a legal opinion, not Peppol onboarding, and not a ViDA go-live date.  
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
| **1. Validator-only** | **Partial.** `En16931UblXmlBuilder` plus embedded `En16931Schematron` (BR subset). Result columns on `invoices`. **No AS4, no Chorus, no SDI, no KSeF.** Full KoSIT / Peppol Schematron files are not in the repo. | `dotnet test --filter En16931XmlBuilderTests` |
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

Do not put AP certs in git. `Peppol:AccessPointMode=own` stays unimplemented. `EInvoicing.Peppol` stays in `FeatureFlagNames.Reserved` and is not in `FeatureFlagNames.All`. Reserved is enforced by `PeppolReservedFlagGuardTests`; do not remove without a matching §-level decision record.

`Peppol:ReservedExit` is read at startup and is not written by this process. Defaults: `Enabled=false`, `CanaryTenantId` empty, `TestAckReceivedAtUtc` null, `ApprovedBy` empty, `ApprovedAtUtc` null. While `Enabled` is false, `FeatureFlagService.Resolve` returns `source=reserved` and `enabled=false`. While `Enabled` is true, a tenant that is not `CanaryTenantId` resolves `source=reserved_exit` and `enabled=false`. The canary tenant resolves `source=reserved_exit_canary` and `enabled=true`. `SubmitAsync` opens HTTP only for that tenant when `Peppol:Provider=storecove` and `Peppol:Storecove:Environment=TEST` (Paket 22-b-2). Every other case stays on the reserved queue and does not open HTTP. HTTP 2xx is `Sent`, not `Ack`.

The Reserved exit is a two-step process: (1) `Peppol:ReservedExit:Enabled=true` for a canary tenant; (2) after the ACK is verified, **Paket 22-b-5** can move Peppol out of Reserved and into All. The send itself is **Paket 22-b** ([breakdown](#peppol-canary-package-breakdown-22-b-1-through-22-b-5)), not this file. The operational sequence is [Peppol canary operations](#peppol-canary-operations). This file does not turn the switch on and does not move the flag.

### Migration (additive)

`peppol_participants` (no credential column): `id`, `tenant_id` (FK `tenants`, indexed), `participant_id` varchar(255), `ap_environment` varchar(8) (`TEST` or `LIVE`), `legal_entity_id` varchar(255) null, `eidentifier_scheme` varchar(64) null, `eidentifier_value` varchar(255) null, `created_at_utc`, `updated_at_utc`. Unique (`tenant_id`, `participant_id`). The operator supplies the three identity columns. They are not derived from a VAT id.

`einvoice_submissions` outbox (no payload column): `id`, `tenant_id` (FK, indexed), `invoice_id` (FK `invoices`, indexed), `status` varchar(16) (`Queued`, `Sent`, `Ack`, `Failed`), `correlation_id` uuid indexed, `attempted_at_utc` null, `acked_at_utc` null, `failure_reason` varchar(512) null, `provider_message_id` varchar(255) null, `provider_status` varchar(64) null, `created_at_utc`. Migration `20260930055757_AddPeppolProviderMessageId` adds the two provider columns. No backfill.

`einvoice_documents` is still plan-only. This package does not add it.

While `EInvoicing.Peppol` is Reserved, a non-canary `PeppolSubmissionService.SubmitAsync` does not call an Access Point. When the provider is not `not-configured` and EN 16931 validation passed, it inserts `einvoice_submissions` with `status=Queued` and `failure_reason=peppol-reserved`. **Paket 22-b-2** is done: the canary tenant may move that row to `Sent` or `Failed` while the name is still Reserved. `Ack` is **Paket 22-b-3**. Promotion into `All` is **Paket 22-b-5**, after that ACK. The service does not read `Peppol__ApiKey` into `failure_reason`. The Storecove key stays `Peppol__Storecove__ApiKey`.

Super Admin (`system.critical`): `POST /api/admin/peppol/participants` inserts one participant row. `GET /api/admin/peppol/participants/{tenantId}` lists that ambient tenant. Another tenant id is HTTP 404. Register writes `AuditEventType.PeppolParticipantRegistered` (113). A read is not audited.

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
- OSS destination rates — **shipped** in Paket **30-d** (in-code seed; 14 countries, FI 25.5, `EL` → `GR`). Not a Peppol submission concern.  
- ViDA central reporting API  

---

## Related docs

- [`EINVOICING_EU.md`](EINVOICING_EU.md)  
- [`FISCAL_GERMANY.md`](FISCAL_GERMANY.md) / [`FISCAL_GERMANY_PROVIDER_DECISION.md`](FISCAL_GERMANY_PROVIDER_DECISION.md) — TSE ≠ e-invoice  
- [`FISCAL_SWITZERLAND_QR_PLAN.md`](FISCAL_SWITZERLAND_QR_PLAN.md)  
- [`COUNTRY_LAYER_CUTOVER.md`](COUNTRY_LAYER_CUTOVER.md)  
- Peppol BIS Billing 3.0: [docs.peppol.eu/poacc/billing/3.0/bis](https://docs.peppol.eu/poacc/billing/3.0/bis/)

---

## Access Point decision (2026-09-29)

This is not a Peppol or EN 16931 compliance claim. The code does not select a vendor. `PeppolSubmissionService.SubmitAsync` stores `not-sent` when `EInvoicing.En16931` is off or `Peppol:Provider=not-configured`. `EInvoicing.Peppol` is `FeatureFlagNames.Reserved` and is not resolved.

### Hosted Access Point

v1 stays a hosted Access Point. Regkasse does not operate one. Candidates, as a vendor shortlist only:

| Candidate | Why it is on the list |
|-----------|------------------------|
| **Storecove** | REST API for send and participant ids. Used by SaaS issuers across EU. A single HTTP client fits `HostedPeppolAccessPointClient`. |
| **Unifiedpost** | Stronger DE and Benelux B2G operations. Heavier enterprise onboarding than a first TEST canary needs. |
| **Tickstar** | OpenPeppol service provider with a Nordic center of gravity. A weaker fit for the DE-first pilot in §5. |

**Chosen for the first TEST canary: Storecove.** The product only needs an interface. Pricing is a partner quote, not a figure in this file. Switching provider later changes `Peppol:Provider` and the deployment secret, not `PaymentService`.

### Where the API key lives

The provider API key is a **deployment secret** (`Peppol__ApiKey` or a vault entry). It is not committed. It is not a column on `company_settings`. It is not a plain-text `tenant_settings` value. That matches the repo rule against secrets in git.

Per tenant, `peppol_participants` holds `tenant_id`, `participant_id`, `ap_environment` (`TEST` or `LIVE`), optional `legal_entity_id`, `eidentifier_scheme`, and `eidentifier_value`, plus `created_at_utc` and `updated_at_utc`. No credential column. Super Admin registers a row with `POST /api/admin/peppol/participants`. `participant_id` is required. The identity fields are optional on the API and are not computed from a VAT id.

### When `EInvoicing.Peppol` leaves Reserved

The flag stays Reserved until `PeppolSubmissionService.SubmitAsync` opens HTTP in **TEST** against the chosen provider and one canary tenant’s submission is ACKed. Until that ACK, do not add the name to `FeatureFlagNames.All` and do not add an appsettings default. Today the service does not read `EInvoicing.Peppol`. The send gate it does read is `EInvoicing.En16931` plus `Peppol:Provider`.

### Production-ready gate for Paket 22

[`COUNTRIES.md`](COUNTRIES.md) §16: UBL sketch, embedded Schematron, validation columns, and `EinvoiceValidated` exist. XRechnung, ZUGFeRD, Peppol send, and the KoSIT/Peppol Schematron packs do not.

| Gate | Must be true |
|------|----------------|
| Vendor | DPA and participant agreement with the hosted AP. TEST document type is BIS Billing 3.0. Production PKI stays at the AP. |
| Config | `Peppol:Provider` names the chosen AP. `Peppol:Environment=TEST` for the canary. API key only in the deployment secret. `EInvoicing.Peppol` still Reserved until the ACK in the paragraph above. `EInvoicing.XRechnung` and `EInvoicing.Zugferd` stay off. |
| Tables | `peppol_participants` is in the database and has no secret column. `einvoice_submissions` is the outbox (`Queued` / `Sent` / `Ack` / `Failed`) plus nullable `provider_message_id` and `provider_status` (`20260930055757_AddPeppolProviderMessageId`). While the flag is Reserved, a non-canary `SubmitAsync` writes `Queued` and `failure_reason=peppol-reserved` and does not open HTTP. The canary tenant in TEST may write `Sent` or `Failed`. `einvoice_documents` is still plan-only. |
| Audit | `EinvoiceValidated` (108), `EinvoiceSubmitted` (109), and `EinvoiceSubmissionFailed` (110) exist. A LIVE ACK is not evidence that those rows are written for a real network send today. |
| Tests | Flag off and `Provider=not-configured` perform no HTTP. A TEST double returns ACK for one canary. Known-good and known-bad UBL stay in `En16931XmlBuilderTests`. Full KoSIT/Peppol schematron files are still absent. |
| Scope | No own AP. No SDI, Chorus B2B, or KSeF client. |

### Rollback

Set the tenant so `EInvoicing.En16931` is off and keep `Peppol:Provider=not-configured`. `EInvoicing.Peppol` cannot be switched off in `FeatureFlagService` because it is not resolved. New calls do not open HTTP. XML and validation columns stay. A document the AP already accepted cannot be unsent from Regkasse. Keep `participant_id`. Unregister only in the provider’s portal. Canary-specific rollback is in [Peppol canary operations](#peppol-canary-operations).

---

## Peppol canary operations

This is not a Peppol or EN 16931 compliance claim. Nothing in this section enables `Peppol:ReservedExit` or moves `EInvoicing.Peppol` out of `FeatureFlagNames.Reserved`. Swiss QR-Rechnung is not this channel ([`FISCAL_SWITZERLAND_QR_PLAN.md`](FISCAL_SWITZERLAND_QR_PLAN.md)).

`Peppol:ReservedExit` is deployment config. The process reads it at startup and does not write it. `SubmitAsync` does not open HTTP when `Enabled=false`, when the tenant is not `CanaryTenantId`, or when `Peppol:Storecove:Environment` is not `TEST` (`PeppolReservedExitTests`, `PeppolCanarySubmissionTests`). The matching canary with `Provider=storecove` and `Environment=TEST` may POST. HTTP 2xx is `Sent`, not `Ack`.

### Who approves the canary

| Field | Who sets it | Rule |
|-------|-------------|------|
| `Peppol:ReservedExit:Enabled` | Ops, in the TEST deployment only | Stays `false` until the pre-conditions below are true. Production stays `false`. |
| `CanaryTenantId` | Ops, after the backend lead confirms the guid | One real tenant id in the TEST database, `Guid` format `D`. Empty or unparsable keeps every tenant off (`source=reserved_exit`, `enabled=false`). |
| `ApprovedBy` | The same Ops change | `ops/<github-login>` of the person who sets `Enabled`. Not a shared mailbox. |
| `ApprovedAtUtc` | The same Ops change | UTC timestamp of that config change. The process does not fill it. |

The backend lead confirms the tenant row and that `peppol_participants.ap_environment` for that tenant is `TEST`. There is no second approver field. Compliance review, if any, is outside this config.

### What counts as an ACK

Today there is no Storecove call. `HostedPeppolAccessPointClient` is not a Storecove client. It is also not on the path `SubmitAsync` uses while the flag is Reserved.

When **Paket 22-b** opens HTTP, evidence is fail-closed:

| Observation | Counts as ACK? |
|-------------|----------------|
| POST HTTP 2xx | No. The hosted client maps that to `Sent`, not `Ack`. |
| GET HTTP 2xx with only the status code stored | No. That mapping is generic. It is not a Storecove document id. |
| `einvoice_submissions.status=Ack`, `acked_at_utc` set, `failure_reason` null, and a provider message id recorded on that row | Yes, for the canary tenant only. |

The outbox columns include `status` (`Queued`, `Sent`, `Ack`, `Failed`), `acked_at_utc`, `failure_reason`, `correlation_id`, `provider_message_id`, and `provider_status` (migration `20260930055757_AddPeppolProviderMessageId`). 22-b-2 stores the Storecove `guid` in `provider_message_id`. `Ack` is still 22-b-3. HTTP 2xx alone is not an ACK.

Ambiguous ACK: leave `ReservedExit:Enabled=false`, leave `TestAckReceivedAtUtc` null, and do not retry by flipping the switch on. Fix the evidence, then try again.

### Promotion into `All`

Only after the ACK row above is verified. Ops does not merge this. The backend lead opens one PR that:

1. Removes `EInvoicing.Peppol` from `FeatureFlagNames.Reserved` and adds it to `FeatureFlagNames.All`.
2. Updates `PeppolReservedFlagGuardTests` (`Source_KeepsPeppolReserved`) and `PeppolReservedExitTests` (`Peppol_StaysReserved_AndOutOfAll`), which currently assert the name is still Reserved and not in `All`.
3. Appends a decision record to this file (date, canary tenant id, `einvoice_submissions.id`, `acked_at_utc`, provider message id).

No database migration. The flag name is code-only. Do not add an appsettings default that turns the flag on.

### Rollback

| Case | Action | What stays |
|------|--------|------------|
| Canary misbehaves before promotion | Set `Peppol:ReservedExit:Enabled=false`. | The `einvoice_submissions` row stays `Queued`, `Sent`, or `Failed`. Do not delete it. Later `SubmitAsync` calls do not open HTTP. |
| Promotion to `All` misbehaves | Revert the PR. The name returns to `Reserved` and leaves `All`. | Existing outbox rows stay. A document an Access Point already accepted cannot be unsent from Regkasse. |

### Pre-conditions before a canary send can start

All of these are required, and they are not met by this document:

- TEST only (`Peppol:Environment=TEST`). Production `ReservedExit:Enabled` stays false.
- `CanaryTenantId` is a real TEST tenant. `ApprovedBy` is `ops/<github-login>`. `ApprovedAtUtc` is set.
- That tenant has a `peppol_participants` row with `ap_environment=TEST`, `legal_entity_id`, `eidentifier_scheme`, and `eidentifier_value` set by the operator, and no credential column.
- `Peppol__ApiKey` is a deployment secret, not a file in git.
- **Paket 22-b-2** is done ([breakdown](#peppol-canary-package-breakdown-22-b-1-through-22-b-5)): the canary tenant’s `SubmitAsync` can open TEST HTTP and persist `provider_message_id`. `Ack` is still **22-b-3**. `Enabled=true` for any other tenant does not send.

## Peppol canary package breakdown (22-b-1 through 22-b-5)

**Paket 22-b "Peppol canary submission"** is a TEST send for one tenant. **22-b-1**, **22-b-2**, **22-b-3** (ACK poll), and **22-b-4** (TEST smoke) are done. **22-b-5** is not started. This section does not move `EInvoicing.Peppol` out of `FeatureFlagNames.Reserved` and does not claim Peppol or EN 16931 compliance.

Scope: `PeppolSubmissionService.SubmitAsync` opens HTTP to the hosted Access Point (Storecove) in **TEST** for exactly one tenant (`Peppol:ReservedExit:CanaryTenantId`).

Out of scope: `LIVE`, more than one tenant, XRechnung, ZUGFeRD, and inbound receive.

`IPeppolAccessPointClient` already exists (`PeppolAccessPoint.cs`), with `MockPeppolAccessPointClient` and `HostedPeppolAccessPointClient`. The hosted client is `Provider=hosted`. It maps POST HTTP 2xx to `Sent` and GET HTTP 2xx to `Ack`, and it stores only the status code. A non-canary `SubmitAsync` returns before that client while the flag is in `Reserved` (`failure_reason=peppol-reserved`). 22-b-2 adds nullable `provider_message_id` and `provider_status` on `einvoice_submissions`. 22-b-1 does not add a second interface and does not call Storecove from `SubmitAsync`.

| Step | PR | Opens HTTP? | Flag stays Reserved? |
|------|----|-------------|----------------------|
| 22-b-1 | Storecove client, not wired | **Done.** Unit-test handler only. `SubmitAsync` does not call it except through 22-b-2 | Yes |
| 22-b-2 | Canary `SubmitAsync` | **Done.** TEST, one tenant, `Provider=storecove`. HTTP 2xx is `Sent`, not `Ack` | Yes |
| 22-b-3 | ACK from `Sent` to `Ack` | **Done.** Poll `GET /document_submissions/{guid}` in TEST. `Peppol:AckPollInterval` defaults to 0 (no HTTP). Not a webhook | Yes |
| 22-b-4 | TEST smoke | **Done.** CI uses a mocked Access Point (`PeppolCanaryEndToEndTests`). Live Storecove is `npm run smoke:peppol-test` and stays manual | Yes |
| 22-b-5 | Promotion into `All` | No new send path | No, after a verified ACK |

### 22-b-1 — Storecove client, no wiring

**Done.** `StorecovePeppolAccessPointClient` implements `IPeppolAccessPointClient`. `PeppolAccessPointClientFactory` returns it only when `Peppol:Provider=storecove`. `not-configured` stays on `HostedPeppolAccessPointClient`, which does not open a socket. A non-canary `SubmitAsync` still returns `Queued` / `peppol-reserved` and does not call Storecove. HTTP 2xx is `Sent`, not Ack. `PeppolTransportResult.ProviderMessageId` carries the Storecove `guid`. 22-b-2 persists that value.

`Peppol:Storecove:ApiKey` is `Peppol__Storecove__ApiKey` only. It is not in appsettings. `BaseUrl` and `Environment` may be set; `LIVE` throws `PeppolTransportException` before HTTP. A document submit reads `peppol_participants` for the tenant. When `legal_entity_id`, `eidentifier_scheme`, and `eidentifier_value` are all set, the JSON sends `legalEntityId`, `routing.eIdentifiers[].scheme`, and `routing.eIdentifiers[].id` exactly as stored. If any of those is missing, the client throws `peppol-participant-not-configured` before HTTP and does not invent a scheme. The request also writes `idempotencyGuid`. The response `guid` is `PeppolTransportResult.ProviderMessageId`. 22-b-2 persists that id.

| | |
|--|--|
| Files | `backend/Services/Countries/EInvoicing/StorecovePeppolAccessPointClient.cs` (new). Optional message-id field on `PeppolTransportResult` in `PeppolAccessPoint.cs`. `ApplicationHost.cs` may register the type. `PeppolSubmissionService.cs` stays on the reserved queue. |
| Tests | Handler test: `Provider=storecove` builds the request and reads a message id from the stub body. `Provider` left at `not-configured` or `hosted` does not construct this client. A non-canary `SubmitAsync` with the flag still Reserved performs zero HTTP and still writes `Queued` / `peppol-reserved` (`PeppolReservedExitTests`). |
| Config | No new key. `Peppol:Provider=storecove` is a legal value and is not the default (`not-configured`). Reuse `Peppol:BaseUrl`, `Peppol:Environment`, and `Peppol__ApiKey`. Do not commit the key. Do not invent a second secret in this PR. |
| Rollback | Delete the client and the registration. Leave `Provider=not-configured`. No migration. No outbox rows change. |

### 22-b-2 — Canary send

**Done.** `SubmitAsync` calls the Storecove client only when all of these are true: `Peppol:ReservedExit:Enabled=true`, the tenant id equals `CanaryTenantId`, `Peppol:Provider=storecove`, and `Peppol:Storecove:Environment` is exactly `TEST`. Every other tenant keeps `Queued` / `peppol-reserved` and zero HTTP. The name stays in `Reserved`. Anything other than exact `TEST` throws `PeppolTransportException` with code `peppol-live-not-allowed` before HTTP. There is no silent LIVE fallback. `Provider` other than `storecove` writes `Failed` / `peppol-provider-not-storecove` and does not open HTTP.

Startup lock (`PeppolStorecoveOptionsValidator`, `ValidateOnStart`): `Peppol:Storecove:Environment=LIVE` together with `Peppol:ReservedExit:Enabled=true` fails process start. `LIVE` with `Enabled=false` is inert and the process starts. `TEST` and an unset environment also start. Unset is not a send path. This prompt does not turn `ReservedExit` on and does not open LIVE HTTP.

The request sends `idempotencyGuid`, `legalEntityId`, and `routing.eIdentifiers[].scheme` plus `.id` from the operator-supplied participant row. A 2xx body `guid` is stored in `provider_message_id`. A 2xx body `status` is stored in `provider_status`. The row is `Sent`, `attempted_at_utc` is set, and `failure_reason` is null. A transport error stores `Failed`, `failure_reason` = `PeppolTransportException.Code` (for HTTP errors, `storecove-http-{status}`; missing identity is `peppol-participant-not-configured`), and `attempted_at_utc` only when HTTP was attempted. This step does not write `Ack` or `acked_at_utc`. The UBL document and the API key are not written to `failure_reason` or the service log. No scheme is guessed.

Migration `20260930055757_AddPeppolProviderMessageId` adds nullable `provider_message_id` varchar(255) and `provider_status` varchar(64). Migration `20260930060830_AddPeppolParticipantIdentity` adds nullable `legal_entity_id` varchar(255), `eidentifier_scheme` varchar(64), and `eidentifier_value` varchar(255) on `peppol_participants`. No backfill. No credential column.

| | |
|--|--|
| Files | `PeppolSubmissionService.cs` (canary branch only; the reserved branch stays for everyone else). `EinvoiceSubmission.cs`. Migration `20260930055757_AddPeppolProviderMessageId`. `PeppolCanarySubmissionTests`. |
| Tests | Matching canary: one HTTP call, row `Sent`, `provider_message_id` set, UBL and API key absent from logs and `failure_reason`. Other tenant or `Enabled=false`: zero HTTP, row stays `Queued` / `peppol-reserved`. Storecove HTTP 4xx: row `Failed`, no `Ack`. `Environment=LIVE` and `Provider=not-configured`: `Failed`, zero HTTP. |
| Config | No new key. Uses `Peppol:ReservedExit:*`, `Peppol:Provider`, `Peppol:Storecove:Environment`, `Peppol:Storecove:BaseUrl`, `Peppol__Storecove__ApiKey`. Defaults stay `Enabled=false` and `Provider=not-configured`. |
| Rollback | Set `Peppol:ReservedExit:Enabled=false`. Revert the branch. Keep the outbox row (`Sent` or `Failed`). Do not delete it. The new columns may stay. A document Storecove already accepted cannot be unsent from Regkasse. |

### 22-b-3 — ACK

**Decision: polling.** A webhook needs a public URL and a signature secret on that host. This canary runs in TEST for one tenant, so a hosted poll is enough and does not expose a receiver. `PeppolAckPollingService` calls Storecove `GET /document_submissions/{guid}` only when `Peppol:AckPollInterval` is greater than 0, `ReservedExit` is enabled for that canary tenant, `Provider=storecove`, and `Peppol:Storecove:Environment` is exactly `TEST`.

Interval: `Peppol:AckPollInterval` is minutes. **Default 0 disables the poller and opens no HTTP.** A `Sent` row is eligible only after 5 minutes (`AttemptedAtUtc`, or `CreatedAtUtc` when the attempt time is null). After **24 hours** the row becomes `Failed` with `failure_reason=peppol-ack-timeout` and the poller does not call Storecove for that row.

`Ack` is written only when the body `guid` equals `provider_message_id` and `state` is `DELIVERED`. Then `acked_at_utc` is set and `failure_reason` is null. Audit `EinvoiceAckReceived` is **115**. Activity `EinvoiceAckReceived` is **262** (severity Info). HTTP 2xx with only a status code stays `Sent`. A mismatched `guid` stays `Sent`. `state=REJECTED`, `state=INVALID`, and `state=ERROR` without a transient code set `Failed` and `failure_reason=peppol-ack-error`. The response body is not logged. The job does not write `Peppol:ReservedExit`. This is not a Peppol compliance claim. `EInvoicing.Peppol` stays Reserved. No LIVE HTTP.

Transient `state=ERROR` codes are `STORE_INTERNAL`, `TIMEOUT`, and `RATE_LIMIT` (Storecove submission error codes for an internal failure, a timeout, and a rate limit). Anything else needs an operator. `Peppol:AckRetryIntervalsSeconds` is seconds, default `[300, 1800]` (5 minutes, then 30 minutes). Each scheduled retry increments `einvoice_submissions.provider_attempt_count` (migration `20260930143000_AddPeppolProviderAttemptCount`, not null, default 0) and leaves the row `Sent`. The next status read waits that interval, measured from `attempted_at_utc`. Attempt 1 and attempt 2 stay `Sent`. The next transient error is `Failed` with `failure_reason=peppol-ack-retries-exhausted`. Audit `EinvoiceSubmissionRetry` is **118**. Activity `EinvoiceSubmissionRetry` is **263** (severity Warning). The UBL document and the API key are not logged. Retries run only when `Peppol:Storecove:Environment` is exactly `TEST`.

| | |
|--|--|
| Files | `PeppolAckPollingService`. `Peppol:AckPollInterval` on `PeppolOptions` (default 0). `AuditEventType.EinvoiceAckReceived` (115). `ActivityEventType.EinvoiceAckReceived` (262). No change to `FeatureFlagNames`. |
| Tests | `state=DELIVERED` and a matching `guid` set `Ack`, `acked_at_utc`, and `failure_reason` null. `state=ERROR` sets `Failed` / `peppol-ack-error` and does not copy the body. Age over 24 hours sets `Failed` / `peppol-ack-timeout` with zero HTTP. A body that has only an HTTP status stays `Sent`. |
| Config | `Peppol:AckPollInterval` defaults to 0. No new secret. `TestAckReceivedAtUtc` stays an ops field. |
| Rollback | Set `Peppol:AckPollInterval=0`. Do not rewrite `Ack` rows and do not hand-edit a row to `Ack` so that 22-b-5 can merge. Ambiguous evidence: leave `ReservedExit:Enabled=false` and `TestAckReceivedAtUtc` null. |

### 22-b-4 — TEST smoke

**Done.** One automated path covers submit, stored message id, and ACK for the canary tenant. `PeppolCanaryEndToEndTests` uses the real `PeppolSubmissionService` and `PeppolAckPollingService` with a mocked `IPeppolAccessPointClient`. CI does not call Storecove. A `Sent` row is aged past the 5-minute poll gate in the test so `PollOnceAsync` can run. `state=DELIVERED` on the Nth status read sets `Ack`. `state=ERROR` sets `Failed` / `peppol-ack-error`. Age over 24 hours sets `Failed` / `peppol-ack-timeout` with no status call. A participant row without `legal_entity_id` sets `Failed` / `peppol-participant-not-configured` and does not open HTTP. A second tenant and any `Environment` other than exact `TEST` do not call the Access Point.

`npm run smoke:peppol-test` (`scripts/smoke/peppol-canary-test-smoke.mjs`) prints the five-step plan and does not call Storecove. Real HTTP is manual: `PEPPOL_SMOKE_ALLOW=1`, `--confirm`, and `Peppol__Storecove__ApiKey`, plus `PEPPOL_SMOKE_LEGAL_ENTITY_ID`, `PEPPOL_SMOKE_SCHEME`, and `PEPPOL_SMOKE_VALUE`. Success prints `provider_message_id` and `einvoice_submissions_status`. Failure prints `http_status` and `provider_code` only. That script is not a CI job. `LIVE` stays unsent. This is not a Peppol or EN 16931 compliance claim. `EInvoicing.Peppol` stays Reserved.

| | |
|--|--|
| Files | `backend/KasseAPI_Final.Tests/Countries/EInvoicing/PeppolCanaryEndToEndTests.cs`. `scripts/smoke/peppol-canary-test-smoke.mjs`. Country-layer filter in `.github/workflows/backend-unit-tests.yml`. |
| Tests | Canary tenant: `Sent` carries `provider_message_id`; ACK sets `acked_at_utc` and `failure_reason` null. Audit `EinvoiceSubmitted` and `EinvoiceAckReceived` (115). Second tenant: zero Access Point calls. `Environment` other than `TEST`: zero Access Point calls. |
| Config | None committed. The mock does not need `Peppol__Storecove__ApiKey`. The manual script reads that variable from the environment. |
| Rollback | Remove the test and the smoke script. No outbox delete. No flag change. |

### Operator guidance

Read-only. `GET /api/admin/peppol/submissions` lists the outbox (`tenantId`, `status`, `limit` default 50, `offset` default 0). `GET /api/admin/peppol/submissions/{id}` returns the same fields plus `invoiceHref` (`/invoices?invoiceId={invoiceId}`). Both require `system.critical`. A caller who is not Super Admin receives HTTP 404, including a Mandanten-Admin filtering by another tenant. The payload has no UBL and no API key. FA: `/admin/peppol/submissions`. This does not submit, poll, enable `EInvoicing.Peppol`, or open LIVE HTTP. It is not a Peppol or EN 16931 compliance claim.

Catalog keys use underscores because the i18n key pattern rejects hyphens. The FA maps the stored code to `peppol.submissions.failure.<key>`.

| `failure_reason` | i18n key | Hint |
|------------------|----------|------|
| `peppol-reserved` | `peppol.submissions.failure.peppol_reserved` | Peppol is Reserved. Contact ops to enable the canary. |
| `peppol-participant-not-configured` | `peppol.submissions.failure.peppol_participant_not_configured` | Missing Peppol participant identity. Fill `legal_entity_id`, `eidentifier_scheme`, and `eidentifier_value` on the tenant's Peppol participant row. |
| `peppol-live-not-allowed` | `peppol.submissions.failure.peppol_live_not_allowed` | LIVE HTTP is disabled until promotion. This is a config error. |
| `peppol-ack-timeout` | `peppol.submissions.failure.peppol_ack_timeout` | Storecove did not deliver within 24h. Retry is a manual ops action until 22-b-5. |
| `peppol-ack-error` | `peppol.submissions.failure.peppol_ack_error` | Storecove reported an error. Check the provider portal. |
| `storecove-http-400` | `peppol.submissions.failure.storecove_http_400` | HTTP error from Storecove. See provider logs. |
| `storecove-http-5xx` (`storecove-http-500` through `storecove-http-599`) | `peppol.submissions.failure.storecove_http_5xx` | HTTP error from Storecove. See provider logs. |
| `peppol-provider-not-storecove`, `storecove-transport`, any other code | `peppol.submissions.failure.unknown` | Unknown failure code. Contact ops. |

### 22-b-5 — Promotion

Only after 22-b-4 shows one canary row with `status=Ack`, `acked_at_utc` set, `failure_reason` null, and `provider_message_id` set. The backend lead’s PR:

1. Removes `EInvoicing.Peppol` from `FeatureFlagNames.Reserved` and adds it to `FeatureFlagNames.All`.
2. Updates `PeppolReservedFlagGuardTests.Source_KeepsPeppolReserved` and `PeppolReservedExitTests.Peppol_StaysReserved_AndOutOfAll`.
3. Appends a decision record here (date, canary tenant id, `einvoice_submissions.id`, `acked_at_utc`, provider message id).

No EF migration in this PR. Do not add an appsettings default that turns the flag on. Country defaults stay off until an explicit tenant override. This file does not perform that move.

The startup lock from 22-b-2 stays. `Peppol:Storecove:Environment=LIVE` with `Peppol:ReservedExit:Enabled=true` still fails startup. Promotion does not set `Environment=LIVE` and does not open LIVE HTTP. A later ops decision, after this promotion, is what may change that lock. Until then the client still accepts only exact `TEST` and throws `peppol-live-not-allowed` for every other value.

| | |
|--|--|
| Files | `FeatureFlagNames.cs`. The two tests above. This plan (decision record only). |
| Tests | The guard tests assert the name is in `All` and not in `Reserved`. A non-canary tenant without an override stays off. |
| Config | No new key. `Peppol:ReservedExit:Enabled` is not a substitute for the flag after promotion. |
| Rollback | Revert the PR. The name returns to `Reserved` and leaves `All`. Existing outbox rows stay. A document the Access Point already accepted cannot be unsent from Regkasse. |

The operational sequence, owners, and checks are [Peppol promotion runbook (22-b-5)](#peppol-promotion-runbook-22-b-5). This file does not perform that promotion.

## Peppol promotion runbook (22-b-5)

This runbook is the procedure for a later promotion. It does not move `EInvoicing.Peppol` out of `FeatureFlagNames.Reserved`, does not set `Peppol:ReservedExit:Enabled`, and does not open LIVE HTTP. It is not a Peppol or EN 16931 compliance claim. A document an Access Point already accepted cannot be unsent from Regkasse.

Do not start until every preflight row is checked and recorded (tenant id, submission id, `acked_at_utc`, provider message id, checker, UTC time). An ambiguous ACK stops the runbook: leave `ReservedExit:Enabled=false` and `TestAckReceivedAtUtc` null.

### Preflight checklist

| Condition | Verifiable check | Pass |
|-----------|------------------|------|
| Canary ACK is recorded | `GET /api/admin/peppol/submissions?tenantId={canaryTenantId}&status=Ack` (`system.critical`). Or: `SELECT id, status, acked_at_utc, failure_reason, provider_message_id FROM einvoice_submissions WHERE tenant_id = '{canaryTenantId}' AND status = 'Ack'`. | At least one row with `status=Ack`, `acked_at_utc` not null, `failure_reason` null, and `provider_message_id` not null. HTTP 2xx alone is not a pass. |
| No `Failed` rows for the canary in the last 24 hours | `SELECT id, failure_reason, attempted_at_utc, created_at_utc FROM einvoice_submissions WHERE tenant_id = '{canaryTenantId}' AND status = 'Failed' AND GREATEST(COALESCE(attempted_at_utc, created_at_utc), created_at_utc) >= (now() AT TIME ZONE 'utc') - interval '24 hours'`. FA `/admin/peppol/submissions` filtered to that tenant and `Failed` is a cross-check; the SQL is the 24-hour window. | Zero rows. |
| Storecove API key is a deployment secret | `git grep -n "Peppol__Storecove__ApiKey" -- "*.json" "*.yml" "*.md" "*.cs"` returns no secret value. Confirm the host environment has a non-empty `Peppol__Storecove__ApiKey` without printing it. `Peppol:Storecove:ApiKey` in committed appsettings stays empty. | Key present only in the secret store. |
| Storecove environment is TEST | Read the deployed value of `Peppol__Storecove__Environment` (config `Peppol:Storecove:Environment`). | The string is exactly `TEST`. `LIVE` is not a pass. `LIVE` with `ReservedExit:Enabled=true` still fails startup. |
| Canary participant identity is set | `GET /api/admin/peppol/participants/{canaryTenantId}` (`system.critical`). | One row with non-empty `legalEntityId`, `eIdentifierScheme`, and `eIdentifierValue`, and `apEnvironment=TEST`. No credential column. |
| EN 16931 flag is on for the canary | `GET /api/admin/feature-flags/EInvoicing.En16931/enabled?tenantId={canaryTenantId}` (`system.critical`). | Response `enabled` is `true`. |
| Storecove TEST portal shows delivery | In the Storecove TEST portal, open the submission whose guid equals that row's `provider_message_id`. | Portal state is delivered. Record the guid only. Do not copy the UBL or the API key into the ticket. |

### Promotion steps

| Step | Owner | Action |
|------|-------|--------|
| 1 | Backend lead | Open a PR that removes `EInvoicing.Peppol` from `FeatureFlagNames.Reserved` and adds it to `FeatureFlagNames.All`. Update `PeppolReservedFlagGuardTests.Source_KeepsPeppolReserved` and `PeppolReservedExitTests.Peppol_StaysReserved_AndOutOfAll` so they expect the name in `All` and not in `Reserved`. Append a decision record to this file: date, canary tenant id, `einvoice_submissions.id`, `acked_at_utc`, provider message id. No EF migration. No appsettings default that turns the flag on. Country defaults stay off. Do not set `Peppol:Storecove:Environment=LIVE`. |
| 2 | Ops | After that PR is merged, set `Peppol:ReservedExit:Enabled=false`. The reserved-exit switch is no longer the send gate. Leave `CanaryTenantId` in config as the record of who was first; it does not replace the feature flag. |
| 3 | Ops | Enable the flag for the canary tenant only: `PUT /api/admin/feature-flags` with `{ "name": "EInvoicing.Peppol", "enabled": true, "tenantId": "{canaryTenantId}", "clearOverride": false }` (`system.critical`). Confirm with `GET /api/admin/feature-flags/EInvoicing.Peppol/enabled?tenantId={canaryTenantId}` that `enabled` is `true`. Do not omit `tenantId` (that would be a global override). |
| 4 | Ops, after Compliance sign-off | Soak the canary for 24 hours. Green means new canary submissions reach `Ack` with `failure_reason` null, and the Failed-in-24h query stays empty. Compliance signs off after that first 24 hours. Only then enable a second tenant with the same `PUT` and a new `tenantId`, soak 24 hours, then a third tenant with another 24 hour soak. Stop the ladder on any `Failed` row. |
| 5 | Ops | After the third soak is green, update [`COUNTRIES.md`](COUNTRIES.md) §16 so Paket 22 is marked production-ready for the TEST Storecove path that this promotion actually runs. Keep the document footer: this is not a legal opinion and does not certify EN 16931, Peppol, or ViDA compliance. Do not mark LIVE as ready. `Environment` stays `TEST` until a later, separate ops decision. |

### Rollback

| Case | Owner | Action | What stays |
|------|-------|--------|------------|
| Promotion misbehaves | Backend lead | Revert the PR. `EInvoicing.Peppol` returns to `FeatureFlagNames.Reserved` and leaves `All`. The guard tests return to the Reserved expectation. | Every `einvoice_submissions` row stays. Do not delete it. A document Storecove already accepted cannot be unsent from Regkasse. |
| Canary must send again while the revert is in place | Ops | Set `Peppol:ReservedExit:Enabled=true` again for that same `CanaryTenantId` only, with `Peppol:Storecove:Environment` exactly `TEST`. | Other tenants do not open HTTP. `LIVE` stays closed. |

Clear the tenant override when reverting if the flag must not stay on in `tenant_settings`: `PUT /api/admin/feature-flags` with `{ "name": "EInvoicing.Peppol", "clearOverride": true, "tenantId": "{tenantId}" }`. Do not hand-edit an outbox row to `Ack`.

### Who owns each step

| Role | Owns |
|------|------|
| Backend lead | Step 1 (the PR) and the revert if the promotion misbehaves. |
| Ops | Flag and config toggles: Step 2 (`ReservedExit:Enabled=false`), Step 3 (canary `PUT`), Step 4 (second and third tenants, each after a 24 hour soak), Step 5 (the §16 wording, with the disclaimer), and re-enabling `ReservedExit` during a revert. |
| Compliance | Sign-off after the first 24 hours of green canary traffic, before the second tenant is enabled. |
