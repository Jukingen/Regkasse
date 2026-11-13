# Documentation standards

English is the language of repository documentation (READMEs, `docs/`, `ai/`, runbooks). This file is the style guide. Track remaining translations in [`TRANSLATION_CHECKLIST.md`](TRANSLATION_CHECKLIST.md).

**Last updated:** 2026-08-23

---

## Language

- Write **American English** (`color`, `behavior`, `canceled`, `license` — not *colour*, *behaviour*, *cancelled*, *licence*).
- Use **active voice** and short sentences.
- Prefer tables for structured data and fenced code blocks for commands.
- Do not claim legal RKSV / BMF / FinanzOnline compliance. Fiscal docs are operational and diagnostic unless a lawyer signs off.

## What stays non-English

| Surface | Language | Why |
|---------|----------|-----|
| POS UI copy | German (de-DE) | Product rule (`AGENTS.md`) |
| Admin UI | i18n de / en / tr | Locale catalogs, not markdown |
| IDE explanations to developers | Turkish | `AGENTS.md` Language Rules |
| `docs/*.de.md` | German | Operator-facing Docker/Windows twins of the English hub |
| RKSV proper names | German | Nullbeleg, Startbeleg, Monatsbeleg, Jahresbeleg, Schlussbeleg, Tagesabschluss |
| Code comments | English or Turkish | Existing dual-comment policy |

Do **not** translate POS UI strings in docs examples into English.

## Product terminology

Use these terms consistently. Do **not** rename product surfaces to match a generic “cash register only” glossary.

| Use | Do not use (in this meaning) | Notes |
|-----|------------------------------|-------|
| **tenant** / **mandant** | customer, client (for the mandant) | End customers of a restaurant are still “customers” in POS/order context |
| **POS** | “the cash register app” as a rewrite of POS | Cashier application: `pos.regkasse.at`, package `frontend/`. Keep **POS** |
| **cash register** / **register** | POS (when you mean the device) | Hardware or logical Kasse (`CashRegister`), open/close/decommission |
| **FA** / **Admin** | “backoffice POS” | Next.js admin (`frontend-admin`, `admin.regkasse.at`) |
| **Sites** | POS | Tenant storefronts (`frontend-sites`) |
| **license** | licence | Billing and deployment keys |
| **Super Admin** | superuser | Platform operator |
| **Mandanten-Admin** | tenant admin (in FA UI copy) | Backend role remains `Manager` |
| **TSE** | “fiscal box” | Signature device / Fiskaly SCU |
| **RKSV** | generic “fiscal law” | Austrian cash-register ordinance |

**POS vs cash register:** POS is the product (UI + `/api/pos/*`). A cash register is the fiscal device/session the POS talks to. Both terms are required.

## Voice and structure

- Lead with the fact or the command, then the exception.
- Link hubs instead of duplicating `AGENTS.md`.
- On conflict: **code → package config → CI → `AGENTS.md` → these docs**.
- Mark uncertainty with `UNKNOWN` rather than inventing behavior.
- Keep secrets out of docs. Use placeholders (`YOUR_PASSWORD`, vault refs).

## File conventions

- New docs: English filename, `SCREAMING_SNAKE` or `kebab-case` matching neighbors.
- German twins: same stem + `.de.md` (example: `DOCKER.md` / `DOCKER.de.md`).
- Put operator runbooks in `docs/`. Put agent contracts in `ai/`. Do not copy fiscal rules into both unless one clearly points to the other.

## Review

Before merging a doc change:

1. Terminology matches the table above.
2. Commands are copy-pasteable.
3. Cross-links resolve (no renamed-file leftovers).
4. POS UI examples stay German.
5. American spelling for new prose.
