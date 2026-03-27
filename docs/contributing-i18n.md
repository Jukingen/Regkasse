# Localization (i18n) — contributor guide

## Commands (frontend-admin)

| Script | Purpose |
|--------|---------|
| `npm run i18n:validate` | Locale JSON checks (soft: missing en/tr are warnings unless you pass flags). |
| `npm run i18n:validate:ci` | **CI gate:** `--strictMissing true` (en/tr must match `de`) + `--orphanPolicy error` (manifest allowlist). |
| `npm run i18n:usage` | Scan `t(...)` usage vs locale files; heuristic “hardcoded UI” hints. |
| `npm run i18n:usage:ci` | Usage with **budgets** from `localization/i18n-ci-budgets.json`. |
| `npm run i18n:boundary` | Forbidden patterns (e.g. `t(product.name)`). |
| `npm run i18n:ci` | Full admin pipeline: validate:ci → boundary → usage:ci → generated keys drift check. |

Run from `frontend-admin/` so paths resolve correctly.

## CI (GitHub Actions)

Workflow: `.github/workflows/localization-validation.yml`

- **Validate (admin):** structural failures (duplicate keys, empty `de`, orphan JSON files, runtime/manifest mismatch) fail the job. With `--strictMissing`, missing `en`/`tr` for any `de` key fails.
- **Usage:** code vs JSON; budgets control how many **hardcoded UI candidates** and **unregistered dynamic `t(\`...\`)` templates** are allowed before failure.
- **Artifacts:** `localization/out/reports/*.json` include `usage-report.frontend-admin.json` and `validate-report.frontend-admin.json`.

## Budgets (`localization/i18n-ci-budgets.json`)

Per-app knobs for `check-localization-usage.mjs`:

| Field | Meaning |
|-------|---------|
| `maxMissingLocalePairs` | Max combined count of **missing en + missing tr** for keys used in code when `--strictMissing` is on. `0` = no gaps allowed. |
| `maxHardcodedUi` | Max **hardcoded UI candidate** rows (heuristic) before the job fails. Lower over time to drive strings into i18n. |
| `maxDynamicUnresolved` | Max `t(\`prefix.${var}\`)` call sites **without** a rule in `localization/dynamic-key-expansions.json` before failure. |

CLI overrides (optional): `--maxMissingLocalePairs`, `--maxHardcodedUi`, `--maxDynamicUnresolved`, `--budgetFile <path>`.

## Dynamic template keys

If you use `` t(`foo.bar.${x}`) ``, add a **prefix + suffix list** to `localization/dynamic-key-expansions.json` so the usage checker expands to real keys (see existing Report Center / users examples).

## Failure output

Scripts print a short **“How to fix”** footer on non-zero exit. Open the JSON report under `localization/out/reports/` for full lists.
