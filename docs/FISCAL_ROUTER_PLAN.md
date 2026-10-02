# Fiscal signature router plan (Paket 71)

**Status:** Partially implemented — see §11 Resolved decisions and the code in `FiscalSignatureRouter`.  
**Scope:** Design a country → fiscal signing dispatch for payment/checkout.  
**Out of scope for this document:** Further code changes, `IsEnabledAsync`, and extending DE signing past the current `SignAsync` wiring.

**Related:** [`COUNTRIES.md`](COUNTRIES.md), [`FISCAL_GERMANY.md`](FISCAL_GERMANY.md), [`FISCAL_GERMANY_PROVIDER_DECISION.md`](FISCAL_GERMANY_PROVIDER_DECISION.md), Paket 70 (AT Fiskaly host pin / `kassensichv` guard).

**Last updated:** 2026-09-29

---

## 1. Current state (source of truth)

| Layer | Today |
|-------|--------|
| Payment signing | `PaymentService` calls `IFiscalSignatureRouter.SignAsync`. AT dispatch is `FiscalSignatureRouter.SignAtAsync` → `ITseService.CreateInvoiceSignatureAsync` (not `IFiskalyTseService` directly) |
| AT cloud TSE | `TseService` → `IFiskalyTseService` / `FiskalyHttpClient` → SIGN AT (`rksv.fiskaly.com`) |
| DE facade | `IKassenSicherheitService` is called from `PaymentService` via `IFiscalSignatureRouter`. The `KASSENSICHERHEIT_DE` branch calls `SignAsync`; `FiscalSignatureRouter.SignDeAsync` calls `IKassenSicherheitService.SignAsync`. Development registers `SoftKassenSicherheitService`; other environments register `FiskalyDeKassenSicherheitService` |
| Country binding | `ICountryStrategyContext.LoadAsync()` → `CompanySettings` + `ICountryProfileRegistry.GetOrDefault(country)` |
| Flags | `IFeatureFlagService.IsEnabled(string featureName, string? tenantId = null)` — **synchronous** |
| AT lock | `Fiscal.RksvAt` locked ON when profile `FiscalSystem == RKSV_AT` |
| DE tax/invoice | Resolver wired (Paket 30-c). `GermanyTaxStrategy.ProjectFiscalTaxSets` delegates to `DeTaxSetMapper`. `GermanyInvoiceStrategy.AllocateReceiptNumberAsync` uses `DeReceiptSequenceService` |

**Implication:** The router sits at the fiscal signature boundary. Tax branches via `ITaxStrategyResolver`. Signing branches via `FiscalSignatureRouter.SignAsync` (`SignAtAsync`, `SignDeAsync`, CH, `SignEuAsync`).

---

## 2. Recommended placement

### Preferred: new `IFiscalSignatureRouter` (facade)

Introduce a thin application service that owns **only** “who signs this payment receipt for this tenant’s country/fiscal system.”

```text
PaymentService  →  IFiscalSignatureRouter.SignPaymentAsync(...)
                       ├─ AT  → existing FiscalTseSigning / ITseService path (byte-identical)
                       ├─ DE  → IKassenSicherheitService.SignAsync (wired in SignDeAsync; do not extend until §6)
                       └─ CH  → NotImplementedException (until CH signing exists)
```

**Why not inline `if/else` in `CreatePaymentAsync`?**

- Payment has **multiple** `FiscalTseSigning.SignAsync` call sites (normal sale, storno/refund-adjacent, offline replay paths). A single router keeps one decision matrix.
- Keeps `PaymentService` free of DE Start/Finish transaction lifecycle details.
- Matches existing country pattern: resolvers/facades at the edge (`ITaxStrategyResolver`, `IInvoiceStrategyResolver`), not raw country strings in domain methods.

### Acceptable interim (not preferred)

A private helper inside `PaymentService` that only switches AT vs “unsupported” — still better than scattering flags, but will be deleted when DE lifecycle differs (Start → pay → Finish vs single AT receipt PUT).

### Explicit non-goal for first implementation slice

Do **not** replace `ITseService` with `IFiskalyTseService` in `PaymentService`. AT stays on `ITseService` so Soft TSE / Fake / Device modes and existing tests remain byte-identical.

---

## 3. Country resolution (safe)

1. Call `ICountryStrategyContext.LoadAsync(ct)` (already used by `PaymentService` for tax/invoice).
2. Use `binding.Profile` (`CountryProfile`), never `settings.Country == "AT"` string compares.
3. Unknown / null / legacy country → `ICountryProfileRegistry.GetOrDefault` → **Austria** (`CountryProfileCodes.Austria`, `FiscalSystem.RKSV_AT`), same as today (`UsedLegacyFallback` when settings missing).
4. Dispatch key: prefer `profile.FiscalSystem` (`RKSV_AT` / `KASSENSICHERHEIT_DE` / `MWST_CH`) over ISO code alone, so regime/profile stay aligned with seeds.

Optional consistency check (warn-only at first): if `FiscalSystem` and feature-flag country defaults disagree, log and follow `FiscalSystem` + flags as below.

---

## 4. Decision matrix

| Country profile (`FiscalSystem`) | Feature flag | Signature service | Behavior (planned) |
|----------------------------------|--------------|-------------------|--------------------|
| `RKSV_AT` (AT, incl. GetOrDefault fallback) | `Fiscal.RksvAt` (locked ON for AT) | `ITseService` via current `FiscalTseSigning` | **Today’s path, unchanged** |
| `KASSENSICHERHEIT_DE` | `Fiscal.KassenSicherheitDe` **ON** | `IKassenSicherheitService.SignAsync` | **Wired.** `SignDeAsync` calls `SignAsync` once. It does not call `StartTransactionAsync` or `FinishTransactionAsync`. Missing TSS/client id → `FiscalSigningNotAvailableException` (`DeNotConfigured`). No AT fallback |
| `KASSENSICHERHEIT_DE` | `Fiscal.KassenSicherheitDe` **OFF** | — | **Explicit error** (HTTP-friendly domain exception). Do **not** AT-fallback |
| `MWST_CH` | `Fiscal.MwstCh` ON or OFF | — | `NotImplementedException` / explicit “CH signing not implemented” until CH package exists |
| Unknown code | — | AT path via GetOrDefault | Treated as AT + locked `Fiscal.RksvAt` |

### AT fail-safe (mandatory)

- AT tenants must **never** hit a generic `else throw` that blocks sales when flags/profile are healthy.
- `Fiscal.RksvAt` remains locked for `RKSV_AT`; router must not require a separate “opt-in” for AT production.
- If country lookup fails (null settings / legacy) → **AT default**, matching `GetOrDefault`.

### DE / CH fail-closed

- DE with flag off → error (no silent AT TSE).
- DE with flag on but readiness gates fail → error naming DE (no AT).
- CH → error until implemented.

---

## 5. Flag resolution

Confirmed API (do not invent async):

```csharp
bool IsEnabled(string featureName, string? tenantId = null);
```

Canonical names (`FeatureFlagNames`):

- `Fiscal.RksvAt`
- `Fiscal.KassenSicherheitDe`
- `Fiscal.MwstCh`

Resolution order (existing `FeatureFlagService`): AT lock → tenant override → country profile default → global override → config.  
**No `IsEnabledAsync`.**

Pass `tenantId` as string (`Guid` `"D"` form) when ambient tenant is known so tenant overrides apply.

---

## 6. DE readiness gates (block extending the current wiring)

`PaymentService` already calls `IKassenSicherheitService.SignAsync` through `SignDeAsync`. Start/Finish stays off the payment path. The DE signature table is already in place (`de_tse_signatures`). The pilot-host gate below is Done.

| Gate | Status | Evidence |
|------|--------|----------|
| `GermanyTaxStrategy.ProjectFiscalTaxSets` real | **Done** | `GermanyStrategies.cs` `GermanyTaxStrategy.ProjectFiscalTaxSets` delegates to `DeTaxSetMapper.MapFromTaxDetailsJson`. |
| DE receipt / Beleg number allocation | **Done** | `DeReceiptSequenceService.FormatDeBelegNr` returns `DE-{slug}-{register}-{seq}` and writes `de_receipt_sequences`. `GermanyInvoiceStrategy.AllocateReceiptNumberAsync` and `PaymentService.AllocateCountryBelegNrAsync` call that service. They do not call `ISequenceReservationService`. |
| Signature persistence model for DE defined | **Done** | Table `de_tse_signatures`. Migration `20260929114500_AddDeTseSignaturePersistence`. `FiscalSignatureRouter.SignDeAsync` inserts the row. `payment_details.TseSignature` stays empty. |
| `Fiscal.KassenSicherheitDe` + `Provider=fiskaly-de` + TEST host only for pilots | **Done** | `KassenSicherheit:PilotMode` defaults false, so `Fiscal.KassenSicherheitDe` stays off (`CountryFeatureFlagDefaults`) and no DE signing runs. `PilotMode=true` makes `KassenSicherheitHostOptionsValidator` reject `Environment=LIVE` and any host other than `kassensichv-middleware.fiskaly.com`. The flag itself is one tenant override, not a profile default. `CountryFiscalLockEvaluator` still rejects `Provider=fake` and `AllowSimulatedTse=true` in Production/Staging. It does not inspect tenants. |
| Contract tests: DE flag off → error; AT unchanged | **Done** | `FiscalSignatureRouterAtDeTests.DeTenant_FlagOff_ThrowsDeFlagOff`. `CountryCallSiteMigrationTests.DePayment_RouterFlagOff_ReturnsDeFlagOff`. `DeTseSignaturePersistenceTests.SignAt_DoesNotWriteDeSignatureRow`. AT chain: `AtFiscalChainBaselineTests` (`CountryBaseline`). |
| DE offline signing decision | **Done** | §13. Out of scope for Paket 20. Unreachable middleware blocks the sale. No third queue. |

The intro used to say a separate DE signature schema was still blocked. That clause is removed because the persistence gate is Done. Start/Finish stays off the payment path.

**Next milestone:** DSFinV-K download and a DE audit event. The pilot host pin is Done: `KassenSicherheit:PilotMode=true` accepts only `Environment=TEST` and `kassensichv-middleware.fiskaly.com`. `Fiscal.KassenSicherheitDe` stays a single tenant override. This is not a KassenSichV sign-off.

---

## 7. Sequence diagrams (text)

### 7.1 AT payment (target = today)

```text
POS/FA
  → PaymentService.CreatePaymentAsync
      → ICountryStrategyContext.LoadAsync
      → ITaxStrategyResolver.Resolve (AT)
      → … cart / money …
      → IFiscalSignatureRouter.SignPaymentAsync
           → FiscalSystem=RKSV_AT, Fiscal.RksvAt locked
           → FiscalTseSigning.SignAsync(ITseService, …)
                → TseService → IFiskalyTseService → SIGN AT (rksv)
      → persist payment_details + receipt
```

### 7.2 DE payment (code today — not Start/Finish)

```text
PaymentService.CreatePaymentAsync
  → LoadAsync → FiscalSystem=KASSENSICHERHEIT_DE
  → IFiscalSignatureRouter.SignAsync
       → FiscalSignatureRouter.SignDeAsync
            OFF → FiscalSigningNotAvailableException (DeFlagOff)
            missing TSS/client id → FiscalSigningNotAvailableException (DeNotConfigured)
            else → IKassenSicherheitService.SignAsync
                 → insert de_tse_signatures (not payment.TseSignature)
```

Start → Finish is not in this path. RKSV DEP export does not read `de_tse_signatures`.

### 7.3 Unknown country

```text
LoadAsync → GetOrDefault(null|legacy) → Austria / RKSV_AT
  → same as §7.1
```

---

## 8. Migration / rollout plan

| Phase | Deliverable | Production risk |
|-------|-------------|-----------------|
| **71-a Docs** | This plan approved | None |
| **71-b Skeleton** | **Done.** `ApplicationHost` registers `IFiscalSignatureRouter`. `PaymentService` calls `SignAsync`. The DE branch is `SignDeAsync`, not an AT fallback |
| **71-c Gates** | **Done** for tax projection, `de_receipt_sequences`, `de_tse_signatures`, and the pilot TEST host pin (§6) |
| **71-d Pilot** | **Done** for the host pin. `KassenSicherheit:PilotMode=true` rejects `Environment=LIVE` and the SIGN DE LIVE host. Profile default stays off; exactly one tenant override turns `Fiscal.KassenSicherheitDe` on. Start/Finish is not the payment path. Not a live TSE |
| **71-e CH** | **Partial.** `FiscalSignatureRouter.SignAsync` already has an `MWST_CH` branch. Bank submit stays off (`QrRechnung:BankSubmit:Enabled`) |

**Rollback**

- Feature flag: DE flag off → DE tenants error (ops disable pilot); AT unaffected.
- Code rollback: revert router wiring; restore direct `FiscalTseSigning` in `PaymentService` if needed.
- Never “rollback” DE by routing DE tenants to AT TSE.

**Config isolation (from Paket 70)**

- AT: `Fiskaly:` + `rksv.fiskaly.com` only; startup rejects `kassensichv` in `Fiskaly:BaseUrl`.
- DE: `KassenSicherheit:` + middleware host only.

---

## 9. Test design

| Case | Expect |
|------|--------|
| AT tenant (or legacy null country) | `ITseService` / Fiskaly AT path invoked; assert no `IKassenSicherheitService` calls |
| AT + router present | Golden / contract: signature payload and call order **byte-identical** to pre-router (same Fake/Soft/Device behavior) |
| DE + `Fiscal.KassenSicherheitDe` ON + gates green (future) | `IKassenSicherheitService` Start/Finish called; no `IFiskalyTseService` |
| DE + flag OFF | Explicit error; no AT signing |
| DE + flag ON + gates red | Explicit “not ready”; no AT signing |
| CH | Explicit not implemented |
| Unknown country code | `GetOrDefault` → AT path |

Prefer unit tests with mocked `ICountryStrategyContext` + flag service + signature dependencies over full HTTP for the matrix.

---

## 10. Risks

| Risk | Mitigation |
|------|------------|
| Accidental AT signing for DE mandant | Fail-closed DE; never AT-fallback for `KASSENSICHERHEIT_DE` |
| Breaking AT sales via broad `else throw` | AT locked flag + GetOrDefault; router default branch = AT for `RKSV_AT` only |
| Mixing SIGN AT / SIGN DE hosts or keys | Keep `Fiskaly:` vs `KassenSicherheit:`; Paket 70 host guard |
| Storing DE sig in RKSV columns | Separate persistence design before 71-d |
| Multiple PaymentService sign sites miss router | Inventory all `FiscalTseSigning` call sites in 71-b |
| Soft TSE / Fake mode forgotten | AT branch must keep using `ITseService`, not raw Fiskaly only |
| Premature DE Payment wiring | Hard readiness checklist §6; docs gate in PR template |

---

## 11. Resolved decisions

### 2026-09-24

1. **71-b timing:** WAIT-FOR-GATES. Sequence: 72 → 73 → 74 → 75 → 76 → 77.
2. **Error type:** New shared `FiscalSigningNotAvailableException` with `code`
   (`DE_FLAG_OFF`, `DE_NOT_READY`, `CH_NOT_IMPLEMENTED`). AT keeps `TseUnavailableException`.
3. **Offline:** DE offline out of scope until DE online signing works.
   AT offline (legacy TSE intents + offline-orders) untouched. Router online-only.
4. **Sonderbeleg:** AT-only, unchanged. Router for payment only.

### 2026-09-29

The router is wired. `ApplicationHost` registers `IFiscalSignatureRouter`. `PaymentService` calls `SignAsync` for `KASSENSICHERHEIT_DE`, and `FiscalSignatureRouter.SignDeAsync` calls `IKassenSicherheitService.SignAsync`. Signatures go to `de_tse_signatures`; `payment_details.TseSignature` stays null for DE. DE offline signing stays out of scope (§13). Do not extend DE signing past this wiring until the §6 pilot-host gate is closed.

---

## 12. Approval checklist

- [ ] Placement: `IFiscalSignatureRouter` (preferred) agreed  
- [ ] Decision matrix + AT fail-safe / DE fail-closed agreed  
- [ ] No DE Payment wiring until §6 gates  
- [ ] Sync `IsEnabled` only — no `IsEnabledAsync`  
- [ ] 71-b vs wait-for-gates sequenced  

**Do not extend DE signing beyond the current wiring until §6 gates are met.**

---

## 13. DE offline decision (2026-09-29)

This section does not change §6. §6 still blocks Start/Finish and any further signing work until its remaining rows are true. §11 (2026-09-24) already said the router is online-only and that DE offline waits until DE online signing works. The paragraphs below fix the operational meaning of that decision. They are not a KassenSichV claim.

### 13.1 DE offline signing is out of scope for Paket 20

Paket 20 does not queue a DE sale when the SIGN DE middleware is unreachable. Do not add `de_offline_transactions`. Do not put DE intents in `offline_transactions` and do not put them in `offline_orders`. Those two tables are the Austrian pair ([`OFFLINE_SYSTEM_INDEX.md`](OFFLINE_SYSTEM_INDEX.md), [`ai/modules/offline_transactions_legacy.md`](../ai/modules/offline_transactions_legacy.md)): legacy TSE payment intents, and full order snapshots with RKSV replay. A third queue would fork replay, limits, and the admin screens without a DE TSS contract for delayed signing. The fallback is to block the sale. `SignDeAsync` already fails closed (`FiscalSigningNotAvailableException` or an HTTP failure). The POS must not keep the basket as a fiscal DE receipt and must not send it later through the Austrian replay APIs.

### 13.2 No separate DE signature chain table

The SIGN DE middleware keeps the TSS transaction chain (revision and signature counter) on its side. `signature_chain_state` stays the Austrian RKSV chain. `de_tse_signatures` is a log of what this API stored for one payment (`payment_details_id`, TSS id, transaction id, signature, algorithm, certificate serial, time). It is not a hash chain and it is not an input to the next DE signature. Paket 20 does not add a DE chain table.

### 13.3 Production-ready gate before a real tenant

`Fiscal.KassenSicherheitDe=true` for a real Production tenant is not allowed until every row below is true. [`COUNTRIES.md`](COUNTRIES.md) §16 is the status table. The country profile resolves this flag **off** (`CountryFeatureFlagDefaults`), including a DE profile.

| Gate | Must be true |
|------|----------------|
| Config | `KassenSicherheit:Provider=fiskaly-de`. Keys only from the environment. `AllowSimulatedTse=false`. Provider is not `fake` (`CountryFiscalLockEvaluator`). `PilotMode=true` pins `Environment=TEST` and `ApiBaseUrl` to `kassensichv-middleware.fiskaly.com` (`KassenSicherheitHostOptionsValidator`). Not `rksv.fiskaly.com`. `DeTssId` and `DeClientId` are set (or `KassenSicherheit__TssId` / `KassenSicherheit__ClientId`). |
| Flag | Tenant override is the only switch, and it is for exactly one tenant. Profile default off is not a go-live. `PilotMode` does not set the flag. `EInvoicing.Peppol` stays Reserved. |
| Tables | `de_receipt_sequences` and `de_tse_signatures` exist. DE rows do not fill `payment_details.tse_signature`, `prev_signature_value_used`, or `certificate_thumbprint`. No `de_offline_transactions`. |
| Signing path | `PaymentService` → `IFiscalSignatureRouter.SignAsync` → `SignDeAsync` → `IKassenSicherheitService.SignAsync`. Start/Finish stay off that path until §6 says otherwise. DSFinV-K download exists; `ExportDsfinvkAsync` is still PUT-only ([`COUNTRIES.md`](COUNTRIES.md) §16). |
| Audit | A DE sign-success and a DE sign-failure audit event exist and do not reuse RKSV DEP events. None of those DE events is in `AuditEventType` today, so this row is open. |
| Tests | Middleware down blocks the sale and writes neither `offline_transactions` nor `offline_orders`. DE payment keeps AT signature columns null and writes `de_tse_signatures`. Flag off throws `DE_FLAG_OFF`. AT CountryBaseline stays green. |
| Offline | §13.1 stays the rule. Unreachable middleware blocks the sale. |

### 13.4 Rollback

Rollback is the feature flag only: set the tenant override `Fiscal.KassenSicherheitDe=false`. New DE sales then stop in `SignDeAsync` with `DE_FLAG_OFF`. Do not delete `de_tse_signatures` rows. They are the log from §13.2, not a chain, and not an RKSV journal. Do not route the tenant to Austrian TSE to “undo” DE. AT `signature_chain_state` and `offline_transactions` / `offline_orders` stay untouched.
