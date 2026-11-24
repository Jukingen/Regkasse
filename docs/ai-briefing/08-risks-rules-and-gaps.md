# 08 — High-Risk Areas, Hard Rules, Known Gaps and Conflicts

> Part 8 of the Regkasse AI briefing. Index: [`../AI_PROJECT_BRIEFING.md`](../AI_PROJECT_BRIEFING.md).
> Verified at commit `e2be4d5b` (2026-11-24). Read this part before proposing any change to payments, fiscal code, auth, tenancy, backup, or the database.

## 1. High-risk areas ("do not touch without explicit scope")

Source: `ai/07_DO_NOT_TOUCH.md`, `.cursor/rules/000-core.mdc`, `.cursor/rules/104-fiscal.mdc`.

| # | Area | Why it matters | Main code locations |
|---|---|---|---|
| 1 | Cart → Payment → Receipt → DailyClosing | Financial and legal impact; atomic fiscal transaction | `Services/Payment*`, `PaymentService.CreatePaymentCore*`, `CartController`, `PaymentController`, `PosDailyClosingService`, `TagesabschlussController` |
| 2 | TSE and the signature chain | Chain continuity and receipt numbers are fiscal evidence | `Services/Tse*`, `Tse/`, `Fiscal/`, `ReceiptSequenceService`, `signature_chain_state`, `BelegdatenPayload*`, `SignaturePipeline` |
| 3 | RKSV special-receipt lifecycle | Uniqueness and register state rules are strict | `RksvSpecialReceiptService`, `Rksv*Policy`, `RksvSpecialReceiptsController` |
| 4 | Decommissioned register guard | A decommissioned register must never accept sessions or payments | Register readiness/commit validation, `CashRegister*` |
| 5 | Voucher ledger and balance | No plaintext codes, balance never below zero, auditable ledger | `PaymentVoucherLedgerService`, `Vouchers/`, `VoucherLedgerEntry` |
| 6 | FinanzOnline outbox | Mapping, retry taxonomy, and reconciliation semantics; audit trail | `Services/FinanzOnlineIntegration/`, outbox controllers |
| 7 | Authorization / RBAC | Permission names and the matrix are contracts | `Authorization/`, `RolePermissionMatrix`, FA `shared/auth/*` |
| 8 | Money precision and rounding | Existing rounding behavior is relied on by fiscal output | `CartMoneyHelper`, tax strategies |
| 9 | Tenant isolation | Query filters, accessor flow, 404 semantics, middleware order | `AppDbContext`, `Tenancy/`, `Middleware/Tenant*` |
| 10 | Offline systems | Two separate systems must not be merged | `offline_transactions` vs `offline_orders` code and UIs |
| 11 | Backup / restore | Tenant vs System strategies, no production restore via API | `Services/Backup/`, `Services/RestoreVerification/` |
| 12 | Working hours and the digital storefront | Working hours never close POS/FA; storefront is not fiscal | `OnlineOrderIntakeService`, `frontend-sites/` |
| 13 | Country / VAT / e-invoicing layer | Strategy selection and VAT-ID patterns are test-gated | `Services/Countries/`, `CountryLayerBypassGateTests` |
| 14 | Singleton + EF pattern | Wrong scoping crashes at runtime | `LicenseService`, all hosted services |

If you are unsure, narrow the scope, list risks and unknowns explicitly, and ask for plan approval first.

## 2. Hard rules (checklist)

**Never:**

- Bypass TSE signing with a client flag. Skip the signature chain. Change receipt numbering casually. Create a separate sequence for special receipts.
- Allow payment on a Decommissioned register. Implement Schlussbeleg as a daily closing. Loosen `CreatePaymentRequest` to accept arbitrary zero-amount normal payments.
- Log, cache, or persist raw voucher codes, passwords, TSE secrets, or full card numbers. Queue voucher payments offline.
- Use `IgnoreQueryFilters()` outside deliberate Super Admin or migration paths. Return 403 where cross-tenant 404 is required. Reorder tenant middleware. Create `AppDbContext` from the root provider inside a singleton. Add another runtime `AppDbContext` constructor.
- Cross API boundaries (POS ↔ Admin). Reintroduce `/api/Payment|Cart|Product`.
- Edit a committed migration. Delete a column directly. Mix backfill and schema change in one migration. Add a `CountryCode` column.
- Hardcode country codes, VAT rates, or VAT-ID regexes outside `CountryProfile` seeds and `VatIdPatterns`.
- Enable `Fiscal.KassenSicherheitDe` or `QrRechnung:BankSubmit` for a real tenant before `docs/COUNTRIES.md` §16 says production-ready.
- Use static `message` / `notification` / `Modal.confirm` from `antd` in FA. Hardcode user-facing strings. Translate POS UI text into English or Turkish.
- Commit secrets. Push directly to `main`/`master`. Merge with `--admin`. Run `npm audit fix --force`.
- Claim legal compliance guarantees. Invent BMF/FinanzOnline behavior. Describe the RKSV submission client as production-complete.
- Merge `offline_transactions` and `offline_orders`. Wire working hours into POS/FA gating. Wire the storefront into the fiscal chain.
- Loosen backup rules: no System dumps for Manager, no production restore via API, no cross-tenant restore.

**Always:**

- Smallest safe change, nearest existing pattern, backward compatibility, no unrelated refactors.
- Plan first for medium or large work and for any high-risk area.
- Tests for new features and high-risk changes. Regenerate and verify the API client after API changes.
- Additive migrations. Audit events for sensitive operations. i18n for UI text.
- State uncertainty explicitly. Prefer repository evidence over documentation.

## 3. Known gaps and limitations (documented honestly)

- **FinanzOnline:** the session SOAP path works with credentials, but the RKSV submission client is a guarded skeleton (Fake/Disabled by default in examples). Manual BMF Belegcheck stays an operational step. Real BMF integration needs official credentials and documentation.
- **Fiscal export** (`/api/admin/fiscal-export`) is diagnostic only, not DEP and not legal proof. DEP §7 export is implemented and verified with the BMF Prüftool.
- **Early Jahresbeleg:** sales are not hard-blocked after an early Jahresbeleg (treated as a legal/process responsibility). Duplicate creation is blocked.
- **Voucher refunds:** partial refund balance restoration may need more work.
- **Multi-country:** Germany is not production-ready (no DSFinV-K download, no DE offline, DE BelegNr is not an Austrian RKSV Beleg). Switzerland: bank submit and some QR known gaps blocked. EU: XRechnung, ZUGFeRD, Peppol promotion, Storecove onboarding blocked. KoSIT/CII not done. Details in `docs/COUNTRIES.md` §16.
- **JWT vs host:** strict validation that the JWT tenant matches the host in production is not enforced (acceptable on reserved shared hosts).
- **Impersonation:** `impersonated_by` is not stored on `AuditLog`. Production handoff to a mandant host is not fully implemented (current FA stores the token and reloads the same origin).
- **Cross-tenant SaaS metrics:** no dedicated API surface.
- **Load:** 200 concurrent cashiers do not meet the 3 s lab budget (`docs/LOAD_TEST_FINDINGS.md`); production target is 50-200 cashiers.
- **Security advisories:** `npm audit` on the SDK 58 lockfile reports 0 high and 23 moderate (checked 2026-11-24). `docs/SECURITY_ADVISORIES.md` still describes SDK 57 and needs a refresh.
- **Sites:** no unit tests (`test` = `tsc --noEmit`); i18n deferred.

## 4. Conflicts between documentation and code (follow the code)

| Topic | What the code says | What stale docs say | Where |
|---|---|---|---|
| POS Expo SDK | `frontend/package.json`: `expo ^58.0.0`, RN 0.88.1, React 19.3.0 | SDK 57 / RN 0.86.3 / React 19.2.3 | `REGKASSE_AI_ONBOARDING.md`, `.cursorrules` |
| POS SDK inside repo map | SDK 58 | "Expo SDK 56" in the Packages list | `.cursor/rules/000-core.mdc` (its own stack table says 58) |
| Next.js (FA) | `^16.4.6` (React `^19.3.1`) | 16.2.10 / React 19.2.7 | `.cursor/rules/000-core.mdc`, `.cursorrules`, onboarding |
| Next.js (Sites) | `^16.4.6` | 16.2.6 | `.cursor/rules/000-core.mdc` |
| EF Core / ASP.NET packages | 10.0.14 (csproj), Npgsql EF 10.0.3 | 10.0.10; Swashbuckle 10.2.3 | `backend/README.md`, `000-core.mdc`, onboarding. Code (csproj) shows Swashbuckle 10.3.1 |
| Tenant identification | Production shared hosts use JWT `tenant_id`; Dev header/query | "Tenants are identified by subdomain `{tenant}.regkasse.at`" | `REGKASSE_AI_ONBOARDING.md` (older). Newer: `000-core.mdc`, `docs/POS_PRODUCTION_ARCHITECTURE.md` |
| Workspaces / packages | Root workspaces include `frontend-marketing` | Packages list omits it | `000-core.mdc` |
| Roles list | `Roles.Canonical` has 8 roles including `ComplianceOfficer` | Role tables list 7 | onboarding, `100-backend.mdc` |
| Vertical count | 10 seeded profiles listed in `docs/VERTICAL_PROFILES.md` | "11 verticals" | `docs/AI_CONTEXT.md` |
| Permission key for user management | Code constant `UserManage = "user.manage"` | `users.manage` | onboarding |
| POS locales | `frontend/i18n/locales` has de, en, tr | "POS UI is German only" | The rule still stands for operator-facing UI; do not add or translate POS operator text into other languages |
| Onboarding freshness | Last reviewed 2026-07-21 | n/a | `REGKASSE_AI_ONBOARDING.md`. `docs/AI_CONTEXT.md` (2026-11-24) is newer |

When two sources disagree, use this priority: nearest executable code → package config → root config → CI workflows → README and `docs/` → `ai/` guidance.

## 5. Repository hygiene observations

- `backend/` and the repo root contain many build/scratch folders (`_build_out*`, `tmp-*`, `.test-out*`, `verification_output*`, `bin`, `obj`, `publish`, `tmp`, `artifacts`, `test-results`). Most are gitignored. Ignore them when reading the code. Do not add new scratch folders to the tracked tree.
- Untracked/modified files seen at analysis time: `audit.json`, `scripts/load-test/seed-tenants.mjs`, `scripts/load-test/tenants.example.json`, modified `scripts/load-test/pos-critical-path.js`, and `next-env.d.ts` in the admin and sites apps. Do not commit generated `next-env.d.ts` churn or `audit.json` by accident.
- Roughly 12,000 tracked files, about 300 controllers, about 220 `DbSet`s, about 1,245 API paths, about 100 FA feature domains. Large surface: always scope a task to one area.
- `docs/` has about 166 Markdown files, many historical. Prefer `docs/AI_CONTEXT.md`, `AGENTS.md`, `.cursor/rules/*.mdc`, `ai/*.md` for current rules, and treat older reports as context only.

## 6. Items this briefing could not verify

- Exact content of each `PaymentSaleGateService` check and exactly where numbering/signing happen inside `PaymentDocumentPersistenceService` (names and order are verified, details are not).
- The exact middleware order (listed from source line numbers about 2147-2277, but conditional branches exist).
- Whether POS also uses legacy-style routes (for example `/api/CashRegister`, `/api/Tagesabschluss`, `/api/Table`, `/api/Invoice`) beyond `/api/pos/*`. The rule says POS uses `/api/pos/*`, `/api/Auth/*`, `/api/Receipts/*`, while the backend exposes those `api/[controller]` routes. Check `frontend/services/api/*Service.ts` before assuming.
- The exact list of vertical profile ids in the database (10 vs 11).
- Purpose of POS screens `taskmaster`, `simple-todo`, `task-suggestions`.
- Which of the many `Dev*` / `*Test*` controllers are compiled out or blocked in Production.
- Current branch-protection settings (only documented, not inspected).
- Whether every permission constant is wired in the FA registry (CI scripts check this; not re-run here).
