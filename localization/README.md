# Localization CSV Interchange Pipeline

Canonical source of truth is JSON catalogs under app locale folders.
CSV is an interchange/edit format only.

## Design

- Canonical JSON:
  - `frontend/i18n/locales/{de,en,tr}/*.json`
  - `frontend-admin/src/i18n/locales/{de,en,tr}/*.json`
- Shared tooling:
  - `localization/scripts/export-translations.mjs`
  - `localization/scripts/import-translations.mjs`
  - `localization/scripts/validate-translations.mjs`
- Contract source:
  - `localization/namespace-manifest.json` — her `app.namespaces[]` girdisi, ilgili uygulamada **`de/<stem>.json`** dosya kök adıyla eşleşmelidir (ör. `frontend-admin`: `admin-shell` → `de/admin-shell.json`). **frontend-admin** için `t('…')` içindeki ilk segment çoğu zaman **camelCase** catalog adıdır (`adminShell`); dosya adı ise `admin-shell.json` olduğundan manifest’te kebab kök görünür — ayrıntı: `frontend-admin/src/i18n/README.md`.
- CSV matrix format (one row per key):
  - `app,namespace,key,description,de,en,tr,status,notes`

## Commands

- Export one app:
  - `node localization/scripts/export-translations.mjs --app frontend`
  - `node localization/scripts/export-translations.mjs --app frontend-admin`
- Export all apps into one deterministic matrix:
  - `node localization/scripts/export-translations.mjs --app all`
- Import CSV to JSON:
  - `node localization/scripts/import-translations.mjs --app frontend --input localization/out/frontend.csv`
  - `node localization/scripts/import-translations.mjs --app frontend-admin --input localization/out/frontend-admin.csv`
- Dry run:
  - `node localization/scripts/export-translations.mjs --app frontend --dry-run`
  - `node localization/scripts/import-translations.mjs --app frontend-admin --dry-run`
- Validate:
  - `node localization/scripts/validate-translations.mjs --app frontend`
  - `node localization/scripts/validate-translations.mjs --app frontend-admin`

## Validation Rules

- Required tuple columns: `app`, `namespace`, `key`
- Invalid JSON in canonical locale files: hard-fail with file + line/column context
- Required locale text: `de`
- Optional locale text: `en`, `tr` (warning)
- Unique tuple: `(app, namespace, key)`
- Namespace allowlist enforced from manifest
- Reserved prefix block enforced from manifest
- Invalid key naming (`keyPattern`) is a failure

### Key naming modes

- `compat` (default):
  - Enforces manifest `keyPattern` as hard-fail
  - Evaluates `targetKeyPattern` as warning only
- `target`:
  - Enforces both `keyPattern` and `targetKeyPattern` as hard-fail

Command examples:

- `node localization/scripts/validate-translations.mjs --app frontend-admin --keyMode compat`
- `node localization/scripts/validate-translations.mjs --app frontend-admin --keyMode target`

## Determinism

- Rows are sorted by `(app, namespace, key)` on export
- Import flattens/unflattens and rewrites JSON sorted deeply
- JSON rewrite uses stable indentation and newline for diff-friendly output

## Parse error behavior

- `validate-translations`: reports JSON parse errors as failures per file and continues collecting additional failures.
- `export-translations` / `import-translations`: fail-fast on invalid JSON to prevent partial/corrupt pipeline operations.

## Atomic write behavior

- JSON and CSV writes use atomic replace (`temp file` -> `rename`) to reduce partial corruption risk.
- Import is still a multi-file operation; atomic replace protects each file boundary, but it is not a full repo-wide transaction.

## Legacy Wrapper Commands

Old `tools/i18n/*.mjs` scripts now delegate to shared `localization/scripts/*`.
Use either style; shared scripts are the primary implementation.
