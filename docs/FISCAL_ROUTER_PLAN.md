# Fiscal signature router plan (Paket 71)

**Status:** PLAN ONLY — not implemented.  
**Scope:** Design a country → fiscal signing dispatch for payment/checkout.  
**Out of scope for this document:** Code changes, `PaymentService` edits, `IsEnabledAsync`, DE Payment wiring.

**Related:** [`COUNTRIES.md`](COUNTRIES.md), [`FISCAL_GERMANY.md`](FISCAL_GERMANY.md), [`FISCAL_GERMANY_PROVIDER_DECISION.md`](FISCAL_GERMANY_PROVIDER_DECISION.md), Paket 70 (AT Fiskaly host pin / `kassensichv` guard).

**Last updated:** 2026-09-24

---

## 1. Current state (source of truth)

| Layer | Today |
|-------|--------|
| Payment signing | `PaymentService` → `FiscalTseSigning.SignAsync` → `ITseService` (not `IFiskalyTseService` directly) |
| AT cloud TSE | `TseService` → `IFiskalyTseService` / `FiskalyHttpClient` → SIGN AT (`rksv.fiskaly.com`) |
| DE facade | `IKassenSicherheitService` / `FiskalyDeKassenSicherheitService` registered; **not** called from `PaymentService` |
| Country binding | `ICountryStrategyContext.LoadAsync()` → `CompanySettings` + `ICountryProfileRegistry.GetOrDefault(country)` |
| Flags | `IFeatureFlagService.IsEnabled(string featureName, string? tenantId = null)` — **synchronous** |
| AT lock | `Fiscal.RksvAt` locked ON when profile `FiscalSystem == RKSV_AT` |
| DE tax/invoice | Resolver wired (Paket 30-c); `ProjectFiscalTaxSets` / receipt allocation for DE still shape / `NotImplementedException` |

**Implication:** A “router” must sit at the **fiscal signature** boundary, not only beside tax strategy resolution. Tax already branches via `ITaxStrategyResolver`; signing does not.

---

## 2. Recommended placement

### Preferred: new `IFiscalSignatureRouter` (facade)

Introduce a thin application service that owns **only** “who signs this payment receipt for this tenant’s country/fiscal system.”

```text
PaymentService  →  IFiscalSignatureRouter.SignPaymentAsync(...)
                       ├─ AT  → existing FiscalTseSigning / ITseService path (byte-identical)
                       ├─ DE  → IKassenSicherheitService (future; not until DE readiness)
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
| `KASSENSICHERHEIT_DE` | `Fiscal.KassenSicherheitDe` **ON** | `IKassenSicherheitService` | **Not wired yet.** When ready: Start → … → Finish. Until tax/receipt allocation are real: fail closed with explicit “DE fiscal signing not ready” (not AT silent fallback) |
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

## 6. DE readiness gates (block Payment wiring)

Do **not** call `IKassenSicherheitService` from payment until all are true:

| Gate | Why |
|------|-----|
| `GermanyTaxStrategy.ProjectFiscalTaxSets` real (or DE-specific fiscal projection) | Payment signing needs consistent tax buckets |
| DE receipt / Beleg number allocation implemented | AT sequence must not be reused as DE legal numbering |
| Signature persistence model for DE defined | Do not store DE TSS sig as RKSV JWS/QR |
| `Fiscal.KassenSicherheitDe` + `Provider=fiskaly-de` + TEST host only for pilots | Isolates from AT `Fiskaly:` block (Paket 70) |
| Contract tests: DE flag off → error; AT unchanged | Regression shield |

Until then, router may exist as **AT-only + explicit DE/CH errors** (skeleton), or remain unmerged. Preferred rollout: ship interface + AT delegate first; DE branch throws `NotImplementedException` / domain “not ready” until gates pass.

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

### 7.2 DE payment (future — after readiness)

```text
PaymentService.CreatePaymentAsync
  → LoadAsync → FiscalSystem=KASSENSICHERHEIT_DE
  → IsEnabled(Fiscal.KassenSicherheitDe, tenantId)
       OFF → throw FiscalSigningNotAvailable (no AT)
       ON  → readiness gates
              fail → throw DE not ready (no AT)
              ok   → IKassenSicherheitService.StartTransactionAsync
                   → … payment persist …
                   → FinishTransactionAsync
                   → persist DE signature fields (TBD schema)
```

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
| **71-b Skeleton** | `IFiscalSignatureRouter` + AT-only implementation wrapping current `FiscalTseSigning`; `PaymentService` call sites switch to router; DE/CH throw explicit not-ready | Low if AT path byte-identical + tests |
| **71-c Gates** | Finish DE tax projection + receipt allocation + persistence design | Medium — DE-only tenants |
| **71-d Pilot** | Wire DE Start/Finish behind `Fiscal.KassenSicherheitDe` for one TEST mandant | Staging only; Production `Provider=not-configured` until pilot |
| **71-e CH** | Separate package; router gains CH branch | Later |

**Rollback**

- Feature flag: DE flag off → DE tenants error (ops disable pilot); AT unaffected.
- Code rollback: revert router wiring; restore direct `FiscalTseSigning` in `PaymentService` if needed.
- Never “rollback” DE by routing DE tenants to AT TSE.

**Config isolation (from Paket 70)**

- AT: `Fiskaly:` + `rksv.fiskaly.com` only; startup rejects `kassensichv` in `Fiskaly:BaseUrl`.
- DE: `KassenSicherheit:` + middleware host only.

---

## 9. Test design (no code yet)

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

## 11. Open decisions (need approval before coding)

1. **71-b timing:** Ship AT-only router skeleton now, or wait until DE gates (§6) are done?
2. **Error type:** Shared `FiscalSigningNotAvailableException` with `code` (`DE_FLAG_OFF`, `DE_NOT_READY`, `CH_NOT_IMPLEMENTED`) vs reuse existing TSE unavailable mapping?
3. **Offline:** DE offline queue is out of scope until DE online signing works; confirm AT offline remains on existing TSE intent / offline-order systems only.
4. **Sonderbelege:** Monatsbeleg / Nullbeleg stay AT-only (`ITseService` / RKSV controllers) — router for **payment** only unless a later package extends it.

---

## 12. Approval checklist

- [ ] Placement: `IFiscalSignatureRouter` (preferred) agreed  
- [ ] Decision matrix + AT fail-safe / DE fail-closed agreed  
- [ ] No DE Payment wiring until §6 gates  
- [ ] Sync `IsEnabled` only — no `IsEnabledAsync`  
- [ ] 71-b vs wait-for-gates sequenced  

**Do not implement until this plan is approved.**
