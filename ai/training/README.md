# AI training dataset (`ai/training/`)

Structured pattern + documentation inventory for Regkasse coding agents.

**Not** a substitute for reading source code or [`AGENTS.md`](../../AGENTS.md). On conflict: **code → package config → CI → AGENTS.md → this dataset**.

## Files

| File | Role |
|------|------|
| [`regkasse-ai-dataset.json`](regkasse-ai-dataset.json) | Generated dataset (inventory + curated patterns + doc index + config keys) |
| [`curated-patterns.json`](curated-patterns.json) | Hand-maintained patterns / domain examples (edit this) |
| Generator | [`../../scripts/generate-ai-training-dataset.mjs`](../../scripts/generate-ai-training-dataset.mjs) |

## Schema (summary)

```json
{
  "project": "Regkasse",
  "patterns": { "services": [], "controllers": [], "components": [] },
  "examples": { "payment": [], "tenant": [], "user": [] },
  "documentation": [],
  "inventory": { "services": {}, "controllers": {}, "components": {} },
  "configuration": { "appsettingsExamples": [], "environmentVariables": {} }
}
```

- **`patterns` / `examples`:** curated rules and snippets (from `curated-patterns.json`).
- **`inventory`:** path-level catalogs of `backend/Services`, `backend/Controllers`, `frontend-admin/src/features` (no file bodies).
- **`documentation`:** indexed `docs/**`, README files, API contracts, `ai/*.md` (title + short summary only — **not** full doc bodies).
- **`configuration`:** section keys from `appsettings*.example.json` + env var patterns. **No secrets.**

## Regenerate

From repository root:

```bash
node scripts/generate-ai-training-dataset.mjs
```

After adding a new canonical pattern, update `curated-patterns.json`, then regenerate.

## Security

- Never commit real connection strings, JWT secrets, PEM material, or voucher codes into this folder.
- Prefer `*.example.json` and placeholder env values only.

**Last updated:** 2026-07-24
