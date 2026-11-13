# Offline replay — structural fallback simplification

**Removal plan:** Preconditions, metrics, feature-flag rollout, and PR steps to remove structural fallback entirely → **`STRUCTURAL_FALLBACK_REMOVAL_PLAN.md`**.

**Fallback usage metric:** Prometheus `structural_fallback_resolved_total`, `structural_fallback_ambiguous_total`. Scanning logs for *"Offline resolved by structural fallback"* is an alternative.

---

## 1. Structural fallback flow (before simplification)

**When it ran:** During replay, in this order:

1. Find the row by **requested Id** → use it if found.
2. Find by **(CashRegisterId, PayloadHash)** → if found, write a dedup audit and use it.
3. **Runtime recomputed hash:** On the same register, compute the runtime canonical hash from `PayloadJson` over the last 2000 rows; take the first row whose hash matches the incoming `payloadHash` (legacy: `payload_hash` in the DB was sometimes different because of an older backfill).
4. **Structural fallback:** If still not found, take the **first matching** row among the last **150** rows via `JsonNode.DeepEquals(stored.PayloadJson, normalizedPayloadJson)`.
5. If none of the above match, create a **new** `OfflineTransaction` row.

**Condition:** Step 4 ran only when steps 2 and 3 produced no result. Structural matching was the last resort when neither the direct hash nor the recomputed hash matched.

---

## 2. Risk assessment

- **Wrong match:** More than one row in the window can match the same payload structurally (for example two different intents with the same content). `FirstOrDefault` took the **newest** (`CreatedAt` desc). If two different intents share the same JSON, replay could bind to the wrong row.
- **Debugging:** A structural resolve was not explicit in audit/log; only PAYLOAD_HASH_DEDUPLICATED was written.
- **Complexity:** The last 150 rows were fully loaded and scanned in memory with DeepEquals. Window size and “first match” did not guarantee uniqueness.

---

## 3. Flow after simplification

- **1–3:** Unchanged (requested Id → hash dedup → runtime recomputed hash).
- **4. Structural fallback (narrowed and guarded):**
  - **Kill switch:** If `OfflineReplay:AllowStructuralFallback` (config) is **false**, step 4 **does not run**. It can be turned off after legacy repair.
  - **Window:** Instead of a fixed 150, use **`StructuralPayloadFallbackLimit` from config** (default **50**); upper bound 500.
  - **Deterministic guard:** If the match count is **not exactly 1** (0 or 2+), structural fallback **does not resolve**; a new row is created. That guarantees “exactly one correct match.”
  - **Log:** On structural resolve, `Information`: *"Offline resolved by structural fallback: CashRegisterId=..., OfflineId=... (hash path did not match)."* On ambiguous match, `Debug`: *"Offline structural fallback: ambiguous match for CashRegisterId=..., N rows match; skipping."*

---

## 4. Complexity removed / reduced

| Before | After |
|--------|--------|
| Fixed 150 rows, first match wins | Limit from config (default 50, max 500); **only a unique match** is used |
| Structural always on | Can be turned off with `AllowStructuralFallback` (kill switch after legacy repair) |
| Structural usage not visible in logs | Traceable via Information/Debug logs |
| Multi-match picked “newest” (risk) | Multi-match does not resolve; a new row is created |

---

## 5. Remaining risks

- **Legacy data:** If `AllowStructuralFallback = false` is set before the old `payload_hash` backfill is finished, rows that do not match via hash or recompute but would match structurally are treated as “not found” and a **new row** is created (duplicate-intent risk). Turn the kill switch only after legacy repair is complete.
- **Correctness:** A unique structural match behaves the same as the previous “first match.” On multi-match there is no guess; a new row is opened. Correctness is not weakened; the ambiguous case takes the safer side.

---

## 6. Safe rollout strategy

1. **Default:** `AllowStructuralFallback = true`, `StructuralPayloadFallbackLimit = 50`. Current behavior is preserved, the window shrinks, and the ambiguous guard is on.
2. **Watch:** Search logs for *"Offline resolved by structural fallback"* to see how often it is used.
3. **Legacy repair:** After `payload_hash` on all relevant rows is aligned to the runtime canonical (for example a maintenance job or backfill script), structural matching may no longer be needed.
4. **Kill switch:** After you confirm structural matching is no longer needed, set `OfflineReplay:AllowStructuralFallback = false` in `appsettings.json` or the environment. Step 4 then stays fully off.

---

## 7. Changed files

| File | Change |
|------|--------|
| `backend/Options/OfflineReplayOptions.cs` (namespace `KasseAPI_Final.Configuration`) | New: `AllowStructuralFallback`, `StructuralPayloadFallbackLimit`. |
| `backend/Services/OfflineTransactionService.cs` | `IOptions<OfflineReplayOptions>`; structural only when the flag is on; limit from config; **unique-match** guard; structural resolve/ambiguous logs. |
| `backend/Program.cs` | `Configure<OfflineReplayOptions>(GetSection("OfflineReplay"))`. |
| `docs/release/OFFLINE_STRUCTURAL_FALLBACK_SIMPLIFICATION.md` | This report. |

Tests: Existing offline replay tests (including LegacyWrongPayloadHash) run on step 3 (recomputed hash). No test depends on structural matching. `IOptions` was added as an optional constructor argument, so all existing tests pass unchanged.
