# Localization tooling

Shared i18n import/export/validation for monorepo UI packages.

| App | Locale catalogs | CI status |
|-----|-----------------|-----------|
| `frontend` (POS) | `frontend/i18n/locales` | Validated (`--strictMissing true`) |
| `frontend-admin` (FA) | `frontend-admin/src/i18n/locales` | Validated (`--strictMissing true` + `--orphanPolicy error` + usage) |
| `frontend-sites` | *(none yet — German hardcoded UI)* | Registered as **`deferred`** in the manifest; scripts skip with exit 0 |

When Sites gains locale files: create `frontend-sites/src/i18n/locales/{de,en,tr}/`, set `"deferred": false`, add namespaces, then CI will enforce them.

## Layout

| Path | Purpose |
|------|---------|
| `scripts/` | `export-translations`, `import-translations`, `validate-translations`, boundary & usage checks |
| `schema/` | CSV/row JSON schemas |
| `namespace-manifest.json` | App namespaces, paths, optional `deferred` |
| `i18n-ci-budgets.json` | CI usage budgets |
| `baseline/` | Boundary baseline for diffs |
| `out/` | Generated CSV / reports |

## Local commands

From **repository root** (scripts resolve paths from the monorepo root):

```bash
# Hard gates (same as CI)
node localization/scripts/validate-translations.mjs --app frontend-admin --strictMissing true --orphanPolicy error
node localization/scripts/validate-translations.mjs --app frontend --strictMissing true
node localization/scripts/check-localization-usage.mjs --app frontend-admin --strictMissing true --budgetFile localization/i18n-ci-budgets.json

# Deferred Sites smoke (prints skip, exit 0)
node localization/scripts/validate-translations.mjs --app frontend-sites

# Convenience (active apps only)
npm run i18n:validate
npm run i18n:ci
cd localization && npm run ci
```

App package scripts: `frontend` / `frontend-admin` → `npm run i18n:*`.

## CI integration

| Workflow | What runs |
|----------|-----------|
| [`localization-validation.yml`](../.github/workflows/localization-validation.yml) | Full i18n job on `pull_request` / `push` to `main`/`master` (path-filtered). **Hard-fails** on the three commands above; missing translations fail the build. |
| [`frontend-admin-ci.yml`](../.github/workflows/frontend-admin-ci.yml) | Admin validate + usage (strict) on FA changes |
| [`frontend-ci.yml`](../.github/workflows/frontend-ci.yml) | POS validate (strict missing) on POS changes |

Phase vars (`LOCALIZATION_BOUNDARY_PHASE`) still control **boundary / keyMode** rollout after the hard gate. They do **not** soften `--strictMissing` / orphan failures.

## Scripts

| Script | Description |
|--------|-------------|
| `npm run export` | Export active apps to CSV under `out/` |
| `npm run import` | Import CSV back into locale files (use with care) |
| `npm run validate` | Validate active apps |
| `npm run boundary` | Translation boundary check |
| `npm run usage` | Usage / budget check |
| `npm run ci` | Strict CI gate (admin + POS validate + usage + boundary) |

## Rules of thumb

- POS user-facing strings stay **German** (`de-DE`); locale files still need `en`/`tr` parity when `--strictMissing true`.
- Admin strings go through FA locale files (de/en/tr) — no hardcoded UI copy in components.
- Prefer existing keys; expand namespaces via `namespace-manifest.json` when needed.
- Unknown `--app` fails with the list of registered apps.

## Related

- [`frontend/i18n/README.md`](../frontend/i18n/README.md)
- [`frontend-admin/src/i18n/README.md`](../frontend-admin/src/i18n/README.md)
- [`.github/workflows/README.md`](../.github/workflows/README.md)

## License

Proprietary — All rights reserved. See [`../LICENSE`](../LICENSE).
