# Regkasse — AI Project Briefing (index)

A deep, code-verified introduction to the Regkasse monorepo for an AI that has never seen it (for example ChatGPT). The briefing is split into short parts so you can load only what a task needs.

> **Verification stamp:** written against commit `e2be4d5b` on branch `docs/recent-changes-2026-11-24`, 2026-11-24. Facts come from the repository (package files, controllers, services, middleware, rules, CI). Where documents disagree with code, the briefing follows the code and records the conflict in [part 08](ai-briefing/08-risks-rules-and-gaps.md) §4.
>
> **Not a legal statement.** Nothing here guarantees RKSV or any other legal compliance.

## Parts

| Part | File | Contents | Size |
|---|---|---|---|
| 00 | [`ai-briefing/00-identity-card.md`](ai-briefing/00-identity-card.md) | One-page always-on card: what the project is, package map, ten rules that must never break | about 1 page |
| 01 | [`ai-briefing/01-product-and-architecture.md`](ai-briefing/01-product-and-architecture.md) | Product, users, verticals, architecture diagram, hosts, monorepo map, real stack versions, satellite apps | short |
| 02 | [`ai-briefing/02-backend.md`](ai-briefing/02-backend.md) | Backend structure, controller surfaces, services, middleware order, hosted services, database, caching, tests | long |
| 03 | [`ai-briefing/03-domain-fiscal-payment.md`](ai-briefing/03-domain-fiscal-payment.md) | Domain model, payment pipeline, TSE, RKSV special receipts, DEP, FinanzOnline, offline systems, vouchers, countries, non-fiscal domains | long |
| 04 | [`ai-briefing/04-tenancy-auth-security.md`](ai-briefing/04-tenancy-auth-security.md) | Tenant resolution and isolation, Super Admin, auth, roles and permissions, licenses, limits, backup boundary | medium |
| 05 | [`ai-briefing/05-frontend-pos.md`](ai-briefing/05-frontend-pos.md) | POS app: routes, services, contexts, offline, guards, flows, tests | medium |
| 06 | [`ai-briefing/06-frontend-admin.md`](ai-briefing/06-frontend-admin.md) | Admin panel: layout, auth boundary, routes, Orval client, Ant Design rules, tests | medium |
| 07 | [`ai-briefing/07-api-contracts-ci-testing.md`](ai-briefing/07-api-contracts-ci-testing.md) | API boundaries, OpenAPI workflow, scripts, hooks, CI/CD, testing, commit and PR rules | medium |
| 08 | [`ai-briefing/08-risks-rules-and-gaps.md`](ai-briefing/08-risks-rules-and-gaps.md) | High-risk areas, hard rules, known gaps, doc-vs-code conflicts, unverified items | medium |
| 09 | [`ai-briefing/09-prompting-guide-and-glossary.md`](ai-briefing/09-prompting-guide-and-glossary.md) | How to write task prompts, clarifying-question patterns (PDF example), five templates, glossary | long |
| 10 | [`ai-briefing/10-chatgpt-instructions.md`](ai-briefing/10-chatgpt-instructions.md) | Ready-to-paste ChatGPT Project instructions (meta-prompt) and setup steps | short |

## How to load it into ChatGPT

**Recommended: a ChatGPT Project.**

1. Create a Project named "Regkasse".
2. Paste the instruction block from part 10 into the Project Instructions.
3. Upload parts 00-09 as project files (part 00 and part 08 matter most).
4. Start a chat and ask: "Summarize the project in 10 bullets and list the five riskiest rules." Compare the answer with part 00 and part 08. Fix the briefing where the answer is wrong.
5. Use one chat per task. Paste Cursor's final report back into the same chat for review.

**Alternative: a single chat.** Paste part 10's block, then part 00, then the parts the task needs (see below). Do not paste everything at once.

## Which parts to give for which task

| Task | Parts |
|---|---|
| Any task | 00, 08 |
| Backend endpoint or service | 02, 04, 07 |
| Payment, receipts, RKSV, TSE, FinanzOnline | 03, 08 |
| Tenancy, auth, permissions | 04 |
| POS screen | 05, 03 |
| Admin screen | 06, 04, 07 |
| Database migration | 02 (§6), 08 |
| CI, scripts, release | 07 |
| Writing prompts for others | 09, 10 |

## Maintenance

- Re-verify after large changes: new modules, stack upgrades (Expo, Next.js, .NET), auth or tenancy changes, new fiscal or country packages, CI changes. Ask the coding agent to update only the affected part and the verification stamp above.
- Keep each part below about 20 KB so it stays easy to upload.
- Do not copy secrets, tokens, or real connection strings into any part. The briefing contains none.
- Canonical sources stay: `AGENTS.md`, `.cursor/rules/*.mdc`, `docs/AI_CONTEXT.md`, `ai/*.md`. This briefing summarizes them and adds code-verified detail. If a canonical source changes a rule, update the matching part here.
