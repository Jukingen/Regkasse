# 09 — Prompting Guide, Task Templates and Glossary

> Part 9 of the Regkasse AI briefing. Index: [`../AI_PROJECT_BRIEFING.md`](../AI_PROJECT_BRIEFING.md).
> Verified at commit `e2be4d5b` (2026-11-24). Audience: an AI (for example ChatGPT) that writes task prompts for a coding agent (Cursor) which can see and edit the code. The prompt writer cannot see the code.

## 1. Division of labor

| The prompt writer (you) | The coding agent (Cursor) |
|---|---|
| Decides WHAT and WHY, scope, constraints, acceptance criteria, tests | Finds the files, reads the real code, makes the edits, runs the tests |
| Knows the briefing (rules, risk, architecture) | Sees the actual repository |
| Does not invent file paths, endpoints, class names, or commands | Verifies every claim in the code first |

If a path or name is not stated in the briefing, write "find and verify" in the prompt. Do not present it as fact.

## 2. Process for every user request

1. **Classify:** package (backend / POS / FA / sites / marketing / localization / docs / CI), type (feature, bugfix, refactor, migration, tests, docs), risk (Low, Medium, High).
2. **Is the request ambiguous?** If it uses a vague verb ("fix", "improve", "clean up") or a plural component ("the PDFs", "the reports", "the payment screen"), do not write the prompt yet. Ask at most 3 multiple-choice questions (see §3). Then write the prompt.
3. **Size it:** small → one prompt; medium or large → stages, each independently verifiable, in dependency order (usually: backend + tests → OpenAPI/Orval client → FA or POS UI → i18n).
4. **Add constraints from the briefing:** API boundary, tenant 404 rule, additive migrations, UI language, Ant Design rules, singleton + scope pattern, no secrets.
5. **Write the prompt** with the template in §4.
6. **Self-check** with the checklist in §6.
7. **After the agent finishes:** review its summary against the acceptance criteria. Write a correction prompt if something is missing or risky.

## 3. Clarifying questions for ambiguous requests

Use these shapes. Offer options taken from this briefing, always add "Other".

### Example: "Fix the PDF formats"

The repository has many PDF generators (QuestPDF in `backend/Services`):

| Group | Generators |
|---|---|
| Receipts | `ReceiptPdfService`, `Receipt/SpecialReceiptPdfService` |
| Invoices | `InvoicePdfService`, `Billing/InvoicePdfGenerator` |
| Reports | `Reports/TagesberichtPdfGenerator`, `MonatsbelegPdfGenerator`, `JahresberichtPdfGenerator`, `DailyClosingReportPdfGenerator`, `PeriodSalesPdfSections`, `PermissionAuditReportPdfGenerator`, `AdminOperationalReportPdfGenerator` |
| Fiscal / RKSV | `FiscalExportPdfGenerator`, `RksvComplianceReportPdfGenerator`, `FiskalyStatisticsPdfGenerator`, `FiskalyErrorPdfGenerator` |
| Other | `License/LicenseCertificatePdfGenerator`, `Countries/QrRechnung/QrRechnungPdf`, `Backup/RestoreDrillPdfRenderer` |

POS printing also uses `expo-print` on the client. Ask:

1. Which PDF or print output? (receipt / invoice / report / fiscal export / license / other)
2. What does "fix" mean? (overflow or page breaks / fonts and umlauts ä ö ü ß / alignment and tables / number, tax, or date formats / QR or signature area / something else). Ask for an example file or screenshot and the scenario (tenant, number of lines, language).
3. Must fiscal values stay identical? (They must. Receipt and fiscal PDFs change layout only. QR payload, signature fields, receipt numbering, and tax values stay untouched.)

Then write the prompt. Receipt and fiscal PDFs are High risk: add "plan first".

### Other common ambiguous requests

- "Improve the payment screen": POS cart/payment UI, the payment modal, or the admin payments list? Which problem (speed, layout, validation, error message)? Does it touch fiscal flow?
- "Add a report": which report, who sees it (permission), POS, FA, or PDF? Period, filters, export format?
- "Make users manageable": which roles can manage whom, which tenant scope, audit needs?
- "Support another country": do not start. Explain that only Austria is production-ready and ask for the exact package in `docs/COUNTRIES.md` §16.

## 4. Cursor task prompt template

Write the output in a single code block, in Turkish (or the user's language), keeping code identifiers in English.

```text
## Goal
(1-3 sentences: what changes and why)

## Scope
- Package(s): ...
- Areas that may be touched: ...
- Areas that must NOT be touched: ...

## Read first
- AGENTS.md, .cursor/rules/000-core.mdc and the matching domain rule (100 backend, 101 admin, 102 POS, 103 database, 104 fiscal, 105 testing)
- Relevant ai/ and docs/ files (list them)
- Find the closest existing example (controller/screen/hook/test) and follow its pattern. Verify every file name in code before editing.

## Requirements
1. ...
2. ...

## Constraints
- (API boundary, tenant isolation and 404, migration rules, UI language, Ant Design rules, backward compatibility, no unrelated refactors)

## Acceptance criteria
- [ ] ...
- [ ] ...

## Tests
- Tests to add or update and the commands to run (verify commands against package.json / CONTRIBUTING.md).

## Deliverables
- When done, report: changed files, root cause or design summary, risks, backward-compatibility impact, how it was verified, open questions. State assumptions explicitly.
```

For **High-risk** work (payments, fiscal/TSE/RKSV, tenant isolation, auth/RBAC, FinanzOnline outbox, vouchers, backup/restore) add at the top:

```text
FIRST present an implementation plan (files, risks, test strategy) and wait for my approval before writing code.
Read ai/07_DO_NOT_TOUCH.md and ai/05_SECURITY_COMPLIANCE.md. Do not change signature, receipt numbering, QR payload, tax or rounding behavior.
```

## 5. Ready templates (adapt names after the agent verifies them in code)

### 5.1 Add or change a backend endpoint (Admin)

```text
## Goal
Add `GET /api/admin/<resource>` that returns <data> for the current tenant (Manager) and all tenants for SuperAdmin where applicable.

## Scope
- Package: backend (+ frontend-admin client regeneration)
- Do not touch: PaymentService, TSE, migrations of existing tables

## Read first
- .cursor/rules/000-core.mdc, 100-backend.mdc, ai/01_BACKEND_CONTRACT.md, ai/03_API_CONTRACT.md, ai/10_API_BOUNDARY_POLICY.md
- Find a similar Admin*Controller (same domain) and follow its structure, DTO naming, [HasPermission] usage, and audit pattern.

## Requirements
1. Controller action + service method; tenant-scoped query via the existing filters (no IgnoreQueryFilters).
2. Permission: reuse an existing key from AppPermissions; if a new one is needed, STOP and ask.
3. Cross-tenant id → 404.
4. DTOs in the existing DTO location; English error messages.

## Acceptance criteria
- [ ] Manager sees only own tenant data; cross-tenant id returns 404.
- [ ] Unauthorized role gets 403.
- [ ] swagger.json regenerated and FA client regenerated; verify-api-client passes.

## Tests
- Unit tests for the service, integration test for tenant isolation and permission. Run dotnet test with a filter, then node scripts/verify-api-client.mjs.
```

### 5.2 Add an Admin (FA) screen

```text
## Goal
Add a list page at `(protected)/<route>` showing <data> using the generated Orval hooks.

## Read first
- .cursor/rules/101-frontend-admin.mdc; frontend-admin/README.md
- Follow a similar feature under frontend-admin/src/features/<domain> (components, hooks, tests) and its route in src/app/(protected).

## Requirements
1. Use generated React Query hooks (do not hand-write API calls unless the spec lacks the endpoint).
2. All text via i18n keys in de, en, tr; regenerate the key union.
3. Permission gate with the existing permission key and menu mapping (menuPermissionRegistry, routePermissions).
4. Toasts with useNotify(); confirmations with useAntdApp().modal. No static antd message/notification/Modal.confirm.

## Acceptance criteria
- [ ] Page hidden and route guarded for roles without the permission (inline 403).
- [ ] Loading, empty, and error states handled.
- [ ] npm run typecheck, lint, test, and i18n:ci pass; verify:menu-permissions passes.
```

### 5.3 Change a POS screen

```text
## Goal
<change> on POS screen `app/(tabs)/<screen>.tsx`.

## Read first
- .cursor/rules/102-frontend-pos.mdc; frontend/i18n/README.md
- frontend/services/api/apiPaths.ts and the matching service; the tests under frontend/__tests__.

## Constraints
- German UI text only (add to the German locale files; keep other locale files consistent).
- No call to /api/admin/*. New paths go into apiPaths.ts and must exist in backend/swagger.json.
- Do not change payment, cart sync, offline queue, or fiscal behavior unless explicitly in scope. Backend stays the final authority.

## Acceptance criteria
- [ ] <behavior>
- [ ] npm run typecheck, lint, and test pass in frontend/; i18n:ci passes.
```

### 5.4 Add a database migration

```text
## Goal
Add column/table <x> for <feature>.

## Read first
- .cursor/rules/103-database.mdc, ai/02_DATABASE_CONTRACT.md, ai/02_DATABASE_OVERVIEW.md
- The latest migrations in backend/Migrations for naming and style.

## Constraints
- Additive only. Never edit a committed migration. Do not delete columns.
- Tenant-scoped table: implements ITenantEntity, non-null tenant_id uuid, indexed FK, global filter registered in AppDbContext.
- Backfill in a separate migration.
- Do not add a CountryCode column (country lives in company_settings.country).

## Tests
- Run migrations on PostgreSQL integration tests; add a tenant isolation test.
```

### 5.5 Bug fix

```text
## Problem
<observed behavior, expected behavior, steps, environment, logs/screenshots>

## Task
1. Reproduce or locate the root cause in code (do not guess). Explain it before changing code.
2. Write a failing test first when practical.
3. Make the smallest fix. No refactors.

## Constraints
- <area-specific rules; if fiscal/payment/tenant/auth: plan first>

## Acceptance criteria
- [ ] The failing test now passes; neighbors still pass.
- [ ] No behavior change outside the bug.
```

## 6. Self-check before sending a prompt

- Right package and right API prefix (POS `/api/pos/*`; Admin `/api/admin/*`)?
- Tenant isolation and the 404 rule stated where relevant?
- DB change: additive, no edit of committed migrations, backfill separate?
- UI text language right (POS German; FA i18n de/en/tr with all three updated)?
- Backend API changed → swagger + Orval + `verify-api-client` step included?
- Permissions: existing keys only? New keys require backend + FA registry + menu mapping updates.
- Acceptance criteria measurable? Tests and commands named?
- "No unrelated changes" stated?
- High-risk area → plan-first block included?
- No unverified file path or name presented as fact?

## 7. Which docs to attach for which task

| Task type | Attach (in addition to part 00) |
|---|---|
| Backend endpoint or service | 02, 04, `ai/01_BACKEND_CONTRACT.md`, `ai/03_API_CONTRACT.md` |
| Tenancy, auth, permissions | 04, 08 |
| Payment, receipts, RKSV, TSE | 03, 08, `ai/05_SECURITY_COMPLIANCE.md`, `ai/07_DO_NOT_TOUCH.md` |
| POS UI | 05, 03 (flows), 08 |
| Admin UI | 06, 04 (permissions), 07 (Orval) |
| Migration or DB | 02 (§6), `ai/02_DATABASE_CONTRACT.md`, `.cursor/rules/103-database.mdc` |
| CI, release, scripts | 07 |
| Digital services, online orders, storefront | 03 (§10), 01 (§6), `docs/DIGITAL_SERVICES.md`, `docs/ONLINE_ORDERS.md` |
| Country, VAT, e-invoicing | 03 (§9), `docs/COUNTRIES.md` |
| Backup, restore, DR | 04 (§7), `docs/BACKUP_AND_DISASTER_RECOVERY.md` |

## 8. Glossary

| Term | Meaning |
|---|---|
| Kasse | Cash register (device or logical register). In docs, "POS" is the product and "cash register" is the device/session |
| Mandant / tenant | A customer company. "Mandanten-Admin" is the `Manager` role |
| FA | Frontend-Admin panel (`frontend-admin/`) |
| FE | Frontend (the POS app) in some docs |
| RKSV | Austrian Registrierkassensicherheitsverordnung (cash register security regulation) |
| TSE | Technische Sicherheitseinrichtung (signing device or service; here also Fiskaly) |
| BMF | Austrian Federal Ministry of Finance |
| FinanzOnline (FON/FO) | BMF online portal and web service; here: outbox + submission tracking |
| DEP / DEP §7 | Datenerfassungsprotokoll: signed receipt journal export for tax audit |
| JWS | JSON Web Signature, compact form `header.payload.signature` |
| Belegcheck | BMF app to verify receipts manually |
| Nullbeleg | Zero-value signed receipt |
| Startbeleg | First signed receipt of a register |
| Monatsbeleg | Monthly zero-value control receipt |
| Jahresbeleg | Yearly zero-value control receipt |
| Schlussbeleg / Endbeleg | Final receipt that permanently decommissions a register |
| Tagesabschluss | Daily closing (also monthly/yearly variants) |
| Gutschein | Voucher |
| Storno | Full cancellation of a receipt (with `StornoReason` and original link) |
| Beleg / Kassenbon | Receipt |
| Sonderbeleg | Special RKSV receipt |
| Signature chain | Each receipt signs over the previous receipt's signature value |
| Vertical profile | Product configuration for a business type (gastronomy, retail, taxi, ...) |
| Beherbergung | Lodging (rooms, guest folios) |
| Orval | OpenAPI → TypeScript/React Query client generator used by FA |
| Husky | Git hook runner (pre-commit gate) |
| BYOK | Bring your own key (per-tenant Fiskaly credentials) |
| Canary | Progressive deployment to a preferred tenant first |
| Impersonation | Super Admin acts as a tenant via a special JWT |
| Ambient tenant | The tenant bound to the current request (`ICurrentTenantAccessor.TenantId`) |
| Platform sentinel | Inactive system tenant row (`platform`), not a real mandant |
