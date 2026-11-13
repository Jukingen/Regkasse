# Cache optimization (Staging)

**Date:** 2026-08-09  
**Scope:** Configuration only (`CacheSettings` + cache logging). No application code changes.  
**Template:** [`backend/appsettings.Staging.example.json`](../backend/appsettings.Staging.example.json) (deployed host uses `appsettings.Staging.json` / env overrides).  
**Related:** [`backend/CONFIGURATION.md`](../backend/CONFIGURATION.md) § Cache Settings · [`AGENTS.md`](../AGENTS.md) § Caching (Backend)

---

## 1. Observation window (≈ 1 week)

| Setting | Value | Why |
|---------|-------|-----|
| `Logging:LogLevel:KasseAPI_Final.Services.Caching` | **`Debug`** | Hit/miss lines are emitted at **Debug** (`RedisCacheService` / memory path). A category level of `Information` alone **will not** show hit/miss. |
| Duration | ~7 days on Staging | Enough traffic for license / product / permission patterns under Demo & QA load |
| After analysis | Set Caching back to **`Warning`** | Avoid log volume in steady-state Staging |

**How to sample hit/miss (ops):**

```text
# Structured fields from RedisCacheService LogOp
Cache op=get key=license_status_… hit=True|False fallback=…
Cache op=get key=product_list_… hit=True|False
Cache op=get key=user_permissions_… hit=True|False
Cache op=get key=tenant_settings_… hit=True|False
```

Group by key **prefix** (`license_status_`, `product_list_`, `user_permissions_`, `tenant_settings_`).  
Hit rate ≈ `count(hit=True) / count(op=get)` per prefix. Ignore `health_check_ping`.

---

## 2. Live Staging log review (fill after observation week)

| Cache type | Key prefix | Hits | Misses | Approx hit rate | Notes (paste from Staging) |
|------------|------------|------|--------|-----------------|----------------------------|
| License status | `license_status_` | _TBD_ | _TBD_ | _TBD_ | |
| Product list | `product_list_` | _TBD_ | _TBD_ | _TBD_ | |
| User permissions | `user_permissions_` | _TBD_ | _TBD_ | _TBD_ | |
| Tenant settings | `tenant_settings_` | _TBD_ | _TBD_ | _TBD_ | |

Until the table is filled from real Staging logs, TTL decisions below are **provisional** (domain + invalidation design), not measured hit rates.

---

## 3. Decisions (applied to Staging example)

| Domain | Previous Staging TTL | New Staging TTL | Decision |
|--------|----------------------|-----------------|----------|
| License (`LicenseCacheMinutes`) | 5 | **5** (unchanged) | Keep short: SaaS gate freshness; miss after sale/activate is corrected by event invalidation. Longer TTL would hide stale “no license” only until write invalidation — still prefer short safety net. |
| Products (`ProductCacheMinutes`) | 20 | **30** | Expect **high hit** on FA/POS list reads; writes already call `InvalidateProductsCacheAsync`. Slightly longer TTL raises hit rate without hurting freshness after mutations. |
| Permissions (`PermissionCacheMinutes`) | 45 | **60** (1 hour) | Expect **high hit** on authz paths; role changes invalidate `user_permissions_{userId}` (event-based). Safe to extend toward 1h as requested. |
| Tenant settings (`TenantSettingsCacheMinutes`) | 60 | **90** | Low write frequency; if Staging logs later show **very low hit rate** and rare reads, consider dropping cache use for this key later (code change — out of scope). For now keep caching with a longer TTL to reduce repeated DB reads when the path is used. |

### High miss rate → is caching worth it?

| Signal | Action |
|--------|--------|
| Hit rate consistently &lt; ~20% and traffic is low | Prefer shorter TTL or stop caching that key in a later change (not done here). |
| Hit rate high + event invalidation on writes | Prefer longer TTL (permissions, products). |
| Freshness-critical overlay (license SaaS) | Keep short TTL even if hit rate is moderate. |

---

## 4. Staging `CacheSettings` (post-adjustment)

```json
"CacheSettings": {
  "LicenseCacheMinutes": 5,
  "ProductCacheMinutes": 30,
  "PermissionCacheMinutes": 60,
  "TenantSettingsCacheMinutes": 90
}
```

Env overrides (same values): `CacheSettings__ProductCacheMinutes=30`, etc.

**Do not copy these TTLs to Production blindly.** Promote only after the observation table is filled and Staging Demo & QA confirms no stale license/product UX.

---

## 5. Follow-ups (ops checklist)

- [ ] Deploy Staging with Caching=`Debug` + new TTLs  
- [ ] After ~7 days, fill §2 hit/miss table from logs  
- [ ] Confirm license status after create/activate still refreshes without manual cache clear  
- [ ] Set `KasseAPI_Final.Services.Caching` back to `Warning`  
- [ ] If hit rates disagree with §3, adjust TTLs again in the Staging example and note the revision here  
- [ ] Only then consider aligning Production `CacheSettings` (separate change)

---

## 6. Revision history

| Date | Change |
|------|--------|
| 2026-08-09 | Initial analysis doc; Staging example: Logging Caching=`Debug` (observation), TTLs 5 / 30 / 60 / 90 |
