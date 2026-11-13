# Structural fallback — removal analysis and plan

> **Status:** Verification required before implementation. Treat as release/plan document, not current runtime behavior unless validated.

**Goal:** Reduce complexity. Analyze whether structural fallback can be removed entirely, including removal preconditions, feature-flag rollout, and a PR plan.

---

## 1. Current-state summary

- **Structural fallback:** During replay, if the hash path (direct + recomputed) does not find an offline row, the service scans the last N rows (default 50, max 500) for a **structural JSON match**. If exactly one row matches, that row is used. Zero or two-or-more matches do not resolve.
- **Location:** `OfflineTransactionService.TryResolveOfflineByStructuralPayloadAsync`, called only when `OfflineReplay:AllowStructuralFallback = true`.
- **Kill switch:** `AllowStructuralFallback = false` disables step 4 entirely. The code path still exists; it is just not entered.

**Removal question:** Can this code path (method + call site + config fields) be deleted entirely? **Yes**, after the preconditions are met.

---

## 2. Fallback usage metric

### 2.1 Counter (implemented)

- **Prometheus:**
  - `structural_fallback_resolved_total`: Replay count where the hash path did not match and structural fallback found **exactly one** match and resolved it.
  - `structural_fallback_ambiguous_total`: Count of structural fallback scans that found **more than one** match and skipped resolve.
- **Usage:** In Grafana, watch `rate(structural_fallback_resolved_total[1h])` or `increase(structural_fallback_resolved_total[7d])`. A long stretch at 0 means the fallback is effectively unused.
- **Code:** `ICoreMetrics.RecordStructuralFallbackResolved` / `RecordStructuralFallbackAmbiguous`, called from `OfflineTransactionService.TryResolveOfflineByStructuralPayloadAsync`.

### 2.2 Log scan (alternative)

- **Search text:** `"Offline resolved by structural fallback"` (Information).
- **Ambiguous:** `"Offline structural fallback: ambiguous match"` (Debug).
- **Tool:** Existing log aggregation (for example Loki, ELK). Use this when counters are unavailable or for historical periods.

**Recommendation:** Counters already collect the metric. Before removal, confirm for at least 1–2 weeks (preferably 2–4 weeks) that `structural_fallback_resolved_total` growth is near 0.

---

## 3. Removal preconditions

### 3.1 Mismatch rate must be low

- **Meaning:** The share of rows where `payload_hash` does not match the runtime canonical hash must be low, so replay usually resolves via the hash path (direct or recomputed) and does not need structural matching.
- **Measurement:**
  - **API:** `POST /api/admin/offline-payload-hash/analyze` (with sample size) → `MismatchRatioPercent`, `LegacyDataQualityRiskHigh`.
  - **Export / risk:** `GET /api/admin/offline-payload-hash/risk` → mismatch ratio and risk flag.
  - **Guard:** `PayloadHashGuard:MismatchWarningThresholdPercent` (default 10). Staying under this threshold is a reasonable “low” target.
- **Condition (recommended):**
  1. `MismatchRatioPercent` target: **&lt; 5%** (preferably near 1% or 0).
  2. If needed, **repair:** `POST /api/admin/offline-payload-hash/repair` (dry-run first, then live) to align mismatched rows, then analyze again.
  3. Lazy repair (align during replay) already exists. After repair, most new replays resolve via hash.

### 3.2 Fallback usage must be negligible

- **Metric:** Growth of `structural_fallback_resolved_total` (for example last 2–4 weeks) **near 0**, or very low relative to total replay (for example &lt; 0.1%).
- **Log:** Production should show “Offline resolved by structural fallback” rarely or never.

When both conditions hold, turning off structural fallback and then removing it is safe.

---

## 4. Feature-flag rollout plan

Existing flag: **`OfflineReplay:AllowStructuralFallback`** (already present).

| Stage | Action | Verification |
|-------|--------|--------------|
| **0. Metrics** | Counters on (`structural_fallback_resolved_total`, `structural_fallback_ambiguous_total`). | Metric visible in Grafana; collect data for a period. |
| **1. Lower mismatch** | Analyze → repair (if needed) → analyze again. MismatchRatioPercent &lt; 5% (preferably ~0). | Risk endpoint and analyze response. |
| **2. Watch** | Watch replay and fallback metrics in production for at least 2–4 weeks. | `structural_fallback_resolved_total` growth ~0. |
| **3. Turn flag off** | Set `AllowStructuralFallback: false` in production (one cash register/environment first, then all). | Replay success rate and error logs must not change; fallback metrics must not grow. |
| **4. Stay off** | Flag false in all environments. If a release passes with no issues, proceed to the removal PR. | No incident; replay behavior unchanged. |
| **5. Remove code** | Apply the “Fallback removal PR” below. | Tests green; no regression. |

**Rollback:** Setting the flag back to `true` re-enables structural fallback. After the code is removed, rollback requires restoring code (that is why stage 4 matters).

---

## 5. Fallback removal PR (code removal)

### 5.1 What to remove

| Location | Change |
|----------|--------|
| `OfflineTransactionService.cs` | Delete `TryResolveOfflineByStructuralPayloadAsync` **entirely**. |
| `OfflineTransactionService.cs` | After hash/recomputed: remove the “4) structural fallback” block: `if (offline == null && _replayOptions.AllowStructuralFallback)` and the `TryResolveOfflineByStructuralPayloadAsync` call plus the dedup audit block. |
| `OfflineReplayOptions.cs` | Remove `AllowStructuralFallback` and `StructuralPayloadFallbackLimit`. |
| `appsettings.json` | If those two settings exist under `OfflineReplay`, **remove them** (keep other OfflineReplay settings). |

### 5.2 Metrics (optional)

- **Option A:** Structural fallback is gone, so `RecordStructuralFallbackResolved` / `RecordStructuralFallbackAmbiguous` calls go away. **Removing the counters from Prometheus** is optional (old data remains; no new growth).
- **Option B:** Also **delete** the counters and interface methods for a full cleanup.

PR description: “Structural fallback removal; preconditions (low mismatch rate, zero fallback usage) verified; AllowStructuralFallback no longer used.”

### 5.3 Documentation updates

- `OFFLINE_STRUCTURAL_FALLBACK_SIMPLIFICATION.md`: Note that “Structural fallback has been removed”; keep as historical reference only.
- `LEGACY_PAYLOAD_HASH_MISMATCH.md`: Replace “Disabling structural fallback” with “Structural fallback has been removed”.
- `TECH_REVIEW_BACKLOG.md`: Mark the P2.1 row as “Removed” or “Done (removed)”.

### 5.4 Tests

- Existing replay tests rely on the hash/recomputed path; there is no structural-specific test. **Regression:** Run all offline replay tests; they must pass.
- Optional: Add an integration test that `AllowStructuralFallback = false` still yields the same result for “create new row” or “recomputed match” scenarios (recompute-path tests already exist).

---

## 6. Summary

| Question | Answer |
|----------|--------|
| Can structural fallback be removed entirely? | **Yes**, after the mismatch rate is low and fallback usage is negligible. |
| Fallback usage metric | **Counter:** `structural_fallback_resolved_total`, `structural_fallback_ambiguous_total` (Prometheus). **Alternative:** Scan logs for “Offline resolved by structural fallback”. |
| Removal preconditions | Low mismatch rate (analyze/repair, target &lt; 5%); fallback metric growth ~0 (2–4 weeks). |
| Feature-flag rollout | Existing `AllowStructuralFallback`; first metrics + lower mismatch → watch → flag false → stay off → removal PR. |
| Fallback removal PR | Delete `TryResolveOfflineByStructuralPayloadAsync` and its call site; remove the two properties from options; update config and docs. |

This plan reduces complexity and leaves a single resolve path (hash + recompute).
