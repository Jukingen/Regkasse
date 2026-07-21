# Frontend Admin — Security Audit

**Scope:** `frontend-admin/` (Next.js Admin Panel)  
**Audit date:** 2026-07-21  
**Auditor:** FA maintainers (+ automated `npm audit` / code review)  
**Next scheduled audit:** 2026-10-21 (quarterly — see §6)

This report covers dependency vulnerabilities, Dependabot coverage, and application-level review (XSS, CSRF, injection, authn/authz, sensitive data).  
It is the working backlog for FA security improvements until the next quarterly review.

---

## 1. Executive summary

| Area | Result |
| ---- | ------ |
| **Critical app bugs (XSS RCE, etc.)** | None found |
| **Highest app risks** | JWT in `localStorage` + non-HttpOnly cookie; impersonation tokens in URL fragment |
| **`npm audit` (after safe fix)** | **16** findings (10 critical / 3 high / 3 moderate) — mostly **dev-time Orval** + Next nested PostCSS; **no force-fix applied** (would break Next/Orval) |
| **Dependabot** | Enabled weekly for `/frontend-admin` (version updates). **Snyk:** not configured in-repo |
| **SQL / NoSQL injection in FA** | N/A — no DB drivers; API owns queries |

**Immediate priorities:** FA-SEC-001, FA-SEC-002, FA-SEC-004, FA-DEP-001 (Orval major upgrade plan).

---

## 2. Dependency audit (`npm audit`)

### 2.1 Commands run

```bash
cd frontend-admin
npm audit
npm audit fix          # safe fixes only — applied 2026-07-21 (1 package removed; remaining vulns need majors)
# NOT run: npm audit fix --force   # would install orval@8 (breaking) or downgrade next — rejected
```

### 2.2 Remaining vulnerabilities (post `npm audit fix`)

| ID | Package | Severity | Advisory / issue | Runtime impact on FA | Plan |
| -- | ------- | -------- | ---------------- | -------------------- | ---- |
| **FA-DEP-001** | `orval` / `@orval/core` ≤7.18 | **Critical** | [GHSA-h526-wf6g-67jv](https://github.com/advisories/GHSA-h526-wf6g-67jv) — code injection via unsanitized `x-enum-descriptions` during **codegen** | **Dev/CI only** (Orval runs at generate time, not in the browser). Risk if a malicious OpenAPI spec is fed into `npm run generate:api` | Plan Orval **8.x** upgrade in a dedicated PR (regenerate client, fix breaking changes). Until then: only trust `backend/swagger.json` from this monorepo |
| **FA-DEP-002** | `esbuild` ≤0.24.2 (via Orval) | Moderate | [GHSA-67mh-4wv8-2f99](https://github.com/advisories/GHSA-67mh-4wv8-2f99) — malicious site → **dev server** | Dev tooling only | Resolved with Orval 8 / tooling bump |
| **FA-DEP-003** | `lodash` ≤4.17.23 (via `@stoplight/spectral-functions` → IBM OpenAPI ruleset → Orval) | High | [GHSA-r5fr-rjxr-66jc](https://github.com/advisories/GHSA-r5fr-rjxr-66jc), [GHSA-f23m-r3pf-42rh](https://github.com/advisories/GHSA-f23m-r3pf-42rh) | Transitive **codegen** path; FA app code should not call `_.template` on untrusted input | Follow Orval upgrade; optionally `overrides` for lodash ≥4.17.24 after smoke-testing generate |
| **FA-DEP-004** | `postcss` &lt;8.5.10 (nested under `next`) | Moderate | [GHSA-qx2v-qp2m-jg93](https://github.com/advisories/GHSA-qx2v-qp2m-jg93) | Next-bundled PostCSS; XSS in CSS stringify is limited build/tooling surface | Wait for Next.js patch release that bumps nested PostCSS; **do not** `audit fix --force` (npm suggests Next 9.x — invalid) |

### 2.3 Why `--force` was refused

| Suggested force action | Why rejected |
| ---------------------- | ------------ |
| Install `orval@8.22.0` | Major breaking change; needs deliberate migration + `generate:api` + contract tests |
| “Fix” PostCSS via Next downgrade | npm suggests `next@9.3.3` — would destroy the App Router stack |

---

## 3. Dependabot / Snyk

| Tool | Status |
| ---- | ------ |
| **Dependabot version updates** | ✅ [`.github/dependabot.yml`](../.github/dependabot.yml) — weekly Saturday for `/frontend-admin` (grouped minor/patch; **ignores Orval major**) |
| **Dependabot security alerts / PRs** | Enable/confirm in GitHub → **Settings → Code security → Dependabot** (alerts + security updates). Not fully expressible in `dependabot.yml` alone |
| **Snyk** | ❌ Not present in-repo. Optional: add Snyk GitHub app or `snyk test` in CI later — not required if Dependabot + quarterly `npm audit` are followed |

**Recommendation:** Keep Dependabot; add a quarterly checklist item to open GitHub **Dependabot alerts** for `frontend-admin` and triage High/Critical within 14 days.

---

## 4. Application security findings

Severity: Critical · High · Medium · Low · Info  
Status: Open · Planned · Mitigated · Accepted

### 4.1 Findings table

| ID | Sev | Category | Title | Status | Evidence |
| -- | --- | -------- | ----- | ------ | -------- |
| FA-SEC-001 | **High** | Auth / Exposure | JWTs in `localStorage` + JS-readable cookie (no HttpOnly) | Open | `src/features/auth/services/authStorage.ts` |
| FA-SEC-002 | **High** | Auth / Exposure | Impersonation handoff puts tokens in URL `#fragment` | Open | `impersonationHandoff.ts`, `ImpersonateCallback.tsx` |
| FA-SEC-003 | Medium | Auth | Edge `proxy.ts` checks JWT shape/`exp` only (no signature) | Accepted* | `src/proxy.ts` (+ tests forge unsigned JWTs) |
| FA-SEC-004 | Medium | Auth | Edge protect-list incomplete — many routes fail-open at proxy | Open | `PROTECTED_PREFIXES` omit `/backup`, `/payments`, `/products`, … |
| FA-SEC-005 | Medium | Config | CSP allows `'unsafe-inline'` / `'unsafe-eval'` in `script-src` | Open | `next.config.mjs` |
| FA-SEC-006 | Medium | CSRF | Axios continues mutations if CSRF bootstrap fails | Open | `src/lib/axios.ts` |
| FA-SEC-007 | Medium | Injection | `window.open(downloadLink)` without origin allowlist | Open | `DataRightsRequestPanel.tsx` |
| FA-SEC-008 | Medium | Exposure | Raw `error.message` in some list UIs | Open | receipts / audit-logs / tables / ManagerSettings |
| FA-SEC-009 | Medium | Exposure | `technicalConsole` redaction gap vs tests | Open | `src/shared/dev/technicalConsole.ts` |
| FA-SEC-010 | Medium | Auth | Client SuperAdmin short-circuits `hasPermission` | Accepted* | `usePermissions.ts` (API + `PermissionRouteGuard` still enforce) |
| FA-SEC-011 | Low | Auth | 2FA Dev bypass UI driven by API `isDevelopment` | Open | `TwoFactorAuth.tsx` |
| FA-SEC-012 | Low | CSRF | CSRF mirror cookie missing `Secure` on HTTPS | Open | `src/services/cookieService.ts` |
| FA-SEC-013 | Low | Exposure | Form drafts keep PII in `localStorage` | Open | `useAutoSave` / CustomerForm (passwords stripped on tenant wizard ✅) |
| FA-SEC-014 | Low | Config | Some `window.open` without `noopener` | Open | e.g. invoice print |
| FA-SEC-015 | Info | XSS | `dangerouslySetInnerHTML` / print `document.write` — mitigated with escape/sandbox | Mitigated | Theme bootstrap, AntdRegistry, print exporters, website preview iframe |
| FA-SEC-016 | Info | Injection | No FA SQL/NoSQL drivers | Mitigated | N/A |

\*Accepted = documented defense-in-depth limitation; API remains source of truth. Revisit if SSR starts trusting the edge JWT.

### 4.2 Category notes

#### XSS
- No Critical unescaped HTML sinks found.
- Print/PDF exporters use `escapeHtml`; website preview uses sandboxed iframe (`allow-scripts` without `allow-same-origin`).
- Residual risk tied to CSP looseness (**FA-SEC-005**) and any future unsanitized HTML.

#### CSRF
- Double-submit design present: `CsrfTokenBootstrap`, axios `X-XSRF-TOKEN` / `X-CSRF-COOKIE`, aligned with backend `CsrfMiddleware`.
- Gaps: soft-fail bootstrap (**FA-SEC-006**), CSRF cookie `Secure` (**FA-SEC-012**).
- API auth is primarily Bearer from storage (not cookie-to-API host), which limits classic cross-site cookie CSRF to the API; CSRF still required when middleware is enabled.

#### SQL / NoSQL injection
- Not applicable in FA. Keep all data access on `/api/admin/*` and generated clients.

#### Authentication / authorization
- Client: `AuthGate` + `PermissionRouteGuard` (fail-closed; empty permissions denied in production).
- Edge: incomplete protect list + unsigned JWT parse (**FA-SEC-003/004**).
- Token theft surface dominates (**FA-SEC-001/002**).

#### Sensitive data exposure
- Sentry: `sendDefaultPii: false` + filters — good.
- `global-error.tsx` does not render stacks — good.
- Gaps: raw API errors in UI, console redaction, draft PII, impersonation fragment.

---

## 5. Improvement plan (prioritized)

### P0 — within 1–2 sprints

| ID | Action | Effort | Owner hint |
| -- | ------ | ------ | ---------- |
| FA-SEC-004 | Expand `proxy.ts` to fail-closed for all non-public App Router routes (or invert to allowlist-only public paths). Align `proxy.test.ts` | Medium | FA |
| FA-SEC-006 | Fail mutating axios requests if CSRF token cannot be obtained; clear operator toast | Small | FA |
| FA-SEC-007 | Allowlist download URL origins before `window.open` | Small | FA |
| FA-SEC-012 | Set `Secure` on CSRF cookie when `https:` | Small | FA |
| FA-SEC-009 | Restore log redaction on `technicalConsole`; gate warn in production | Small | FA |

### P1 — next quarter (design + implement)

| ID | Action | Effort | Owner hint |
| -- | ------ | ------ | ---------- |
| FA-SEC-001 | Migrate session toward **HttpOnly Secure** cookies (BFF or API-issued); shrink/remove `localStorage` tokens | Large | FA + Backend |
| FA-SEC-002 | Replace fragment handoff with one-time code / same-origin session swap; never put refresh token in URL | Large | FA + Backend |
| FA-DEP-001 | Upgrade Orval to 8.x; regenerate clients; run `verify-api-client` + contract tests | Large | FA |
| FA-SEC-005 | CSP: nonces for theme bootstrap; remove `'unsafe-eval'` if build allows | Medium | FA |
| FA-SEC-008 | Route remaining raw errors through `translateApiError` / `getUserFacingApiErrorMessage` | Medium | FA |

### P2 — opportunistic / polish

| ID | Action | Effort |
| -- | ------ | ------ |
| FA-SEC-011 | Ignore API `isDevelopment` for 2FA bypass UI in production builds | Small |
| FA-SEC-013 | TTL + clear drafts on logout; inventory which forms draft PII | Medium |
| FA-SEC-014 | Standardize `noopener,noreferrer` on `window.open` | Small |
| FA-DEP-004 | Track Next.js release notes for nested PostCSS ≥8.5.10 | Small |
| FA-SEC-003 / 010 | Keep Accepted; document in AGENTS/README that edge JWT is not cryptographic auth | Small |

### Dependency track (parallel)

1. Weekly: merge Dependabot PRs for FA after CI green.  
2. Quarterly: re-run `npm audit`; triage High/Critical within **14 days**.  
3. Dedicated epic: Orval 8 (clears FA-DEP-001/002/003 cluster).  
4. Optional: enable GitHub Dependabot **security** updates; evaluate Snyk only if org requires a second scanner.

---

## 6. Regular audit schedule

| Cadence | Activity |
| ------- | -------- |
| **Weekly** | Review/merge Dependabot PRs for `frontend-admin` |
| **Monthly** | Spot-check Sentry security-related issues; confirm CSRF enabled in production API config |
| **Quarterly** | Full repeat of this audit: `npm audit`, Dependabot alert triage, code review of auth/XSS/CSP deltas, update this file’s date + findings table |
| **After major upgrades** | Next / React / Ant Design / Orval / auth changes → targeted security re-test |

**Calendar:** next full audit **2026-10-21**, then +3 months.  
Add a recurring invite: **“FA Security Audit (quarterly)”**.

### Quarterly checklist

- [ ] `cd frontend-admin && npm audit` (and safe `npm audit fix`)
- [ ] GitHub Dependabot alerts for `frontend-admin` triaged
- [ ] Diff auth (`authStorage`, `proxy.ts`, CSRF, impersonation) since last audit
- [ ] Grep `dangerouslySetInnerHTML`, `innerHTML`, `eval(`, `document.write`
- [ ] Confirm no new secrets in repo / `.env.example` only
- [ ] Update §4 table statuses; bump “Audit date” / “Next scheduled audit”
- [ ] File or close tickets for P0/P1 items

---

## 7. Existing controls (keep)

- Security headers: CSP, `X-Frame-Options: DENY`, `nosniff`, Referrer-Policy, Permissions-Policy, HSTS (prod) — `next.config.mjs`
- CSRF double-submit client path + bootstrap
- `AuthGate` + `PermissionRouteGuard` fail-closed RBAC
- Sentry `sendDefaultPii: false` + event filtering
- Create-tenant draft strips passwords
- Sandboxed website preview iframe
- `.env.local` gitignored; production `NEXT_PUBLIC_*` build-time discipline

---

## 8. Related docs

| Doc | Role |
| --- | ---- |
| [`README.md`](README.md) | Setup, troubleshooting |
| [`TECHNICAL_DEBT.md`](TECHNICAL_DEBT.md) | Non-security debt (link overlapping items if needed) |
| [`docs/PERFORMANCE_MONITORING.md`](docs/PERFORMANCE_MONITORING.md) | Observability (not a substitute for security monitoring) |
| [`AGENTS.md`](../AGENTS.md) | CSRF / auth / tenant isolation rules |
| [`.github/dependabot.yml`](../.github/dependabot.yml) | Automated dependency PRs |

---

## 9. Changelog

| Date | Change |
| ---- | ------ |
| 2026-07-21 | Initial audit: `npm audit` + `npm audit fix` (safe), code review, Dependabot note, prioritized plan, quarterly schedule. |
